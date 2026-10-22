# Changelog

## 1.0.1 - 2026-10-22

- 修复直接从 UPM Git URL 安装时 uGUI 1.0 项目被强制要求 2.0 的依赖冲突。
- Nodin 继续保留，由包内独立引导通过 Git 安装，不再请求 Unity 注册表中的不存在版本。
- uGUI 1.x 缺少独立 TMP 时自动补齐；uGUI 2.x 使用自带 TMP。
- 用程序集版本条件隔离首次安装时尚不可用的依赖；提供网络失败后的依赖修复菜单。
- 增加 Unity 2022.3 的对象查找 API 分支。

## 1.0.0 - 2026-10-22

- 从 UnityFramework 提取运行时本地化组件和编辑器工具，保留程序集名与现有脚本 GUID。
- LocalizeControl 随包提供，不再继承框架核心单例，保留原有访问接口。
- 保留 Nodin 依赖，明确声明 Unity Localization、uGUI 和 Newtonsoft JSON 依赖。
- 用可选文本格式化接口衔接输入模块，包本身不依赖手柄图标或具体输入框架。
- CarWebsite 语言资源、字体与数据库适配仍由项目管理。
