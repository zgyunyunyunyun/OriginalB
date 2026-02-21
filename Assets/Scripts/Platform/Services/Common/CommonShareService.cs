using OriginalB.Platform.Interfaces;

namespace OriginalB.Platform.Services.Common
{
    public class CommonShareService : IShareService
    {
        public void Share(SharePayload payload, System.Action<ShareResult> callback)
        {
            callback?.Invoke(new ShareResult(false, "Share SDK not integrated."));
        }
    }
}
