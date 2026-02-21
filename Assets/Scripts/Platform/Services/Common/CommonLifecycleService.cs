using System;
using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.Common
{
    public class CommonLifecycleService : ILifecycleService
    {
        public event Action<bool> PauseChanged;
        public event Action FocusRegained;
        public event Action LowMemory;
        public event Action<NetworkState> NetworkStateChanged;

        private CommonLifecycleDriver driver;
        private NetworkState lastNetworkState = NetworkState.Unknown;

        public void Initialize()
        {
            if (driver != null)
            {
                return;
            }

            var host = new GameObject("CommonLifecycleDriver");
            UnityEngine.Object.DontDestroyOnLoad(host);
            driver = host.AddComponent<CommonLifecycleDriver>();
            driver.Initialize(this);
            EmitNetworkIfChanged();
        }

        public void Shutdown()
        {
            if (driver == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(driver.gameObject);
            driver = null;
        }

        internal void NotifyPauseChanged(bool paused)
        {
            PauseChanged?.Invoke(paused);
        }

        internal void NotifyFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                FocusRegained?.Invoke();
            }
        }

        internal void NotifyLowMemory()
        {
            LowMemory?.Invoke();
        }

        internal void EmitNetworkIfChanged()
        {
            var current = ConvertState(Application.internetReachability);
            if (current == lastNetworkState)
            {
                return;
            }

            lastNetworkState = current;
            NetworkStateChanged?.Invoke(current);
        }

        private static NetworkState ConvertState(NetworkReachability reachability)
        {
            switch (reachability)
            {
                case NetworkReachability.NotReachable:
                    return NetworkState.NotReachable;
                case NetworkReachability.ReachableViaCarrierDataNetwork:
                    return NetworkState.Carrier;
                case NetworkReachability.ReachableViaLocalAreaNetwork:
                    return NetworkState.Wifi;
                default:
                    return NetworkState.Unknown;
            }
        }
    }

    public class CommonLifecycleDriver : MonoBehaviour
    {
        private CommonLifecycleService owner;
        private float nextNetworkCheckTime;

        public void Initialize(CommonLifecycleService lifecycleService)
        {
            owner = lifecycleService;
            nextNetworkCheckTime = Time.unscaledTime + 1f;
        }

        private void Update()
        {
            if (owner == null)
            {
                return;
            }

            if (Time.unscaledTime < nextNetworkCheckTime)
            {
                return;
            }

            nextNetworkCheckTime = Time.unscaledTime + 1f;
            owner.EmitNetworkIfChanged();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            owner?.NotifyPauseChanged(pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            owner?.NotifyFocusChanged(hasFocus);
        }

        private void OnApplicationLowMemory()
        {
            owner?.NotifyLowMemory();
        }
    }
}
