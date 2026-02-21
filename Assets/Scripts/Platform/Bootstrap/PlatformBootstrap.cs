using OriginalB.Platform.Core;
using OriginalB.Platform.Diagnostics;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;
using OriginalB.Platform.Services.Douyin;
using OriginalB.Platform.Services.WeChat;
using UnityEngine;

namespace OriginalB.Platform.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public class PlatformBootstrap : MonoBehaviour
    {
        private static bool initialized;
        private static GameObject bootstrapObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (initialized)
            {
                return;
            }

            bootstrapObject = new GameObject("PlatformBootstrap");
            DontDestroyOnLoad(bootstrapObject);
            bootstrapObject.AddComponent<PlatformBootstrap>();
        }

        private void Awake()
        {
            if (initialized)
            {
                Destroy(gameObject);
                return;
            }

            initialized = true;
            RegisterServicesByPlatform();
        }

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            if (ServiceLocator.TryResolve<ILifecycleService>(out var lifecycleService))
            {
                lifecycleService.Shutdown();
            }

            ServiceLocator.Clear();
            initialized = false;
            bootstrapObject = null;
        }

        private static void RegisterServicesByPlatform()
        {
            RegisterPlatformSpecificCoreServices();
            ServiceLocator.Register<IAssetService>(new CommonAssetService());

            var lifecycleService = new CommonLifecycleService();
            ServiceLocator.Register<ILifecycleService>(lifecycleService);
            lifecycleService.Initialize();

            if (ServiceLocator.TryResolve<IAuthService>(out var authService))
            {
                authService.Initialize();
            }

            if (ServiceLocator.TryResolve<IAdService>(out var adService))
            {
                adService.Initialize();
            }

            PlatformDiagnosticsReporter.ReportStartup();
        }

        private static void RegisterPlatformSpecificCoreServices()
        {
#if PLATFORM_WECHAT
            ServiceLocator.Register<IPlatformContext>(new WeChatPlatformContext());
            ServiceLocator.Register<IInputService>(new WeChatInputService());
            ServiceLocator.Register<ILogService>(new WeChatLogService());
            ServiceLocator.Register<IStorageService>(new WeChatStorageService());
        ServiceLocator.Register<IAuthService>(new WeChatAuthService());
        ServiceLocator.Register<IAdService>(new WeChatAdService());
        ServiceLocator.Register<IShareService>(new WeChatShareService());
        ServiceLocator.Register<IAnalyticsService>(new WeChatAnalyticsService());
#elif PLATFORM_DOUYIN
            ServiceLocator.Register<IPlatformContext>(new DouyinPlatformContext());
            ServiceLocator.Register<IInputService>(new DouyinInputService());
            ServiceLocator.Register<ILogService>(new DouyinLogService());
            ServiceLocator.Register<IStorageService>(new DouyinStorageService());
        ServiceLocator.Register<IAuthService>(new DouyinAuthService());
        ServiceLocator.Register<IAdService>(new DouyinAdService());
        ServiceLocator.Register<IShareService>(new DouyinShareService());
        ServiceLocator.Register<IAnalyticsService>(new DouyinAnalyticsService());
#else
            ServiceLocator.Register<IPlatformContext>(new CommonPlatformContext());
            ServiceLocator.Register<IInputService>(new CommonInputService());
            ServiceLocator.Register<ILogService>(new CommonLogService());
            ServiceLocator.Register<IStorageService>(new CommonStorageService());
            ServiceLocator.Register<IAuthService>(new CommonAuthService());
            ServiceLocator.Register<IAdService>(new CommonAdService());
            ServiceLocator.Register<IShareService>(new CommonShareService());
            ServiceLocator.Register<IAnalyticsService>(new CommonAnalyticsService());
#endif
        }
    }
}
