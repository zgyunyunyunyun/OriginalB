using System;
using System.Collections.Generic;
using System.Reflection;
using OriginalB.Platform.Interfaces;

namespace OriginalB.Platform.Services.Common
{
    public class CommonStorageService : IStorageService
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, string> MemoryStore = new Dictionary<string, string>();

        private static readonly Type PlayerPrefsType = ResolvePlayerPrefsType();
        private static readonly MethodInfo GetIntMethod = ResolveMethod("GetInt", typeof(string), typeof(int));
        private static readonly MethodInfo SetIntMethod = ResolveMethod("SetInt", typeof(string), typeof(int));
        private static readonly MethodInfo GetStringMethod = ResolveMethod("GetString", typeof(string), typeof(string));
        private static readonly MethodInfo SetStringMethod = ResolveMethod("SetString", typeof(string), typeof(string));
        private static readonly MethodInfo HasKeyMethod = ResolveMethod("HasKey", typeof(string));
        private static readonly MethodInfo SaveMethod = ResolveMethod("Save");

        public int GetInt(string key, int defaultValue = 0)
        {
            if (TryInvoke(GetIntMethod, out var result, key, defaultValue) && result is int intValue)
            {
                return intValue;
            }

            lock (Sync)
            {
                if (MemoryStore.TryGetValue(key, out var text) && int.TryParse(text, out var parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public void SetInt(string key, int value)
        {
            if (TryInvoke(SetIntMethod, out _, key, value))
            {
                return;
            }

            lock (Sync)
            {
                MemoryStore[key] = value.ToString();
            }
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (TryInvoke(GetStringMethod, out var result, key, defaultValue) && result is string stringValue)
            {
                return stringValue;
            }

            lock (Sync)
            {
                if (MemoryStore.TryGetValue(key, out var text))
                {
                    return text;
                }
            }

            return defaultValue;
        }

        public void SetString(string key, string value)
        {
            if (TryInvoke(SetStringMethod, out _, key, value))
            {
                return;
            }

            lock (Sync)
            {
                MemoryStore[key] = value ?? string.Empty;
            }
        }

        public bool HasKey(string key)
        {
            if (TryInvoke(HasKeyMethod, out var result, key) && result is bool boolValue)
            {
                return boolValue;
            }

            lock (Sync)
            {
                return MemoryStore.ContainsKey(key);
            }
        }

        public void Save()
        {
            TryInvoke(SaveMethod, out _);
        }

        private static Type ResolvePlayerPrefsType()
        {
            var exact = Type.GetType("UnityEngine.PlayerPrefs, UnityEngine.CoreModule", false);
            if (exact != null)
            {
                return exact;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var candidate = loadedAssemblies[i].GetType("UnityEngine.PlayerPrefs", false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MethodInfo ResolveMethod(string methodName, params Type[] parameters)
        {
            if (PlayerPrefsType == null)
            {
                return null;
            }

            return PlayerPrefsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
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
