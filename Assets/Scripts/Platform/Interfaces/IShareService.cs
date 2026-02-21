using System;

namespace OriginalB.Platform.Interfaces
{
    public readonly struct SharePayload
    {
        public SharePayload(string title, string imageUrl, string query)
        {
            Title = title;
            ImageUrl = imageUrl;
            Query = query;
        }

        public string Title { get; }
        public string ImageUrl { get; }
        public string Query { get; }
    }

    public readonly struct ShareResult
    {
        public ShareResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public bool Success { get; }
        public string Error { get; }
    }

    public interface IShareService
    {
        void Share(SharePayload payload, Action<ShareResult> callback);
    }
}
