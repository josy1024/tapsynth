using System;
using System.IO;
using System.Text;

namespace TapSynth.Utils
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _path = Path.Combine(Directory.GetCurrentDirectory(), "tapsynth_debug.log");

        public static void Info(string message)
        {
            TryWrite("INFO", message);
        }

        public static void Error(string message)
        {
            TryWrite("ERROR", message);
        }

        public static void Exception(Exception ex, string context = null)
        {
            var msg = new StringBuilder();
            if (!string.IsNullOrEmpty(context)) msg.Append(context + " - ");
            msg.Append(ex.ToString());
            TryWrite("EX", msg.ToString());
        }

        private static void TryWrite(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_path, $"{DateTime.UtcNow:O} [{level}] {message}\n");
                }
            }
            catch { }
        }
    }
}
