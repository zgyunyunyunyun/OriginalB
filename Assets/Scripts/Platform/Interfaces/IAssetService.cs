using UnityEngine;

namespace OriginalB.Platform.Interfaces
{
    public interface IAssetService
    {
        T Load<T>(string path) where T : Object;
        bool Exists<T>(string path) where T : Object;
        void UnloadUnused();
    }
}
