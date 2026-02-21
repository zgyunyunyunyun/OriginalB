using System.Collections.Generic;
using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.Common
{
    public class CommonAnalyticsService : IAnalyticsService
    {
        public void TrackEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }

            Debug.Log($"[Analytics] {eventName}");
        }
    }
}
