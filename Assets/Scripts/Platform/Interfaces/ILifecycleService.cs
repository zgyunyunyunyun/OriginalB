using System;

namespace OriginalB.Platform.Interfaces
{
    public enum NetworkState
    {
        Unknown = 0,
        NotReachable = 1,
        Carrier = 2,
        Wifi = 3
    }

    public interface ILifecycleService
    {
        event Action<bool> PauseChanged;
        event Action FocusRegained;
        event Action LowMemory;
        event Action<NetworkState> NetworkStateChanged;

        void Initialize();
        void Shutdown();
    }
}
