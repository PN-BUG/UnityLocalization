using UnityEditor;
using UnityEngine;

internal static class FrameworkLocalizationConfigEditor
{
    private const string AssetPath = "Assets/Resources/UnityFrameworkLocalizationConfig.asset";

    [MenuItem("Tools/Localization/语言配置")]
    public static void SelectOrCreate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请在编辑模式下创建或编辑本地化语言配置。");
            return;
        }
        var config = FrameworkLocalizationConfig.Load();
        if (config == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            config = ScriptableObject.CreateInstance<FrameworkLocalizationConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    [SettingsProvider]
    public static SettingsProvider CreateProvider()
    {
        return new SettingsProvider("Project/UnityFramework/Localization", SettingsScope.Project)
        {
            label = "Localization",
            guiHandler = _ =>
            {
                var config = FrameworkLocalizationConfig.Load();
                EditorGUILayout.HelpBox("配置可用语言及其菜单顺序；运行时与 ToolsHub 翻译共用。没有配置时各自保留默认行为。空列表使用回退语言。", MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (config == null)
                    {
                        if (GUILayout.Button("创建语言配置")) SelectOrCreate();
                        return;
                    }
                    var serialized = new SerializedObject(config);
                    serialized.Update();
                    EditorGUILayout.PropertyField(serialized.FindProperty("availableLanguages"), new GUIContent("可用语言"), true);
                    EditorGUILayout.PropertyField(serialized.FindProperty("fallbackLanguage"), new GUIContent("回退语言"));
                    serialized.ApplyModifiedProperties();
                    if (GUILayout.Button("在 Project 中选择配置")) SelectOrCreate();
                }
            },
            keywords = new[] { "语言", "本地化", "Language", "Localization" }
        };
    }
}
