using System;

namespace Crossroads.Core
{
    /// <summary>
    /// Engine-free logging hook. Unity bootstrappers wire the handlers to
    /// Debug.Log*; headless tests wire them to Console so the same code paths
    /// run everywhere and messages stay visible.
    /// </summary>
    public static class StoryLog
    {
        public static Action<string> Info = DefaultSink;
        public static Action<string> Warn = DefaultSink;
        public static Action<string> Error = DefaultSink;

        public static void Log(string msg) { if (Info != null) Info(msg); }
        public static void LogWarning(string msg) { if (Warn != null) Warn(msg); }
        public static void LogError(string msg) { if (Error != null) Error(msg); }

        private static void DefaultSink(string msg) { }
    }
}
