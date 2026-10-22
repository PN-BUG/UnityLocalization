using System;
using System.Collections.Generic;
using Nodin;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceNamespace: "", sourceAssembly: "UnityFramework", sourceClassName: "LocalizeControl")]
public class LocalizeControl : MonoBehaviour
{
    private static LocalizeControl instance;
    private static bool isQuitting;

    // Preserve the existing API without depending on the framework's singleton assembly.
    public static LocalizeControl Ins
    {
        get
        {
            if (isQuitting) return null;
            if (instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                instance = FindAnyObjectByType<LocalizeControl>();
#else
                instance = FindObjectOfType<LocalizeControl>();
#endif
                if (instance == null)
                    instance = new GameObject("[Singleton] LocalizeControl").AddComponent<LocalizeControl>();
            }
            return instance;
        }
    }

    public static LocalizeControl InsOrNull => instance;
    public static bool Exists => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        isQuitting = false;
    }

    protected virtual void OnApplicationQuit() => isQuitting = true;

    protected virtual void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    [BoxGroup("Debug参数")][LabelText("是否初始化")] [ShowInInspector] [ReadOnly] bool isInit = false;
    [BoxGroup("Debug参数")][LabelText("Locale字典")] [ShowInInspector] [ReadOnly]Dictionary<SystemLanguage, Locale> localeDic ;
    [BoxGroup("Debug参数")][LabelText("延迟执行的方法")] [ShowInInspector] [ReadOnly] List<Action> delayExecutionMethod = new List<Action>();
    
    
    [BoxGroup("参数")][LabelText("当没有系统语言时默认返回的")] [ShowInInspector]public const SystemLanguage fallbackLanguage =  SystemLanguage.English;
    
    
    #region 获取当前系统语言
    
    //当前游戏中有的语言
    private static Dictionary<int,SystemLanguage> defaultLanguageDic = new Dictionary<int, SystemLanguage>()
    {
        {0,SystemLanguage.ChineseSimplified},
        {1,SystemLanguage.ChineseTraditional},
        {2,SystemLanguage.English},
        {3,SystemLanguage.Russian},
        {4,SystemLanguage.Spanish},
        {5,SystemLanguage.Arabic},
    };

    public static Dictionary<int, SystemLanguage> systemLanguageDic
    {
        get
        {
            var config = FrameworkLocalizationConfig.Load();
            if (config == null) return defaultLanguageDic;
            var languages = config.GetAvailableLanguages();
            var result = new Dictionary<int, SystemLanguage>();
            for (var i = 0; i < languages.Count; i++) result.Add(i, languages[i]);
            return result;
        }
        set { defaultLanguageDic = value; }
    }

    public static SystemLanguage ResolveAvailableLanguage(SystemLanguage language)
    {
        var config = FrameworkLocalizationConfig.Load();
        return config == null ? language : config.ResolveLanguage(language);
    }
    
    //获取当前系统语言，若没有则返回默认
    public static SystemLanguage GetSystemDefaultLanguage()
    {
        SystemLanguage systemLang = Application.systemLanguage;
        var config = FrameworkLocalizationConfig.Load();
        if (config != null) return config.ResolveLanguage(systemLang);
        foreach (SystemLanguage lang in systemLanguageDic.Values)
        {
            if (lang == systemLang)
            {
                return lang;
            }
        }
        return fallbackLanguage;
    }
    
    #endregion
    
    
    #region async初始化
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        //async加载语言环境
        AsyncInit();
    }
    
    private async void AsyncInit()
    {
        /* 1.确保 Localization 系统完全初始化
         * 它是整个本地化系统的核心初始化操作，加载基础配置（如 Locale 列表、默认语言环境、语言环境切换器等）。
         * 如果 Localization 系统尚未初始化完成，直接使用其他操作可能会导致不可预期的行为。
         */
        var initialization = LocalizationSettings.InitializationOperation;
        await initialization.Task;
        if (initialization.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log("Localization 系统初始化失败：\n" + initialization.OperationException?.Message);
            return;
        }
 
        // 2.确保选定的语言环境初始化 SelectedLocaleAsync 将确保语言环境设置已经初始化，并且已经选择了一个区域设置。
        var m_InitializeOperation = LocalizationSettings.SelectedLocaleAsync;
        await m_InitializeOperation.Task;
        if (initialization.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.Log("语言环境设置已经初始化失败：\n" + initialization.OperationException?.Message);
            return;
        }
        
        // 3.初始化获取所有Locale (语言环境)
        Dictionary<SystemLanguage, Locale> res =  new Dictionary<SystemLanguage, Locale>();
        
        foreach (var language in systemLanguageDic.Values)
        {
            var code = FrameworkLocalizationConfig.GetLocaleCode(language);
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale != null) res[language] = locale;
        }

        localeDic = res;
        
        isInit = true;
        var config = FrameworkLocalizationConfig.Load();
        if (config != null && !res.ContainsValue(LocalizationSettings.SelectedLocale))
            ChangeLanguage(config.ResolveLanguage(Application.systemLanguage));

        //执行剩余方法
        for (int i = 0; i < delayExecutionMethod.Count; i++)
        {
            delayExecutionMethod[i].Invoke();
        }
        delayExecutionMethod.Clear();
    }
    
    #endregion

    
    #region 获取本地化文本 / 更改语言
    //切换系统语言
    public void ChangeLanguage(SystemLanguage language)
    {
        if (isInit == false)
        {
            delayExecutionMethod.Add(() => { ChangeLanguage(language); });
            Debug.LogError("Localization系统还未初始化完毕，先将方法压入堆栈");
            return;
        }
        
        language = ResolveAvailableLanguage(language);
        if (localeDic.ContainsKey(language))
        {
            LocalizationSettings.Instance.SetSelectedLocale(localeDic[language]);
        }
        else
        {
            Debug.LogError($"Language {language} not found");
        }
    }

    //异步获取本地化文本
    public async void GetLocalizeTextSync(string tableName, string entry,Action<string> callback)
    {
        if (isInit == false)
        {
            delayExecutionMethod.Add(() => { GetLocalizeTextSync(tableName, entry, callback); });
            Debug.LogError("Localization系统还未初始化完毕，先将方法压入堆栈");
            return;
        }
        
        // 确保语言表的初始化 GetTableAsync用于异步返回请求的表
        AsyncOperationHandle<StringTable> loadingTable = LocalizationSettings.StringDatabase.GetTableAsync(tableName);
        
        await loadingTable.Task;
        
        if (loadingTable.Status == AsyncOperationStatus.Succeeded)
        {
            var stringTable = loadingTable.Result;
            callback.Invoke(stringTable.GetEntry(entry).GetLocalizedString());
        }
        else
        {
            Debug.LogError("无法加载字符串表\n" + loadingTable.OperationException?.Message);
        }
    }
    
    //获取本地化文本
    public string GetLocalizeText(TableReference tableReference,TableEntryReference tableEntryReference)
    {
        if (isInit == false)
        {
            Debug.LogError("Localization系统还未初始化完毕");
            return "Error";
        }
        
        return LocalizationSettings.StringDatabase.GetTableEntry(tableReference, tableEntryReference).Entry.GetLocalizedString();
    }
    #endregion
}
