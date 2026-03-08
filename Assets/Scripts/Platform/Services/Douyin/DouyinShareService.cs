using System;
using System.Reflection;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;

namespace OriginalB.Platform.Services.Douyin
{
    public class DouyinShareService : CommonShareService, IShareService
    {
        void IShareService.Share(SharePayload payload, Action<ShareResult> callback)
        {
            Share(payload, callback);
        }

        public new void Share(SharePayload payload, Action<ShareResult> callback)
        {
            try
            {
                var ttShareType = ResolveType("TTSDK.TTShare");
                var jsonData = BuildShareJson(payload);
                if (ttShareType == null || jsonData == null)
                {
                    base.Share(payload, callback);
                    return;
                }

                var successType = ttShareType.GetNestedType("OnShareSuccessCallback", BindingFlags.Public);
                var failType = ttShareType.GetNestedType("OnShareFailedCallback", BindingFlags.Public);
                var cancelType = ttShareType.GetNestedType("OnShareCancelledCallback", BindingFlags.Public);
                var shareMethod = ttShareType.GetMethod(
                    "ShareAppMessage",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { successType, failType, cancelType, jsonData.GetType() },
                    null);

                if (successType == null || failType == null || cancelType == null || shareMethod == null)
                {
                    base.Share(payload, callback);
                    return;
                }

                var completed = false;
                void Complete(bool success, string error)
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    callback?.Invoke(new ShareResult(success, error ?? string.Empty));
                }

                var successDelegate = CreateCallbackDelegate(successType, args => Complete(true, string.Empty));
                var failDelegate = CreateCallbackDelegate(failType, args => Complete(false, args.Length > 0 ? ExtractError(args[0]) : "share failed"));
                var cancelDelegate = CreateCallbackDelegate(cancelType, args => Complete(false, "share cancelled"));

                if (successDelegate == null || failDelegate == null || cancelDelegate == null)
                {
                    base.Share(payload, callback);
                    return;
                }

                shareMethod.Invoke(null, new object[] { successDelegate, failDelegate, cancelDelegate, jsonData });
            }
            catch (Exception ex)
            {
                callback?.Invoke(new ShareResult(false, ex.Message));
            }
        }

        private static object BuildShareJson(SharePayload payload)
        {
            var jsonMapperType = ResolveType("TTSDK.UNBridgeLib.LitJson.JsonMapper");
            if (jsonMapperType == null)
            {
                return null;
            }

            var toObjectMethod = jsonMapperType.GetMethod("ToObject", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (toObjectMethod == null)
            {
                return null;
            }

            var title = string.IsNullOrWhiteSpace(payload.Title)
                ? PlatformRuntimeConfig.DouyinDefaultShareTitle
                : payload.Title;
            var imageUrl = string.IsNullOrWhiteSpace(payload.ImageUrl)
                ? PlatformRuntimeConfig.DouyinDefaultShareImageUrl
                : payload.ImageUrl;
            var query = string.IsNullOrWhiteSpace(payload.Query)
                ? PlatformRuntimeConfig.DouyinDefaultShareQuery
                : payload.Query;

            var json = "{"
                + "\"title\":\"" + EscapeJson(title) + "\","
                + "\"imageUrl\":\"" + EscapeJson(imageUrl) + "\","
                + "\"query\":\"" + EscapeJson(query) + "\""
                + "}";
            return toObjectMethod.Invoke(null, new object[] { json });
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string ExtractError(object result)
        {
            if (result == null)
            {
                return "tt.shareAppMessage failed";
            }

            return result.ToString();
        }

        private static Type ResolveType(string fullName)
        {
            var type = Type.GetType(fullName, false);
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

        private static Delegate CreateCallbackDelegate(Type delegateType, Action<object[]> callback)
        {
            if (delegateType == null || callback == null)
            {
                return null;
            }

            var invoke = delegateType.GetMethod("Invoke");
            if (invoke == null)
            {
                return null;
            }

            var parameters = invoke.GetParameters();
            if (parameters.Length == 0)
            {
                var proxy = new NoArgCallbackProxy(() => callback(Array.Empty<object>()));
                return Delegate.CreateDelegate(delegateType, proxy, nameof(NoArgCallbackProxy.Invoke), false);
            }

            if (parameters.Length == 1)
            {
                var proxyType = typeof(OneArgBoxedCallbackProxy<>).MakeGenericType(parameters[0].ParameterType);
                var proxy = Activator.CreateInstance(proxyType, new Action<object>(arg => callback(new[] { arg })));
                var method = proxyType.GetMethod(nameof(OneArgBoxedCallbackProxy<object>.Invoke));
                if (proxy != null && method != null)
                {
                    return Delegate.CreateDelegate(delegateType, proxy, method, false);
                }
            }

            return null;
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

        private sealed class OneArgBoxedCallbackProxy<T>
        {
            private readonly Action<object> callback;

            public OneArgBoxedCallbackProxy(Action<object> callback)
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
