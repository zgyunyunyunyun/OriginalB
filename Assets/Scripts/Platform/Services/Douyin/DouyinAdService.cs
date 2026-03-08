using System;
using System.Reflection;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;

namespace OriginalB.Platform.Services.Douyin
{
    public class DouyinAdService : CommonAdService, IAdService
    {
        private Type ttType;

        void IAdService.Initialize() => Initialize();
        bool IAdService.IsRewardedReady() => IsRewardedReady();
        void IAdService.ShowRewarded(Action<bool> onCompleted) => ShowRewarded(onCompleted);
        bool IAdService.IsInterstitialReady() => IsInterstitialReady();
        void IAdService.ShowInterstitial() => ShowInterstitial();

        public new void Initialize()
        {
            ttType = ResolveType("TTSDK.TT");
        }

        public new bool IsRewardedReady()
        {
            return ttType != null && !string.IsNullOrWhiteSpace(PlatformRuntimeConfig.DouyinRewardedAdUnitId);
        }

        public new void ShowRewarded(Action<bool> onCompleted)
        {
            if (!IsRewardedReady())
            {
                onCompleted?.Invoke(false);
                return;
            }

            try
            {
                var method = ttType.GetMethod(
                    "CreateRewardedVideoAd",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(Action<bool, int>),
                        typeof(Action<int, string>),
                        typeof(bool),
                        typeof(string[]),
                        typeof(int),
                        typeof(bool)
                    },
                    null);

                if (method == null)
                {
                    onCompleted?.Invoke(false);
                    return;
                }

                var completed = false;
                Action<bool> complete = granted =>
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    onCompleted?.Invoke(granted);
                };

                Action<bool, int> success = (isEnded, _) => complete(isEnded);
                Action<int, string> fail = (_, __) => complete(false);

                method.Invoke(null, new object[]
                {
                    PlatformRuntimeConfig.DouyinRewardedAdUnitId,
                    success,
                    fail,
                    false,
                    null,
                    0,
                    false
                });
            }
            catch
            {
                onCompleted?.Invoke(false);
            }
        }

        public new bool IsInterstitialReady()
        {
            return ttType != null && !string.IsNullOrWhiteSpace(PlatformRuntimeConfig.DouyinInterstitialAdUnitId);
        }

        public new void ShowInterstitial()
        {
            if (!IsInterstitialReady())
            {
                return;
            }

            try
            {
                var method = ttType.GetMethod(
                    "CreateInterstitialAd",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Action<int, string>), typeof(Action), typeof(Action) },
                    null);
                if (method == null)
                {
                    return;
                }

                Action<int, string> fail = (_, __) => { };
                Action success = () => { };
                Action complete = () => { };
                method.Invoke(null, new object[] { PlatformRuntimeConfig.DouyinInterstitialAdUnitId, fail, success, complete });
            }
            catch
            {
                // Keep silent to avoid interrupting gameplay flow.
            }
        }

        private static Type ResolveType(string fullName)
        {
            var type = Type.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
