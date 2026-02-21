#if UNITY_EDITOR
using OriginalB.Platform.Core;
using UnityEditor;
using UnityEngine;

namespace OriginalB.Platform.Diagnostics.Editor
{
    public static class PlatformDiagnosticsMenu
    {
        [MenuItem("Tools/OriginalB/Platform/Report Startup Diagnostics")]
        private static void ReportStartupDiagnostics()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PlatformDiag] 当前不在 Play 模式，ServiceLocator 可能未初始化。建议进入 Play 后执行该命令。", null);
            }

            PlatformDiagnosticsReporter.ReportStartup();
        }

        [MenuItem("Tools/OriginalB/Platform/Clear Service Locator")]
        private static void ClearServiceLocator()
        {
            ServiceLocator.Clear();
            Debug.Log("[PlatformDiag] ServiceLocator 已清空。", null);
        }
    }
}
#endif
