using System;

namespace OriginalB.Platform.Interfaces
{
    public interface IAdService
    {
        void Initialize();
        bool IsRewardedReady();
        void ShowRewarded(Action<bool> onCompleted);
        bool IsInterstitialReady();
        void ShowInterstitial();
    }
}
