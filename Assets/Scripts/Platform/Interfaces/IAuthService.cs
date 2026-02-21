using System;

namespace OriginalB.Platform.Interfaces
{
    public readonly struct LoginResult
    {
        public LoginResult(bool success, string userId, string error)
        {
            Success = success;
            UserId = userId;
            Error = error;
        }

        public bool Success { get; }
        public string UserId { get; }
        public string Error { get; }
    }

    public interface IAuthService
    {
        bool IsLoggedIn { get; }
        string UserId { get; }

        void Initialize();
        void Login(Action<LoginResult> callback);
        void Logout();
    }
}
