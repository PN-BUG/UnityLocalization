using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nodin;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Receives a localized template, resolves platform input placeholders, and writes it to a UI text component.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Localization/Dynamic Localization Bridge")]
public sealed class LocalizedTextReceiver : MonoBehaviour
{
    [Serializable]
    private sealed class DynamicBinding
    {
        [LabelText("原文")] public string source;
        [LabelText("本地化文本")] public LocalizedString localized = new LocalizedString();
    }

    [LabelText("旧版文本组件"), SerializeField] private Text legacyText;
    [LabelText("TMP 文本组件"), SerializeField] private TMP_Text tmpText;
    [LabelText("动态文本绑定"), SerializeField] private List<DynamicBinding> dynamicBindings = new List<DynamicBinding>();
    [SerializeField, HideInInspector] private string originalSourceText;

    public string OriginalSourceText => originalSourceText;

#if UNITY_EDITOR
    public static event Action<LocalizedTextReceiver> RefreshFromCurrentTextRequested;

    [Button("按当前文本刷新本地化")]
    private void RefreshFromCurrentTextInInspector()
    {
        if (!Application.isPlaying)
            RefreshFromCurrentTextRequested?.Invoke(this);
    }
#endif

    private DynamicBinding activeDynamicBinding;
    private Dictionary<string, string> activeTemplateValues;
    private string expectedDisplay;
    private string lastObservedSourceText;
    private string latestLocalizedTemplate;
    private string requestedSourceText;
    private string platformReplacementColor;
    private int sourceRevision;
    private int localizationRequestRevision;
    private bool hasRequestedSource;
    private bool isAwaitingLocalizedValue;

    private static readonly Regex TemplateTokenRegex = new Regex(@"\{[^{}]+\}", RegexOptions.Compiled);

    public async void ApplyLocalizedText(string localizedTemplate)
    {
        // Keep the authored source visible and serialized in Edit Mode. Locale previews
        // must not write translated text into scene or prefab TMP components.
        if (!Application.isPlaying) return;

        var sourceVersion = sourceRevision;
        // Startup locale selection and user switching can overlap. Only the newest request may write.
        var requestVersion = ++localizationRequestRevision;
        var template = localizedTemplate ?? string.Empty;
        if (template.StartsWith("No translation found for '", StringComparison.Ordinal))
        {
            template = !string.IsNullOrEmpty(originalSourceText) ? originalSourceText : requestedSourceText;
            if (string.IsNullOrEmpty(template)) return;
        }
        ResolveTargets();
        var resolvedTemplate = await ApplyTemplateValuesAsync(template);
        // An inactive panel still needs its text updated before it becomes visible again.
        if (!Application.isPlaying || this == null || sourceVersion != sourceRevision || requestVersion != localizationRequestRevision)
        {
            return;
        }

        latestLocalizedTemplate = template;
        var value = ApplyPlatformReplacement(resolvedTemplate);
        isAwaitingLocalizedValue = false;
        expectedDisplay = value;
        if (legacyText != null) legacyText.text = value;
        if (tmpText != null) tmpText.text = value;
    }

    /// <summary>
    /// Selects a new business source before localization. This prevents an older asynchronous
    /// localization callback from overwriting a newer input-device-specific prompt.
    /// </summary>
    public void ApplySourceText(string source, string replacementColor = null)
    {
        ResolveTargets();
        requestedSourceText = source ?? string.Empty;
        platformReplacementColor = replacementColor;
        hasRequestedSource = true;
        sourceRevision++;
        latestLocalizedTemplate = string.Empty;
        lastObservedSourceText = requestedSourceText;
        isAwaitingLocalizedValue = true;
        ObserveDynamicSource(requestedSourceText);
        if (activeDynamicBinding == null)
        {
            ApplyLocalizedText(requestedSourceText);
        }
    }

    public void Configure(Text text)
    {
        Configure(text, false);
    }

    public void Configure(Text text, bool replaceOriginalSource)
    {
        legacyText = text;
        tmpText = null;
        CaptureOriginalSource(text != null ? text.text : string.Empty, replaceOriginalSource);
    }

    public void Configure(TMP_Text text)
    {
        Configure(text, false);
    }

    public void Configure(TMP_Text text, bool replaceOriginalSource)
    {
        tmpText = text;
        legacyText = null;
        CaptureOriginalSource(text != null ? text.text : string.Empty, replaceOriginalSource);
    }

    /// <summary>
    /// Replaces the source captured by the receiver. Editor tooling uses this for a nested Prefab
    /// whose displayed Text is overridden without applying that value to the source Prefab.
    /// </summary>
    public void SetOriginalSource(string source)
    {
        if (!string.IsNullOrEmpty(source)) originalSourceText = source;
    }

