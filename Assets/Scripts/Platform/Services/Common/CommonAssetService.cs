using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.Common
{
    public class CommonAssetService : IAssetService
    {
        public T Load<T>(string path) where T : Object
        {
            return Resources.Load<T>(path);
        }

        public bool Exists<T>(string path) where T : Object
        {
            return Load<T>(path) != null;
        }

        public void UnloadUnused()
        {
            Resources.UnloadUnusedAssets();
        }
    }
}
