#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ZKO.Localization.Editor
{
    /// <summary>
    /// Runs independently of Nodin and TMP, so a direct Git installation can resolve first.
    /// Dependent runtime/editor assemblies are enabled by version defines after installation.
    /// </summary>
    [InitializeOnLoad]
    public static class LocalizationDependencySetup
    {
        public const string NodinGitReference = "https://github.com/PN-BUG/Nodin.git";
        public const string LegacyTmpReference = "com.unity.textmeshpro@3.0.6";
        private const string AttemptKey = "ZKO.Localization.Dependencies.Attempted.1.0.1";
        private static AddAndRemoveRequest request;

        static LocalizationDependencySetup() => EditorApplication.update += WaitForEditor;

        private static void WaitForEditor()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitForEditor;
            EnsureDependencies(false);
        }

        [MenuItem("Tools/Localization/修复模块依赖")]
        public static void RepairDependencies() => EnsureDependencies(true);

        /// <summary>Returns missing package identifiers without modifying the project.</summary>
        public static string[] GetMissingPackageIdentifiers()
        {
            return GetMissingPackageIdentifiers(PackageInfo.GetAllRegisteredPackages());
        }

        private static string[] GetMissingPackageIdentifiers(PackageInfo[] packages)
        {
            var result = new List<string>();
            if (!HasVersion(packages, "com.zko.nodin", "1.8.0")) result.Add(NodinGitReference);
            // TMP is a separate package in uGUI 1.x, and included in uGUI 2.x.
            if (!HasVersion(packages, "com.unity.ugui", "2.0.0") &&
                !HasVersion(packages, "com.unity.textmeshpro", "3.0.0"))
                result.Add(LegacyTmpReference);
            return result.ToArray();
        }

        /// <summary>Resolves required packages through UPM. Does not edit manifest.json or replace local source.</summary>
        public static void EnsureDependencies(bool retry)
        {
            if (request != null) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (retry) Debug.LogWarning("[UnityLocalization] 请等待导入、编译和 Play Mode 结束后再修复依赖。");
                return;
            }
            if (!retry && SessionState.GetBool(AttemptKey, false)) return;

            try
            {
                var packages = PackageInfo.GetAllRegisteredPackages();
                var missing = GetMissingPackageIdentifiers(packages);
                if (missing.Length == 0) return;
                // Do not discard a project's editable dependency just to satisfy the minimum version.
                var outdatedLocal = packages.FirstOrDefault(package =>
                    (package.name == "com.zko.nodin" && missing.Contains(NodinGitReference) ||
                     package.name == "com.unity.textmeshpro" && missing.Contains(LegacyTmpReference)) &&
                    (package.source == PackageSource.Local || package.source == PackageSource.Embedded));
                SessionState.SetBool(AttemptKey, true);
                if (outdatedLocal != null)
                {
                    Debug.LogError("[UnityLocalization] 请先更新本地依赖 " + outdatedLocal.name +
                        "（当前 " + outdatedLocal.version + "）。本模块会保留本地源码；更新后选择 Tools > Localization > 修复模块依赖。");
                    return;
                }
                Debug.Log("[UnityLocalization] 正在安装依赖：" + string.Join(", ", missing));
                request = Client.AddAndRemove(missing);
                EditorApplication.update += PollRequest;
            }
            catch (Exception exception)
            {
                request = null;
                SessionState.SetBool(AttemptKey, true);
                Debug.LogError("[UnityLocalization] 依赖安装未能启动：" + exception.Message +
                    "。可通过 Tools > Localization > 修复模块依赖 重试。");
            }
        }

        private static void PollRequest()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= PollRequest;
            var succeeded = request.Status == StatusCode.Success;
            var error = request.Error?.message;
            request = null;
            if (succeeded)
                Debug.Log("[UnityLocalization] 依赖已解析，等待 Unity 导入和编译完成。");
            else
                Debug.LogError("[UnityLocalization] 依赖安装失败：" + error +
                    "。检查网络后选择 Tools > Localization > 修复模块依赖 重试。");
        }

        private static bool HasVersion(PackageInfo[] packages, string name, string minimum)
        {
            var package = packages.FirstOrDefault(item => item.name == name);
            return package != null && VersionAtLeast(package.version, minimum);
        }

        private static bool VersionAtLeast(string value, string minimum)
        {
            var stable = (value ?? "").Split('-', '+')[0];
            return Version.TryParse(stable, out var version) && version >= new Version(minimum);
        }
    }
}
#endif
