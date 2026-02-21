namespace OriginalB.Platform.Interfaces
{
    public enum PlatformType
    {
        Common = 0,
        WeChat = 1,
        Douyin = 2
    }

    public enum FeatureFlag
    {
        TouchInput = 0,
        RewardedAd = 1,
        InterstitialAd = 2,
        Share = 3,
        Login = 4,
        NetworkStatus = 5,
        LifecycleEvents = 6
    }

    public interface IPlatformContext
    {
        PlatformType Current { get; }
        string PlatformVersion { get; }
        bool IsDevEnvironment { get; }
        bool SupportsFeature(FeatureFlag featureFlag);
    }
}
