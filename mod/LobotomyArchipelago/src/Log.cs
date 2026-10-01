using System;
using System.IO;
using UnityEngine;

namespace LobotomyArchipelago
{
    public static class Log
    {
        private static readonly object Lock = new object();
        private static string _path;

        public static string Directory
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "Archipelago");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static void Init()
        {
            try
            {
                _path = Path.Combine(Directory, "LobotomyArchipelago.log");
                string previous = Path.Combine(Directory, "LobotomyArchipelago.previous.log");
                if (File.Exists(_path))
                {
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(_path, previous);
                }
            }
            catch (Exception)
            {
                _path = null;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", exception == null ? message : message + ": " + exception);
        }

        private static void Write(string level, string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + level + "] " + message;
            try
            {
                Debug.Log("[Archipelago] " + message);
            }
            catch (Exception)
            {
                // Logging must never break the game.
            }
            if (_path == null) return;
            lock (Lock)
            {
                try
                {
                    File.AppendAllText(_path, line + Environment.NewLine);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}
