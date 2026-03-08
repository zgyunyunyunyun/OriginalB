using System;
using System.Reflection;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;

namespace OriginalB.Platform.Services.Douyin
{
    public class DouyinAuthService : CommonAuthService, IAuthService
    {
        private bool isLoggedIn;
        private string userId = string.Empty;

        bool IAuthService.IsLoggedIn => IsLoggedIn;
        string IAuthService.UserId => UserId;
        void IAuthService.Initialize() => Initialize();
        void IAuthService.Login(Action<LoginResult> callback) => Login(callback);
        void IAuthService.Logout() => Logout();

        public new bool IsLoggedIn => isLoggedIn;
        public new string UserId => userId;

        public new void Initialize()
        {
            isLoggedIn = false;
            userId = string.Empty;
        }

        public new void Login(Action<LoginResult> callback)
        {
            try
            {
                var accountType = ResolveType("TTSDK.TTAccount");
                if (accountType == null)
                {
                    callback?.Invoke(new LoginResult(false, string.Empty, "TTAccount not found."));
                    return;
                }

                var successType = accountType.GetNestedType("OnLoginSuccessCallback", BindingFlags.Public);
                var failedType = accountType.GetNestedType("OnLoginFailedCallback", BindingFlags.Public);
                var loginMethod = accountType.GetMethod(
                    "Login",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { successType, failedType, typeof(bool) },
                    null);

                if (successType == null || failedType == null || loginMethod == null)
                {
                    callback?.Invoke(new LoginResult(false, string.Empty, "TTAccount.Login API not available."));
                    return;
                }

                var completed = false;
                void Complete(LoginResult result)
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    callback?.Invoke(result);
                }

                var successDelegate = CreateCallbackDelegate(successType, args =>
                {
                    var code = args.Length > 0 ? args[0] as string : string.Empty;
                    var anonymousCode = args.Length > 1 ? args[1] as string : string.Empty;

                    var loginState = false;
                    if (args.Length > 2 && args[2] is bool boolState)
                    {
                        loginState = boolState;
                    }

                    var resolvedUserId = !string.IsNullOrWhiteSpace(code)
                        ? code
                        : (anonymousCode ?? string.Empty);
                    isLoggedIn = loginState || !string.IsNullOrWhiteSpace(resolvedUserId);
                    userId = resolvedUserId;
                    Complete(new LoginResult(true, userId, string.Empty));
                });

                var failDelegate = CreateCallbackDelegate(failedType, args =>
                {
                    var error = args.Length > 0 && args[0] != null ? args[0].ToString() : "login failed";
                    isLoggedIn = false;
                    userId = string.Empty;
                    Complete(new LoginResult(false, string.Empty, error));
                });
                if (successDelegate == null || failDelegate == null)
                {
                    callback?.Invoke(new LoginResult(false, string.Empty, "Failed to bind TTAccount callbacks."));
                    return;
                }

                loginMethod.Invoke(null, new object[] { successDelegate, failDelegate, PlatformRuntimeConfig.DouyinForceLogin });
            }
            catch (Exception ex)
            {
                callback?.Invoke(new LoginResult(false, string.Empty, ex.Message));
            }
        }

        public new void Logout()
        {
            isLoggedIn = false;
            userId = string.Empty;
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

            if (parameters.Length == 3)
            {
                var proxyType = typeof(ThreeArgBoxedCallbackProxy<,,>).MakeGenericType(
                    parameters[0].ParameterType,
                    parameters[1].ParameterType,
                    parameters[2].ParameterType);
                var proxy = Activator.CreateInstance(proxyType, new Action<object, object, object>((a, b, c) => callback(new[] { a, b, c })));
                var method = proxyType.GetMethod(nameof(ThreeArgBoxedCallbackProxy<object, object, object>.Invoke));
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

        private sealed class ThreeArgBoxedCallbackProxy<T1, T2, T3>
        {
            private readonly Action<object, object, object> callback;

            public ThreeArgBoxedCallbackProxy(Action<object, object, object> callback)
            {
                this.callback = callback;
            }

            public void Invoke(T1 a, T2 b, T3 c)
            {
                callback?.Invoke(a, b, c);
            }
        }
    }
}
