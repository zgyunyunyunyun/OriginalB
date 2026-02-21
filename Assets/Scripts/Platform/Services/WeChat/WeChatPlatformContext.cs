using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.WeChat
{
    public class WeChatPlatformContext : IPlatformContext
    {
        public PlatformType Current => PlatformType.WeChat;

        public string PlatformVersion => Application.unityVersion;

        public bool IsDevEnvironment => Debug.isDebugBuild || Application.isEditor;

        public bool SupportsFeature(FeatureFlag featureFlag)
        {
            switch (featureFlag)
            {
                case FeatureFlag.TouchInput:
                case FeatureFlag.LifecycleEvents:
                case FeatureFlag.NetworkStatus:
                case FeatureFlag.Share:
                case FeatureFlag.Login:
                case FeatureFlag.RewardedAd:
                case FeatureFlag.InterstitialAd:
                    return true;
                default:
                    return false;
            }
        }
    }
}
