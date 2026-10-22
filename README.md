# ZKO Localization

可独立安装的 Unity UPM 本地化包，最低目标版本为 Unity 2023.2。包名 `com.zko.localization`，版本 `1.0.0`。

## 功能与依赖

包含语言配置、`LocalizeControl`、动态文本接收与原文解析、TMP 字间距、语言对象切换、图片状态切换，以及本地化扫描、翻译流水线、文本导入和 Inspector 工具。

| 必需依赖 | 版本 | 用途 |
| --- | --- | --- |
| `com.unity.localization` | 1.5.3 | Locale、String/Asset Tables、Addressables 资源加载 |
| `com.unity.ugui` | 2.0.0 | uGUI 和此版本 Unity 中的 TextMeshPro |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 | 编辑器翻译服务的 JSON 读写 |
| `com.zko.nodin` | 1.8.0 | Inspector 标签、调试信息与按钮 |

UnityFramework 核心、ToolsHub、Rewired、JoystickIcons 和项目业务代码均不是本包依赖。Nodin 是必需依赖；若目标项目没有可解析它的包源，先安装 [Nodin](https://github.com/PN-BUG/Nodin)，再安装本包。

## 安装

公开仓库：[PN-BUG/UnityLocalization](https://github.com/PN-BUG/UnityLocalization)。

在 Unity Package Manager 中选择 **Add package from git URL**，输入：

```text
https://github.com/PN-BUG/UnityLocalization.git
```

需要固定版本时使用 `https://github.com/PN-BUG/UnityLocalization.git#v1.0.0`。使用 UnityToolsHub 时，也可从「可选模块 > 本地化模块」显式安装；不会自动安装。

本地目录和压缩包安装方式：

1. 先安装 Nodin 1.8.0 或兼容版本。
2. 在 Package Manager 中选择 **Add package from disk**，选中本目录的 `package.json`；或选择 **Add package from tarball**，选中交付的 `com.zko.localization-1.0.0.tgz`。
3. 在 Unity Localization 中创建项目自己的 Localization Settings、Locales 和字符串表，并完成 Addressables 配置。
4. 需要统一语言策略时，通过 `Project Settings > UnityFramework > Localization` 创建配置。配置仍保存在项目的 `Assets/Resources/UnityFrameworkLocalizationConfig.asset`。

迁移已有项目时，应先移除旧的 LocalizationTools 和 LocalizeControl 脚本，再安装此包；保留原 `.meta`，避免重复类或重复 GUID。包内保留原有程序集名、组件名、字段名和脚本 GUID；`LocalizeControl` 的程序集由 `UnityFramework` 改为 `UnityFramework.Localization`，并带有 `MovedFrom` 标记。

## 项目资源

语言表、Localization Settings、字体、场景和数据库连接配置由使用本包的项目持有。

现有工具默认使用 `ProjectText` 字符串表、`Assets/Localization/ProjectText` 和简体中文原文。`LocalizedSourceTextResolver` 可在调用时指定其他表；流水线窗口支持配置输出目录。三语言 JSON 导入器 `ProjectTextLocalizationImporter` 保留原协议：读取项目根目录的 `.codex_spreadsheet_work/localization-import.json` 与 `.request`，不会自动导入随包数据。

## 可选输入显示适配

手柄识别、输入方式切换、按钮 Sprite 和按键文本替换属于输入模块。本包只提供 `LocalizedTextPresentation`：

```csharp
LocalizedTextPresentation.Formatter = (tmpTarget, translatedText, color) => translatedText;
// 输入方式变化后，要求已启用的文本重新格式化。
LocalizedTextPresentation.NotifyChanged();
```

`tmpTarget == null` 表示旧版 `UnityEngine.UI.Text`；`color` 是可选颜色字符串。没有适配器时直接显示翻译结果。适配器应在 `BeforeSceneLoad` 注册；本包会在 `SubsystemRegistration` 清理静态状态，以支持关闭 Domain Reload 的使用方式。

配合 UnityFramework 时，输入适配器位于 `Assets/UnityFramework/Runtime/Module/InputSystem/LocalizationBridge`，程序集为 `UnityFramework.Localization.Input`，依赖本包与 `UnityFramework.JoystickIcons`。它在启动时注册，沿用 `PlatformStringReplace`、输入事件与 `Resources/PlatformStringReplaceDatabase.asset`。仅需本地化时不必安装此适配器；需要手柄文本图标时，再随输入模块接入。

## 验证范围

已静态核对包结构、程序集依赖和迁移前后的脚本/程序集 `.meta`。尚未完成 Unity 包解析、Editor 导入及脚本编译验证；静态检查不能替代这些验证。

UPM 目录和包清单依据 Unity 2023.2 官方文档：[创建自定义包](https://docs.unity3d.com/2023.2/Documentation/Manual/CustomPackages.html)、[包清单](https://docs.unity3d.com/2023.2/Documentation/Manual/upm-manifestPkg.html)。
