using System;
using System.Collections.Generic;

namespace OriginalB.Platform.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T implementation) where T : class
        {
            if (implementation == null)
            {
                throw new ArgumentNullException(nameof(implementation));
            }

            Services[typeof(T)] = implementation;
        }

        public static T Resolve<T>() where T : class
        {
            if (!TryResolve<T>(out var service))
            {
                throw new InvalidOperationException($"Service not registered: {typeof(T).FullName}");
            }

            return service;
        }

        public static bool TryResolve<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var instance))
            {
                service = instance as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class
        {
            return Services.ContainsKey(typeof(T));
        }

        public static void Clear()
        {
            Services.Clear();
        }
    }
}
