using OriginalB.Platform.Interfaces;

namespace OriginalB.Platform.Core
{
    public enum PlatformServiceMode
    {
        AutoByBuildSymbols = 0,
        ForceCommon = 1,
        ForceWeChat = 2,
        ForceDouyin = 3
    }

    public static class PlatformRuntimeConfig
    {
        public static PlatformServiceMode ServiceMode { get; private set; } = PlatformServiceMode.AutoByBuildSymbols;
        public static string DouyinRewardedAdUnitId { get; private set; } = string.Empty;
        public static string DouyinInterstitialAdUnitId { get; private set; } = string.Empty;
        public static bool DouyinForceLogin { get; private set; } = true;
        public static string DouyinDefaultShareTitle { get; private set; } = string.Empty;
        public static string DouyinDefaultShareImageUrl { get; private set; } = string.Empty;
        public static string DouyinDefaultShareQuery { get; private set; } = string.Empty;

        public static void Apply(
            PlatformServiceMode serviceMode,
            string douyinRewardedAdUnitId,
            string douyinInterstitialAdUnitId,
            bool douyinForceLogin,
            string douyinDefaultShareTitle,
            string douyinDefaultShareImageUrl,
            string douyinDefaultShareQuery)
        {
            ServiceMode = serviceMode;
            DouyinRewardedAdUnitId = douyinRewardedAdUnitId ?? string.Empty;
            DouyinInterstitialAdUnitId = douyinInterstitialAdUnitId ?? string.Empty;
            DouyinForceLogin = douyinForceLogin;
            DouyinDefaultShareTitle = douyinDefaultShareTitle ?? string.Empty;
            DouyinDefaultShareImageUrl = douyinDefaultShareImageUrl ?? string.Empty;
            DouyinDefaultShareQuery = douyinDefaultShareQuery ?? string.Empty;
        }

        public static bool TryGetForcedPlatform(out PlatformType platformType)
        {
            switch (ServiceMode)
            {
                case PlatformServiceMode.ForceCommon:
                    platformType = PlatformType.Common;
                    return true;
                case PlatformServiceMode.ForceWeChat:
                    platformType = PlatformType.WeChat;
                    return true;
                case PlatformServiceMode.ForceDouyin:
                    platformType = PlatformType.Douyin;
                    return true;
                default:
                    platformType = PlatformType.Common;
                    return false;
            }
        }
    }
}