    public void AddOrUpdateDynamicBinding(string source, string table, string entry)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(table) || string.IsNullOrEmpty(entry)) return;
        var binding = dynamicBindings.Find(item => item.source == source);
        if (binding == null)
        {
            binding = new DynamicBinding { source = source };
            dynamicBindings.Add(binding);
        }
        binding.localized.TableReference = table;
        binding.localized.TableEntryReference = entry;
    }

    private void Reset() => ResolveTargets();
    private void OnValidate() => ResolveTargets();

    private void OnEnable()
    {
        LocalizedTextPresentation.Changed += OnPresentationChanged;
        ResolveTargets();
        // A Prefab instance may serialize a translated display value in TMP_Text. Treating that
        // value as the source here can overwrite a localization callback that already ran during
        // scene activation. The captured source is stable and is the correct startup fallback.
        var source = hasRequestedSource
            ? requestedSourceText
            : !string.IsNullOrEmpty(originalSourceText)
                ? originalSourceText
                : ReadText();
        sourceRevision++;
        CaptureOriginalSource(source);
        lastObservedSourceText = source;
        isAwaitingLocalizedValue = true;
        ObserveDynamicSource(source);
        if (activeDynamicBinding == null)
        {
            ApplyLocalizedText(source);

            // LocalizeStringEvent can be enabled before this receiver and publish its first value
            // immediately from a cached table. Refresh after applying the fallback so the selected
            // locale always gets the final write, independent of component enable order.
            var localizer = GetComponent<LocalizeStringEvent>();
            if (localizer != null && localizer.isActiveAndEnabled)
            {
                localizer.RefreshString();
            }
        }
    }

    private void OnDisable()
    {
        LocalizedTextPresentation.Changed -= OnPresentationChanged;
        ReleaseDynamicBinding();
    }

    private void OnDestroy()
    {
        ReleaseDynamicBinding();
        RestoreOriginalText();
    }

    /// <summary>Refreshes an Excel-updated table value without replacing the current business source.</summary>
    public void RefreshTranslation()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (activeDynamicBinding?.localized != null)
            activeDynamicBinding.localized.RefreshString();
        else
            GetComponent<LocalizeStringEvent>()?.RefreshString();
    }

    private void LateUpdate()
    {
        if (dynamicBindings.Count == 0 || isAwaitingLocalizedValue) return;
        var value = ReadText();
        if (value != expectedDisplay)
        {
            sourceRevision++;
            lastObservedSourceText = value;
            ObserveDynamicSource(value);
        }
    }

    /// <summary>
    /// Restores the value produced by the original business code. Removing this component therefore
    /// returns the object to its pre-localization behaviour without editing that business code.
    /// </summary>
    public void RestoreOriginalText()
    {
        ResolveTargets();
        var value = !string.IsNullOrEmpty(lastObservedSourceText) ? lastObservedSourceText : originalSourceText;
        if (legacyText != null) legacyText.text = value ?? string.Empty;
        if (tmpText != null) tmpText.text = value ?? string.Empty;
        expectedDisplay = value;
    }

    private void ObserveDynamicSource(string source)
    {
        ReleaseDynamicBinding();
        expectedDisplay = source;
        if (string.IsNullOrEmpty(source)) return;
        activeDynamicBinding = dynamicBindings.Find(item => item.source == source);
        if (activeDynamicBinding == null)
        {
            foreach (var binding in dynamicBindings)
            {
                if (!TryMatchTemplate(binding.source, source, out var values)) continue;
                activeDynamicBinding = binding;
                activeTemplateValues = values;
                break;
            }
        }
        if (activeDynamicBinding == null || activeDynamicBinding.localized == null || activeDynamicBinding.localized.IsEmpty)
        {
            activeDynamicBinding = null;
            return;
        }
        activeDynamicBinding.localized.StringChanged += ApplyLocalizedText;
    }

    private void ReleaseDynamicBinding()
    {
        if (activeDynamicBinding != null && activeDynamicBinding.localized != null)
            activeDynamicBinding.localized.StringChanged -= ApplyLocalizedText;
        activeDynamicBinding = null;
        activeTemplateValues = null;
    }

    private void OnPresentationChanged()
    {
        if (isActiveAndEnabled && !string.IsNullOrEmpty(latestLocalizedTemplate))
        {
            ApplyLocalizedText(latestLocalizedTemplate);
        }
    }

    private string ApplyPlatformReplacement(string resolvedTemplate)
    {
        return LocalizedTextPresentation.Format(tmpText, resolvedTemplate, platformReplacementColor);
    }

    private async System.Threading.Tasks.Task<string> ApplyTemplateValuesAsync(string template)
    {
        if (activeTemplateValues == null || activeTemplateValues.Count == 0) return template;
        foreach (var pair in activeTemplateValues)
        {
            // A template value may itself be player-facing source text, such as a
            // LevelDataSO levelName ("关卡 1"). Resolve it independently before
            // inserting it into the localized outer template. Numeric/user data
            // simply falls back to its original value.
            var localizedValue = await LocalizedSourceTextResolver.ResolveAsync(pair.Value);
            template = template.Replace(pair.Key, localizedValue);
        }
        return template;
    }

    private static bool TryMatchTemplate(string template, string displayedValue,
        out Dictionary<string, string> values)
    {
        values = null;
        if (string.IsNullOrEmpty(template) || string.IsNullOrEmpty(displayedValue)) return false;
        var tokens = TemplateTokenRegex.Matches(template).Cast<Match>().ToArray();
        if (tokens.Length == 0) return false;

        var pattern = new StringBuilder("^");
        var position = 0;
        foreach (var token in tokens)
        {
            pattern.Append(Regex.Escape(template.Substring(position, token.Index - position)));
            pattern.Append("(.*?)");
            position = token.Index + token.Length;
        }
        pattern.Append(Regex.Escape(template.Substring(position))).Append('$');

        var match = Regex.Match(displayedValue, pattern.ToString(), RegexOptions.Singleline);
        if (!match.Success) return false;
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < tokens.Length; index++)
            values[tokens[index].Value] = match.Groups[index + 1].Value;
        return true;
    }

    private string ReadText()
    {
        if (tmpText != null) return tmpText.text;
        return legacyText != null ? legacyText.text : string.Empty;
    }

    private void ResolveTargets()
    {
        if (legacyText == null && tmpText == null)
        {
            tmpText = GetComponent<TMP_Text>();
            if (tmpText == null) legacyText = GetComponent<Text>();
        }
    }

    private void CaptureOriginalSource(string value, bool replace = false)
    {
        if ((replace || string.IsNullOrEmpty(originalSourceText)) && !string.IsNullOrEmpty(value))
            originalSourceText = value;
    }

}
