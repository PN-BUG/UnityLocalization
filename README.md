# ZKO Localization

可直接通过 Git URL 安装的 Unity UPM 本地化包，最低目标版本为 Unity 2022.3。包名 `com.zko.localization`，版本 `1.0.1`。

## 功能与依赖

包含语言配置、`LocalizeControl`、动态文本接收与原文解析、TMP 字间距、语言对象切换、图片状态切换，以及本地化扫描、翻译流水线、文本导入和 Inspector 工具。

| 必需依赖 | 版本 | 用途 |
| --- | --- | --- |
| `com.unity.localization` | 1.5.3 | Locale、String/Asset Tables、Addressables 资源加载 |
| `com.unity.ugui` | 1.0.0 起 | 接受项目现有 uGUI 1.x / 2.x，不强制升级到 2.0 |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 | 编辑器翻译服务的 JSON 读写 |
| `com.zko.nodin` | 1.8.0 起 | Inspector 标签、调试信息与按钮；包内引导从 Git 补齐 |
| TextMeshPro | 3.x 或 uGUI 2.x 自带 | uGUI 1.x 项目缺少 TMP 时，引导安装 `com.unity.textmeshpro@3.0.6` |

UnityFramework 核心、ToolsHub、Rewired、JoystickIcons 和项目业务代码均不是本包依赖。无需先安装 ToolsHub 或 Nodin。

Nodin 继续保留为实际依赖，但不再作为 `package.json` 中的注册表版本请求。UnityLocalization 自带独立的 Editor 引导程序集，在包完成解析后通过 Package Manager 从 `https://github.com/PN-BUG/Nodin.git` 补齐 Nodin；旧版 uGUI 项目缺少 TMP 时在同一次请求中补齐 TMP。主运行时与工具程序集在这些依赖可用后才启用，首次导入引导自身不引用 Nodin、TMP 或本地化主程序集。

已满足版本的依赖会保留；版本过旧的本地/嵌入依赖不会被 Git 包覆盖，会提示先更新对应源码。

## 安装

公开仓库：[PN-BUG/UnityLocalization](https://github.com/PN-BUG/UnityLocalization)。

在 Unity Package Manager 中选择 **Add package from git URL**，输入：

```text
https://github.com/PN-BUG/UnityLocalization.git
```

需要固定版本，或重试旧版依赖解析失败的安装时，使用：

```text
https://github.com/PN-BUG/UnityLocalization.git#v1.0.1
```

添加此 URL 后等待包内引导完成 Nodin/TMP 安装，再等待 Unity 导入和编译完成即可。整个流程由 UnityLocalization 自己处理，不需要其他工具。若下载失败，可在恢复网络后选择 `Tools > Localization > 修复模块依赖` 重试；自动安装每个 Editor 会话最多尝试一次，避免失败后反复发起请求。

使用 UnityToolsHub 时，也可从「可选模块 > 本地化模块」显式安装本包，依赖仍由本包处理。

本地目录和压缩包安装方式：

1. 在 Package Manager 中选择 **Add package from disk**，选中本目录的 `package.json`；或选择 **Add package from tarball**，选中交付的 `com.zko.localization-1.0.1.tgz`。
2. 等待包内引导自动安装所缺依赖。
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

已静态核对包结构、程序集依赖、uGUI 1.x / 2.x 的程序集启用条件，以及迁移前后的脚本/程序集 `.meta`。

在 Unity 2023.2.22f1、uGUI 2.0、已有 Nodin 1.8.0 的项目中，已通过 MCP 确认包解析为 `1.0.1`、引导程序集导入并加载、脚本重新编译后没有 C# 编译错误。缺少 Nodin/TMP 的首次安装，以及 Unity 2022.3 / uGUI 1.x 环境仍需实际项目验证；这些场景目前只完成了依赖与程序集条件的静态核对。

UPM 目录、包清单及按包版本控制程序集依据 Unity 官方文档：[包清单](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-manifestPkg.html)、[程序集版本条件](https://docs.unity3d.com/2022.3/Documentation/Manual/ScriptCompilationAssemblyDefinitionFiles.html#define-symbols)。
