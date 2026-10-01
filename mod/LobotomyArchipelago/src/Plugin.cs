using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LobotomyArchipelago
{
    /// <summary>Global state of the mod.</summary>
    public static class Plugin
    {
        public const string Version = "0.1.0";
        public const int ProtocolVersion = 1;
        public const string HarmonyId = "archipelago.lobotomycorporation";

        public static readonly Session Session = new Session();
        public static Bridge Bridge;
        public static Runner Runner;
        public static string ModDirectory = "";
        public static bool Initialized;
        public static int Port = 38269;

        /// <summary>Lets the player continue without enough abnormality items (toggled with F11).</summary>
        public static bool BypassDayBlock;

        public static void Init()
        {
            if (Initialized) return;
            Initialized = true;
            Log.Init();
            Log.Info("Lobotomy Corporation Archipelago mod " + Version + " loading");

            try
            {
                ModDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            }
            catch (Exception)
            {
                ModDirectory = "";
            }
            LoadConfig();

            Session.LoadLastSession();

            if (HarmonyBridge.Init(HarmonyId))
            {
                Patches.InstallAll();
            }
            else
            {
                Log.Error("Harmony could not be initialized, the randomizer cannot work.");
            }

            Bridge = new Bridge(Port);
            Bridge.Start();
            EnsureRunner();
        }

        private static void LoadConfig()
        {
            try
            {
                string path = Path.Combine(ModDirectory, "config.json");
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, "{\n    \"port\": 38269\n}\n");
                    return;
                }
                var config = Json.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                Port = config.Int("port", Port);
            }
            catch (Exception e)
            {
                Log.Warn("Could not read config.json: " + e.Message);
            }
        }

        /// <summary>Creates the persistent runner object (Update loop and overlay) if it does not exist yet.</summary>
        public static void EnsureRunner()
        {
            if (Runner != null) return;
            try
            {
                var gameObject = new GameObject("ArchipelagoRunner");
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                Runner = gameObject.AddComponent<Runner>();
                Log.Info("Runner created");
            }
            catch (Exception e)
            {
                Log.Warn("Runner could not be created yet: " + e.Message);
            }
        }

        public static void Message(string text, bool systemLog = true)
        {
            Log.Info(text);
            if (Runner != null) Runner.AddMessage(text);
            if (systemLog && GameApi.InFacility) GameApi.SystemLog("[AP] " + text);
        }
    }
}
