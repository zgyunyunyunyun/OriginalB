using System;
using System.IO;
using UnityEngine;

public static class GameDebugLogger
{
    private static readonly object Sync = new object();
    private static string cachedLogFilePath;
    private static bool printedLogPath;

    public static bool EnableConsoleLog = true;
    public static bool EnableFileLog = true;

    public static void Info(string tag, string message)
    {
        Write("INFO", tag, message);
    }

    public static void Warn(string tag, string message)
    {
        Write("WARN", tag, message);
    }

    public static void Error(string tag, string message)
    {
        Write("ERROR", tag, message);
    }

    private static void Write(string level, string tag, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{tag}] {message}";

        if (EnableConsoleLog)
        {
            if (level == "ERROR")
            {
                Debug.LogError(line);
            }
            else if (level == "WARN")
            {
                Debug.LogWarning(line);
            }
            else
            {
                Debug.Log(line);
            }
        }

        if (!EnableFileLog)
        {
            return;
        }

        try
        {
            lock (Sync)
            {
                var path = ResolveLogFilePath();
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameDebugLogger] Write file failed: {ex.Message}");
        }
    }

    private static string ResolveLogFilePath()
    {
        if (!string.IsNullOrEmpty(cachedLogFilePath))
        {
            return cachedLogFilePath;
        }

        var root = ResolveProjectLogDirectory();
        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
        }

        cachedLogFilePath = Path.Combine(root, $"game_debug_{DateTime.Now:yyyyMMdd}.log");
        if (!printedLogPath)
        {
            printedLogPath = true;
            Debug.Log($"[GameDebugLogger] Log file: {cachedLogFilePath}");
        }

        return cachedLogFilePath;
    }

    private static string ResolveProjectLogDirectory()
    {
        try
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                return Path.Combine(projectRoot, "Log");
            }
        }
        catch
        {
        }

        return Path.Combine(Application.persistentDataPath, "Log");
    }
}