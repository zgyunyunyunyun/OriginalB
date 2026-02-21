using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.Common
{
    public class CommonPlatformContext : IPlatformContext
    {
        public PlatformType Current
        {
            get
            {
#if PLATFORM_WECHAT
                return PlatformType.WeChat;
#elif PLATFORM_DOUYIN
                return PlatformType.Douyin;
#else
                return PlatformType.Common;
#endif
            }
        }

        public string PlatformVersion => Application.unityVersion;

        public bool IsDevEnvironment => Debug.isDebugBuild || Application.isEditor;

        public bool SupportsFeature(FeatureFlag featureFlag)
        {
            switch (featureFlag)
            {
                case FeatureFlag.TouchInput:
                case FeatureFlag.LifecycleEvents:
                    return true;
                default:
                    return false;
            }
        }
    }
}
