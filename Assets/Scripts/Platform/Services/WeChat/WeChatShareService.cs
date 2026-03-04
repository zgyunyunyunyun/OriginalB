using System;
using System.Linq;
using System.Reflection;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;

namespace OriginalB.Platform.Services.WeChat
{
    public class WeChatShareService : CommonShareService, IShareService
    {
        public new void Share(SharePayload payload, Action<ShareResult> callback)
        {
            try
            {
                var wxType = ResolveType("WeChatWASM.WX");
                var shareOptionType = ResolveType("WeChatWASM.ShareAppMessageOption");
                if (wxType == null || shareOptionType == null)
                {
                    base.Share(payload, callback);
                    return;
                }

                var option = Activator.CreateInstance(shareOptionType);
                if (option == null)
                {
                    base.Share(payload, callback);
                    return;
                }

                TrySetStringMember(option, "title", payload.Title);
                TrySetStringMember(option, "imageUrl", payload.ImageUrl);
                TrySetStringMember(option, "query", payload.Query);

                var completed = false;
                void Report(bool success, string error)
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    callback?.Invoke(new ShareResult(success, error));
                }

                TrySetCallbackMember(option, "success", _ => Report(true, string.Empty));
                TrySetCallbackMember(option, "fail", res => Report(false, ExtractError(res)));
                TrySetCallbackMember(option, "complete", res =>
                {
                    if (completed)
                    {
                        return;
                    }

                    var err = ExtractError(res);
                    var success = string.IsNullOrWhiteSpace(err)
                        || err.IndexOf(":ok", StringComparison.OrdinalIgnoreCase) >= 0
                        || err.IndexOf("ok", StringComparison.OrdinalIgnoreCase) >= 0;
                    Report(success, success ? string.Empty : err);
                });

                var method = wxType.GetMethod("ShareAppMessage", BindingFlags.Public | BindingFlags.Static, null, new[] { shareOptionType }, null);
                if (method == null)
                {
                    base.Share(payload, callback);
                    return;
                }

                method.Invoke(null, new[] { option });
            }
            catch (Exception ex)
            {
                callback?.Invoke(new ShareResult(false, ex.Message));
            }
        }

        private static Type ResolveType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void TrySetStringMember(object target, string memberName, string value)
        {
            if (target == null)
            {
                return;
            }

            var targetType = target.GetType();
            var property = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.CanWrite && string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (property != null && property.PropertyType == typeof(string))
            {
                property.SetValue(target, value ?? string.Empty);
                return;
            }

            var field = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => string.Equals(f.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(target, value ?? string.Empty);
            }
        }

        private static void TrySetCallbackMember(object target, string memberName, Action<object> callback)
        {
            if (target == null || callback == null)
            {
                return;
            }

            var targetType = target.GetType();
            var property = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.CanWrite && typeof(Delegate).IsAssignableFrom(p.PropertyType) && string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                var callbackDelegate = CreateCallbackDelegate(property.PropertyType, callback);
                if (callbackDelegate != null)
                {
                    property.SetValue(target, callbackDelegate);
                }

                return;
            }

            var field = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => typeof(Delegate).IsAssignableFrom(f.FieldType) && string.Equals(f.Name, memberName, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                var callbackDelegate = CreateCallbackDelegate(field.FieldType, callback);
                if (callbackDelegate != null)
                {
                    field.SetValue(target, callbackDelegate);
                }
            }
        }

        private static Delegate CreateCallbackDelegate(Type delegateType, Action<object> callback)
        {
            if (delegateType == null || callback == null)
            {
                return null;
            }

            var invokeMethod = delegateType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                return null;
            }

            var parameters = invokeMethod.GetParameters();
            if (parameters.Length == 0)
            {
                var proxy = new NoArgCallbackProxy(() => callback(null));
                return Delegate.CreateDelegate(delegateType, proxy, nameof(NoArgCallbackProxy.Invoke), false);
            }

            if (parameters.Length == 1)
            {
                var parameterType = parameters[0].ParameterType;
                var proxyType = typeof(OneArgCallbackProxy<>).MakeGenericType(parameterType);
                var proxy = Activator.CreateInstance(proxyType, callback);
                var method = proxyType.GetMethod(nameof(OneArgCallbackProxy<object>.Invoke));
                if (proxy != null && method != null)
                {
                    return Delegate.CreateDelegate(delegateType, proxy, method, false);
                }
            }

            return null;
        }

        private static string ExtractError(object result)
        {
            if (result == null)
            {
                return "wx.shareAppMessage failed.";
            }

            var resultType = result.GetType();
            var property = resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(p.Name, "errMsg", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "error", StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                var value = property.GetValue(result);
                if (value != null)
                {
                    return value.ToString();
                }
            }

            var field = resultType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => string.Equals(f.Name, "errMsg", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.Name, "error", StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                var value = field.GetValue(result);
                if (value != null)
                {
                    return value.ToString();
                }
            }

            return "wx.shareAppMessage failed.";
        }

        private sealed class NoArgCallbackProxy
        {
            private readonly Action callback;

            public NoArgCallbackProxy(Action callback)
            {
                this.callback = callback;
            }

            public void Invoke()
            {
                callback?.Invoke();
            }
        }

        private sealed class OneArgCallbackProxy<T>
        {
            private readonly Action<object> callback;

            public OneArgCallbackProxy(Action<object> callback)
            {
                this.callback = callback;
            }

            public void Invoke(T arg)
            {
                callback?.Invoke(arg);
            }
        }
    }
}
