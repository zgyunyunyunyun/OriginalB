using System.Text;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Diagnostics
{
    public static class PlatformDiagnosticsReporter
    {
        public static void ReportStartup()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Platform diagnostics startup report");
            builder.AppendLine($"UnityVersion={Application.unityVersion}");
            builder.AppendLine($"RuntimePlatform={Application.platform}");
            builder.AppendLine($"DebugBuild={Debug.isDebugBuild}");

            if (ServiceLocator.TryResolve<IPlatformContext>(out var platformContext) && platformContext != null)
            {
                builder.AppendLine($"PlatformContext={platformContext.GetType().Name}");
                builder.AppendLine($"CurrentPlatform={platformContext.Current}");
                builder.AppendLine($"PlatformVersion={platformContext.PlatformVersion}");
                builder.AppendLine($"IsDevEnvironment={platformContext.IsDevEnvironment}");
                builder.AppendLine($"Feature.TouchInput={platformContext.SupportsFeature(FeatureFlag.TouchInput)}");
                builder.AppendLine($"Feature.LifecycleEvents={platformContext.SupportsFeature(FeatureFlag.LifecycleEvents)}");
                builder.AppendLine($"Feature.NetworkStatus={platformContext.SupportsFeature(FeatureFlag.NetworkStatus)}");
                builder.AppendLine($"Feature.Share={platformContext.SupportsFeature(FeatureFlag.Share)}");
                builder.AppendLine($"Feature.Login={platformContext.SupportsFeature(FeatureFlag.Login)}");
                builder.AppendLine($"Feature.RewardedAd={platformContext.SupportsFeature(FeatureFlag.RewardedAd)}");
                builder.AppendLine($"Feature.InterstitialAd={platformContext.SupportsFeature(FeatureFlag.InterstitialAd)}");
            }
            else
            {
                builder.AppendLine("PlatformContext=<missing>");
            }

            AppendServiceState<IInputService>(builder, "InputService");
            AppendServiceState<ILogService>(builder, "LogService");
            AppendServiceState<IStorageService>(builder, "StorageService");
            AppendServiceState<IAssetService>(builder, "AssetService");
            AppendServiceState<ILifecycleService>(builder, "LifecycleService");
            AppendServiceState<IAuthService>(builder, "AuthService");
            AppendServiceState<IAdService>(builder, "AdService");
            AppendServiceState<IShareService>(builder, "ShareService");
            AppendServiceState<IAnalyticsService>(builder, "AnalyticsService");

            if (ServiceLocator.TryResolve<IAdService>(out var adService) && adService != null)
            {
                builder.AppendLine($"Ad.RewardedReady={adService.IsRewardedReady()}");
                builder.AppendLine($"Ad.InterstitialReady={adService.IsInterstitialReady()}");
            }

            if (ServiceLocator.TryResolve<ILogService>(out var logService) && logService != null)
            {
                logService.Info("PlatformDiag", builder.ToString().TrimEnd());
                return;
            }

            Debug.Log(builder.ToString().TrimEnd());
        }

        private static void AppendServiceState<T>(StringBuilder builder, string label) where T : class
        {
            if (ServiceLocator.TryResolve<T>(out var service) && service != null)
            {
                builder.AppendLine($"{label}={service.GetType().Name}");
                return;
            }

            builder.AppendLine($"{label}=<missing>");
        }
    }
}
