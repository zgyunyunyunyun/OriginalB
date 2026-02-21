using System;
using OriginalB.Platform.Interfaces;

namespace OriginalB.Platform.Services.Common
{
    public class CommonAdService : IAdService
    {
        public void Initialize()
        {
        }

        public bool IsRewardedReady()
        {
            return false;
        }

        public void ShowRewarded(Action<bool> onCompleted)
        {
            onCompleted?.Invoke(false);
        }

        public bool IsInterstitialReady()
        {
            return false;
        }

        public void ShowInterstitial()
        {
        }
    }
}
