using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>Optional shared language policy for the framework and localization authoring tools.</summary>
[CreateAssetMenu(menuName = "UnityFramework/Localization/Language Config", fileName = ResourceName)]
public sealed class FrameworkLocalizationConfig : ScriptableObject
{
    public const string ResourceName = "UnityFrameworkLocalizationConfig";

    [Tooltip("可用语言，列表顺序也是语言菜单的顺序。重复或不支持的项会被忽略。")]
    [SerializeField] private List<SystemLanguage> availableLanguages = new List<SystemLanguage>
    {
        SystemLanguage.ChineseSimplified, SystemLanguage.ChineseTraditional, SystemLanguage.English,
        SystemLanguage.Russian, SystemLanguage.Spanish, SystemLanguage.Arabic
    };
    [Tooltip("系统语言或存档语言不可用时使用；未选入列表时使用列表第一项。")]
    [SerializeField] private SystemLanguage fallbackLanguage = SystemLanguage.English;

    // A single named Resources asset makes the same optional policy available before scene Awake,
    // to standalone editor tools, and to player builds without a scene-object lookup.
    public static FrameworkLocalizationConfig Load() => Resources.Load<FrameworkLocalizationConfig>(ResourceName);

    public List<SystemLanguage> GetAvailableLanguages()
    {
        var result = new List<SystemLanguage>();
        var codes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (availableLanguages != null)
            foreach (var language in availableLanguages)
            {
                var code = GetLocaleCode(language);
                if (!string.IsNullOrEmpty(code) && codes.Add(code)) result.Add(language);
            }
        // An empty list cannot leave startup without any language.
        if (result.Count == 0)
            result.Add(string.IsNullOrEmpty(GetLocaleCode(fallbackLanguage)) ? SystemLanguage.English : fallbackLanguage);
        return result;
    }

    public SystemLanguage ResolveLanguage(SystemLanguage requested)
    {
        var languages = GetAvailableLanguages();
        foreach (var language in languages)
            if (GetLocaleCode(language) == GetLocaleCode(requested)) return language;
        foreach (var language in languages)
            if (GetLocaleCode(language) == GetLocaleCode(fallbackLanguage)) return language;
        return languages[0];
    }

    public static string GetLocaleCode(SystemLanguage language)
    {
        // Preserve the framework's existing Chinese table identifiers.
        if (language == SystemLanguage.ChineseSimplified || language == SystemLanguage.Chinese) return "zh-Hans";
        if (language == SystemLanguage.ChineseTraditional) return "zh-TW";
        return new LocaleIdentifier(language).Code;
    }

    public static string GetDisplayName(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.ChineseSimplified: case SystemLanguage.Chinese: return "简体中文";
            case SystemLanguage.ChineseTraditional: return "繁體中文";
            case SystemLanguage.English: return "English";
            case SystemLanguage.Russian: return "Русский";
            case SystemLanguage.Spanish: return "Español";
            case SystemLanguage.Arabic: return "العربية";
            default: return new LocaleIdentifier(GetLocaleCode(language)).CultureInfo?.NativeName ?? language.ToString();
        }
    }
}
