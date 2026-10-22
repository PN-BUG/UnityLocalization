# Changelog

## 1.0.0 - 2026-10-22

- 从 UnityFramework 提取运行时本地化组件和编辑器工具，保留程序集名与现有脚本 GUID。
- LocalizeControl 随包提供，不再继承框架核心单例，保留原有访问接口。
- 保留 Nodin 依赖，明确声明 Unity Localization、uGUI 和 Newtonsoft JSON 依赖。
- 用可选文本格式化接口衔接输入模块，包本身不依赖手柄图标或具体输入框架。
- CarWebsite 语言资源、字体与数据库适配仍由项目管理。
