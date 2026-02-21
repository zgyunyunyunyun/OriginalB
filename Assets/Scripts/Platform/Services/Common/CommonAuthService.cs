using System;
using OriginalB.Platform.Interfaces;

namespace OriginalB.Platform.Services.Common
{
    public class CommonAuthService : IAuthService
    {
        public bool IsLoggedIn { get; private set; }
        public string UserId { get; private set; }

        public void Initialize()
        {
            IsLoggedIn = false;
            UserId = string.Empty;
        }

        public void Login(Action<LoginResult> callback)
        {
            var result = new LoginResult(false, string.Empty, "Auth SDK not integrated.");
            callback?.Invoke(result);
        }

        public void Logout()
        {
            IsLoggedIn = false;
            UserId = string.Empty;
        }
    }
}
