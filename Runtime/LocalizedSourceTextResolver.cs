using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Resolves business-code source text through a String Table without requiring callers to know a key.
/// The Simplified Chinese table is treated as the source-text index.
/// </summary>
public static class LocalizedSourceTextResolver
{
    public const string DefaultTable = "ProjectText";

    private const string SourceLocaleCode = "zh-Hans";
    private static readonly Regex TemplateTokenRegex =
        new Regex(@"\{(?!(?:确认|取消|合体键|换脚键|撤回键)\})[^{}]+\}", RegexOptions.Compiled);
    private static readonly Regex HanSpacingRegex =
        new Regex(@"(?<=[\u3400-\u9fff])[\p{Zs}\t]+(?=[\u3400-\u9fff])", RegexOptions.Compiled);

    private sealed class SourceEntry
    {
        public long Id;
        public string Source;
        public int LiteralLength;
        public bool Preferred;
    }

    private sealed class SourceIndex
    {
        public readonly Dictionary<string, SourceEntry> Exact =
            new Dictionary<string, SourceEntry>(StringComparer.Ordinal);
        public readonly List<SourceEntry> Templates = new List<SourceEntry>();
    }

    private static readonly Dictionary<string, Task<SourceIndex>> IndexTasks =
        new Dictionary<string, Task<SourceIndex>>(StringComparer.Ordinal);
    private static readonly object SyncRoot = new object();
    private static readonly Dictionary<string, HashSet<long>> PreferredEntries =
        new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);

    /// <summary>
    /// Converts source text to the selected locale. Missing tables, keys or translations safely return sourceText.
    /// </summary>
    public static Task<string> ResolveAsync(string sourceText, string tableName = DefaultTable, Locale locale = null)
    {
        return ResolveAsync(sourceText, tableName, locale, 0);
    }

    private static async Task<string> ResolveAsync(string sourceText, string tableName, Locale locale, int depth)
    {
        if (string.IsNullOrEmpty(sourceText) || string.IsNullOrEmpty(tableName))
            return sourceText;

        try
        {
            await LocalizationSettings.InitializationOperation.Task;
            // Resolve every segment against the same locale, even if a switch happens mid-await.
            if (locale == null) locale = await LocalizationSettings.SelectedLocaleAsync.Task;
            var index = await GetIndexAsync(tableName);
            if (!TryResolveEntry(index, sourceText, out var sourceEntry, out var templateValues))
            {
                // Authored statistics wrap captions and values in TMP tags. Translate only text runs;
                // the tags, sizes, colors and live numbers must survive unchanged.
                if (depth < 8 && Regex.IsMatch(sourceText, @"<[a-zA-Z/][^<>]*>"))
                {
                    var runs = Regex.Split(sourceText, @"(<[a-zA-Z/][^<>]*>)");
                    for (int i = 0; i < runs.Length; i += 2)
                        if (!string.IsNullOrEmpty(runs[i])) runs[i] = await ResolveAsync(runs[i], tableName, locale, depth + 1);
                    return string.Concat(runs);
                }
                // Business summaries join independently translatable labels with bullets/newlines.
                // Keep identifiers and separators intact rather than requiring every combination as a key.
                if (depth < 8 && (sourceText.Contains(" · ") || sourceText.Contains("\n")))
                {
                    var parts = Regex.Split(sourceText, @"( · |\r?\n)");
                    for (int i = 0; i < parts.Length; i += 2)
                        if (!string.IsNullOrEmpty(parts[i])) parts[i] = await ResolveAsync(parts[i], tableName, locale, depth + 1);
                    return string.Concat(parts);
                }
                // Trim only after splitting separators: a leading " · " belongs to the layout.
                // Preserve padding so arrow labels and authored spacing do not change.
                if (depth < 8)
                {
                    var trimmed = sourceText.Trim();
                    if (trimmed.Length > 0 && trimmed.Length != sourceText.Length)
                    {
                        int start = sourceText.IndexOf(trimmed, StringComparison.Ordinal);
                        return sourceText.Substring(0, start)
                            + await ResolveAsync(trimmed, tableName, locale, depth + 1)
                            + sourceText.Substring(start + trimmed.Length);
                    }
                }
                return sourceText;
            }

            TableReference tableReference = tableName;
            TableEntryReference entryReference = sourceEntry.Id;
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                tableReference, entryReference, locale);
            var localized = await operation.Task;
            if (string.IsNullOrEmpty(localized) || localized.StartsWith("No translation found", StringComparison.Ordinal))
                return sourceText;

            return await ApplyTemplateValuesAsync(localized, templateValues, tableName, locale, depth);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"本地化原文转换失败，已回退原文：{sourceText}\n{exception.Message}");
            return sourceText;
        }
    }

    /// <summary>Clears cached source indexes after String Tables are regenerated at runtime or in play mode.</summary>
    public static void ClearCache()
    {
        lock (SyncRoot)
            IndexTasks.Clear();
    }

    /// <summary>Cloud snapshot entries win when a bundled key has the same source text.</summary>
    public static void SetPreferredEntries(string tableName, IEnumerable<long> ids)
    {
        lock (SyncRoot)
        {
            PreferredEntries[tableName] = new HashSet<long>(ids);
            IndexTasks.Remove(tableName);
        }
    }

    public static void ClearPreferredEntries()
    {
        lock (SyncRoot)
        {
            PreferredEntries.Clear();
            IndexTasks.Clear();
        }
    }

    private static Task<SourceIndex> GetIndexAsync(string tableName)
    {
        lock (SyncRoot)
        {
            if (!IndexTasks.TryGetValue(tableName, out var task))
            {
                task = BuildIndexAsync(tableName);
                IndexTasks.Add(tableName, task);
            }
            return task;
        }
    }

    private static async Task<SourceIndex> BuildIndexAsync(string tableName)
    {
        var sourceLocale = LocalizationSettings.AvailableLocales.GetLocale(SourceLocaleCode);
        if (sourceLocale == null)
            throw new InvalidOperationException($"找不到源语言：{SourceLocaleCode}");

        TableReference tableReference = tableName;
        var operation = LocalizationSettings.StringDatabase.GetTableAsync(tableReference, sourceLocale);
        var table = await operation.Task;
        if (table == null)
            throw new InvalidOperationException($"找不到 String Table：{tableName}_{SourceLocaleCode}");

        HashSet<long> preferred;
        lock (SyncRoot) PreferredEntries.TryGetValue(tableName, out preferred);
        var index = new SourceIndex();
        foreach (var tableEntry in table.Values.OrderByDescending(entry => entry != null && preferred != null && preferred.Contains(entry.KeyId)))
        {
            if (tableEntry == null || string.IsNullOrEmpty(tableEntry.Value))
                continue;

            var source = DecodeEscapedBraces(tableEntry.Value);
            var entry = new SourceEntry
            {
                Id = tableEntry.KeyId,
                Source = source,
                LiteralLength = TemplateTokenRegex.Replace(source, string.Empty).Length,
                Preferred = preferred != null && preferred.Contains(tableEntry.KeyId)
            };

            AddExact(index, source, entry);
            var normalized = NormalizeHanSpacing(source);
            if (!string.Equals(source, normalized, StringComparison.Ordinal))
                AddExact(index, normalized, entry);

            if (TemplateTokenRegex.IsMatch(source))
                index.Templates.Add(entry);
        }

        // Some existing simulator controls choose their business source from the OS/site language.
        // Recognize English sources as aliases of the same entry, so any selected locale can follow.
        var englishLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
        if (englishLocale != null)
        {
            var englishTable = await LocalizationSettings.StringDatabase.GetTableAsync(tableReference, englishLocale).Task;
            if (englishTable != null)
                foreach (var tableEntry in englishTable.Values.OrderByDescending(entry => entry != null && preferred != null && preferred.Contains(entry.KeyId)))
                {
                    if (tableEntry == null || string.IsNullOrEmpty(tableEntry.Value)) continue;
                    var source = DecodeEscapedBraces(tableEntry.Value);
                    var entry = new SourceEntry
                    {
                        Id = tableEntry.KeyId,
                        Source = source,
                        LiteralLength = TemplateTokenRegex.Replace(source, string.Empty).Length,
                        Preferred = preferred != null && preferred.Contains(tableEntry.KeyId)
                    };
                    AddExact(index, source, entry);
                    AddExact(index, NormalizeHanSpacing(source), entry);
                    if (TemplateTokenRegex.IsMatch(source)) index.Templates.Add(entry);
                }
        }

        // More literal context wins, so a broad template cannot steal a more specific match.
        index.Templates.Sort((left, right) => left.LiteralLength == right.LiteralLength
            ? right.Preferred.CompareTo(left.Preferred) : right.LiteralLength.CompareTo(left.LiteralLength));
        return index;
    }

    private static void AddExact(SourceIndex index, string source, SourceEntry entry)
    {
        if (!index.Exact.ContainsKey(source))
            index.Exact.Add(source, entry);
    }

    private static bool TryResolveEntry(SourceIndex index, string sourceText,
        out SourceEntry entry, out Dictionary<string, string> templateValues)
    {
        templateValues = null;
        if (index.Exact.TryGetValue(sourceText, out entry))
            return true;

        var normalized = NormalizeHanSpacing(sourceText);
        if (index.Exact.TryGetValue(normalized, out entry))
            return true;

        foreach (var candidate in index.Templates)
        {
            if (TryMatchTemplate(candidate.Source, sourceText, out templateValues) ||
                (!string.Equals(sourceText, normalized, StringComparison.Ordinal) &&
                 TryMatchTemplate(NormalizeHanSpacing(candidate.Source), normalized, out templateValues)))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    private static bool TryMatchTemplate(string template, string displayedValue,
        out Dictionary<string, string> values)
    {
        values = null;
        var tokens = TemplateTokenRegex.Matches(template).Cast<Match>().ToArray();
        if (tokens.Length == 0)
            return false;

        var pattern = new StringBuilder("^");
        var position = 0;
        foreach (var token in tokens)
        {
            pattern.Append(Regex.Escape(template.Substring(position, token.Index - position)));
            pattern.Append("(.+?)");
            position = token.Index + token.Length;
        }
        pattern.Append(Regex.Escape(template.Substring(position))).Append('$');

        var match = Regex.Match(displayedValue, pattern.ToString(), RegexOptions.Singleline);
        if (!match.Success)
            return false;

        values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < tokens.Length; index++)
            values[tokens[index].Value] = match.Groups[index + 1].Value;
        return true;
    }

    private static async Task<string> ApplyTemplateValuesAsync(string localized,
        Dictionary<string, string> values, string tableName, Locale locale, int depth)
    {
        if (values == null)
            return localized;
        foreach (var pair in values)
        {
            // Status templates contain other UI sources, such as part names and finish labels.
            // Bound recursion so a broadly authored template cannot recurse indefinitely.
            var value = depth < 8 ? await ResolveAsync(pair.Value, tableName, locale, depth + 1) : pair.Value;
            localized = localized.Replace(pair.Key, value);
        }
        return localized;
    }

    private static string DecodeEscapedBraces(string value)
    {
        return value.Replace("{{", "{").Replace("}}", "}");
    }

    private static string NormalizeHanSpacing(string value)
    {
        // Excel normalizes CRLF to LF. Business source text may still use CRLF from a scene asset.
        return string.IsNullOrEmpty(value) ? value : HanSpacingRegex.Replace(
            value.Replace("\r\n", "\n").Replace("\r", "\n"), string.Empty);
    }
}
