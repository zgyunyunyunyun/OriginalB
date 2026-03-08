using OriginalB.Platform.Core;
using OriginalB.Platform.Diagnostics;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;
using UnityEngine;

namespace OriginalB.Platform.Bootstrap
{
    internal static class ChannelSdkSwitch
    {
#if PLATFORM_WECHAT || PLATFORM_DOUYIN || ENABLE_CHANNEL_SDK
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif
    }

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
            if (PlatformRuntimeConfig.TryGetForcedPlatform(out var forcedPlatform))
            {
                PlatformServiceRegistrar.RegisterCoreServices(forcedPlatform);
                return;
            }

            if (!ChannelSdkSwitch.Enabled)
            {
                PlatformServiceRegistrar.RegisterCoreServices(PlatformType.Common);
                return;
            }

#if PLATFORM_WECHAT
            PlatformServiceRegistrar.RegisterCoreServices(PlatformType.WeChat);
#elif PLATFORM_DOUYIN
            PlatformServiceRegistrar.RegisterCoreServices(PlatformType.Douyin);
#else
            PlatformServiceRegistrar.RegisterCoreServices(PlatformType.Common);
#endif
        }
    }
}
