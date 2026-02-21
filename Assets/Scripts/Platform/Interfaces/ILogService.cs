using System;

namespace OriginalB.Platform.Interfaces
{
    public interface ILogService
    {
        void Info(string tag, string message);
        void Warn(string tag, string message);
        void Error(string tag, string message, Exception exception = null);
        void Flush();
    }
}
