using System;
using System.Reflection;
using OriginalB.Platform.Services.Common;

namespace OriginalB.Platform.Services.Douyin
{
    public class DouyinStorageService : CommonStorageService
    {
        private static readonly Type TtStorageType = ResolveType("TTSDK.TTStorage");
        private static readonly MethodInfo SetIntSyncMethod = ResolveMethod("SetIntSync", typeof(string), typeof(int));
        private static readonly MethodInfo GetIntSyncMethod = ResolveMethod("GetIntSync", typeof(string), typeof(int));
        private static readonly MethodInfo SetStringSyncMethod = ResolveMethod("SetStringSync", typeof(string), typeof(string));
        private static readonly MethodInfo GetStringSyncMethod = ResolveMethod("GetStringSync", typeof(string), typeof(string));
        private static readonly MethodInfo HasKeySyncMethod = ResolveMethod("HasKeySync", typeof(string));

        public override int GetInt(string key, int defaultValue = 0)
        {
            if (TryInvoke(GetIntSyncMethod, out var result, key, defaultValue) && result is int intValue)
            {
                return intValue;
            }

            return base.GetInt(key, defaultValue);
        }

        public override void SetInt(string key, int value)
        {
            if (TryInvoke(SetIntSyncMethod, out _, key, value))
            {
                return;
            }

            base.SetInt(key, value);
        }

        public override string GetString(string key, string defaultValue = "")
        {
            if (TryInvoke(GetStringSyncMethod, out var result, key, defaultValue) && result is string stringValue)
            {
                return stringValue;
            }

            return base.GetString(key, defaultValue);
        }

        public override void SetString(string key, string value)
        {
            if (TryInvoke(SetStringSyncMethod, out _, key, value ?? string.Empty))
            {
                return;
            }

            base.SetString(key, value);
        }

        public override bool HasKey(string key)
        {
            if (TryInvoke(HasKeySyncMethod, out var result, key) && result is bool boolValue)
            {
                return boolValue;
            }

            return base.HasKey(key);
        }

        public override void Save()
        {
            // TTStorage sync APIs are immediate. Keep common save as editor fallback.
            base.Save();
        }

        private static Type ResolveType(string fullName)
        {
            var exact = Type.GetType(fullName, false);
            if (exact != null)
            {
                return exact;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var candidate = loadedAssemblies[i].GetType(fullName, false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MethodInfo ResolveMethod(string methodName, params Type[] parameters)
        {
            if (TtStorageType == null)
            {
                return null;
            }

            return TtStorageType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
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
