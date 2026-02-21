using System.Collections.Generic;

namespace OriginalB.Platform.Interfaces
{
    public interface IAnalyticsService
    {
        void TrackEvent(string eventName, Dictionary<string, object> parameters = null);
    }
}
