using System;
using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.Common
{
    public class CommonLogService : ILogService
    {
        public void Info(string tag, string message)
        {
            Debug.Log(Format(tag, message));
        }

        public void Warn(string tag, string message)
        {
            Debug.LogWarning(Format(tag, message));
        }

        public void Error(string tag, string message, Exception exception = null)
        {
            var output = Format(tag, message);
            if (exception != null)
            {
                output = output + "\n" + exception;
            }

            Debug.LogError(output);
        }

        public void Flush()
        {
        }

        private static string Format(string tag, string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{tag}] {message}";
        }
    }
}
