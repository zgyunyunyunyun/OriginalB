using System;
using System.Reflection;
using OriginalB.Platform.Services.Common;

namespace OriginalB.Platform.Services.WeChat
{
    public class WeChatStorageService : CommonStorageService
    {
        private static readonly Type WxBaseType = ResolveWxBaseType();
        private static readonly MethodInfo StorageGetIntSyncMethod = ResolveMethod("StorageGetIntSync", typeof(string), typeof(int));
        private static readonly MethodInfo StorageSetIntSyncMethod = ResolveMethod("StorageSetIntSync", typeof(string), typeof(int));
        private static readonly MethodInfo StorageGetStringSyncMethod = ResolveMethod("StorageGetStringSync", typeof(string), typeof(string));
        private static readonly MethodInfo StorageSetStringSyncMethod = ResolveMethod("StorageSetStringSync", typeof(string), typeof(string));
        private static readonly MethodInfo StorageHasKeySyncMethod = ResolveMethod("StorageHasKeySync", typeof(string));

        public override int GetInt(string key, int defaultValue = 0)
        {
            if (TryInvoke(StorageGetIntSyncMethod, out var result, key, defaultValue) && result is int intValue)
            {
                return intValue;
            }

            return base.GetInt(key, defaultValue);
        }

        public override void SetInt(string key, int value)
        {
            if (TryInvoke(StorageSetIntSyncMethod, out _, key, value))
            {
                return;
            }

            base.SetInt(key, value);
        }

        public override string GetString(string key, string defaultValue = "")
        {
            if (TryInvoke(StorageGetStringSyncMethod, out var result, key, defaultValue) && result is string stringValue)
            {
                return stringValue;
            }

            return base.GetString(key, defaultValue);
        }

        public override void SetString(string key, string value)
        {
            if (TryInvoke(StorageSetStringSyncMethod, out _, key, value ?? string.Empty))
            {
                return;
            }

            base.SetString(key, value);
        }

        public override bool HasKey(string key)
        {
            if (TryInvoke(StorageHasKeySyncMethod, out var result, key) && result is bool boolValue)
            {
                return boolValue;
            }

            return base.HasKey(key);
        }

        public override void Save()
        {
            // WeChat sync storage writes immediately. Keep base save as fallback.
            base.Save();
        }

        private static Type ResolveWxBaseType()
        {
            var exact = Type.GetType("WeChatWASM.WXBase, WeChatWASM", false);
            if (exact != null)
            {
                return exact;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var candidate = loadedAssemblies[i].GetType("WeChatWASM.WXBase", false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MethodInfo ResolveMethod(string methodName, params Type[] parameters)
        {
            if (WxBaseType == null)
            {
                return null;
            }

            return WxBaseType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
        }

        private static bool TryInvoke(MethodInfo method, out object result, params object[] args)
        {
            result = null;
            if (method == null)
            {
                return false;
            }

            try
            {
                result = method.Invoke(null, args);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
