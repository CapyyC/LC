using System;
using System.Collections.Generic;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Harmony patch callbacks. They are attached by name in <see cref="InstallAll"/>; every patch is optional and
    /// a failure to attach one only disables that feature (see the diagnostics report, F9).
    /// Patch methods only use __instance (typed as object), so they do not depend on parameter names.
    /// </summary>
    public static class Patches
    {
        public static void InstallAll()
        {
            // Runner fallback: make sure the Update loop exists once a scene is running.
            HarmonyBridge.Patch("NewTitleScript", "Update", 0, null, "EnsureRunner_Postfix");
            HarmonyBridge.Patch("UnitMouseEventManager", "Update", 0, null, "EnsureRunner_Postfix");

            // Abnormality extraction
            HarmonyBridge.Patch("CreatureGenerate.CreatureGenerateModel", "SetCreature", 0, null,
                "SetCreature_Postfix");
            HarmonyBridge.Patch("CreatureSelectUI", "GetCreatureList", -1, null, "GetCreatureList_Postfix");
            HarmonyBridge.Patch("GameSceneController", "OnClickNextDay", -1, "OnClickNextDay_Prefix", null);

            // E.G.O extraction
            HarmonyBridge.Patch("CreatureInfoEquipmentRoot", "OnClickWeapon", -1, "OnClickWeapon_Prefix", null);
            HarmonyBridge.Patch("CreatureInfoEquipmentRoot", "OnClickArmor", -1, "OnClickArmor_Prefix", null);

            // Missions and research
            HarmonyBridge.Patch("DeployUI", "CheckResearchAvailable", -1, "CheckResearchAvailable_Prefix", null);

            // Day flow
            HarmonyBridge.Patch("GameManager", "StartGame", -1, null, "StartGame_Postfix");
            HarmonyBridge.Patch("GameManager", "EndGame", -1, null, "EndGame_Postfix");
            HarmonyBridge.Patch("GameManager", "ClearAction", -1, "ClearAction_Prefix", null);
            HarmonyBridge.Patch("KetherLastBossBase", "ToCredit", -1, "ToCredit_Prefix", null);
            HarmonyBridge.Patch("DontTouchMe", "ExitGame", -1, "DontTouchMeExit_Prefix", null);

            // Agent slots
            HarmonyBridge.Patch("AgentModel", "SetCurrentSefira", 1, "SetCurrentSefira_Prefix",
                "SetCurrentSefira_Postfix");

            // DeathLink
            HarmonyBridge.Patch("EscapeUI", "RestartAtCheckpoint", -1, null, "RestartAtCheckpoint_Postfix");
            HarmonyBridge.Patch("AgentModel", "OnDie", -1, null, "AgentOnDie_Postfix");

            Log.Info("Patch report:\n  " + string.Join("\n  ", HarmonyBridge.Report.ToArray()));
        }

        private static void Guard(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Error("Patch " + name + " failed", e);
            }
        }

        // ----------------------------------------------------------------------------------------------------------
        public static void EnsureRunner_Postfix()
        {
            if (Plugin.Runner == null) Plugin.EnsureRunner();
        }

        public static void SetCreature_Postfix(object __instance)
        {
            Guard("SetCreature", () => Extraction.ReplaceGeneratedCreatures(__instance));
        }

        public static void GetCreatureList_Postfix(object __instance)
        {
            Guard("GetCreatureList", () => Extraction.SanitizeSelectionUi(__instance));
        }

        public static bool OnClickNextDay_Prefix()
        {
            try
            {
                return Extraction.AllowNextDay();
            }
            catch (Exception e)
            {
                Log.Error("OnClickNextDay patch failed", e);
                return true;
            }
        }

        public static bool OnClickWeapon_Prefix(object __instance)
        {
            return Extraction.TryExtractionCheck(__instance, "WEAPON");
        }

        public static bool OnClickArmor_Prefix(object __instance)
        {
            return Extraction.TryExtractionCheck(__instance, "ARMOR");
        }

        public static bool CheckResearchAvailable_Prefix()
        {
            try
            {
                Session session = Plugin.Session;
                if (!session.HasSlot) return true;
                bool randomized = session.ResearchRandomized;
                Tracker.SweepMissions(randomized);
                if (Plugin.Runner != null) Plugin.Runner.FlushChecksSoon();
                // With randomized research the mission rewards are Archipelago checks instead of a research choice.
                return !randomized;
            }
            catch (Exception e)
            {
                Log.Error("CheckResearchAvailable patch failed", e);
                return true;
            }
        }

        public static void StartGame_Postfix()
        {
            Guard("StartGame", () =>
            {
                Runner.InManagement = true;
                Runner.ManagementStartTime = UnityEngine.Time.realtimeSinceStartup;
                Tracker.Sweep();
                ItemApplier.ReconcileState();
                Runner.WarnAboutAgentSlots(true);
            });
        }

        public static void EndGame_Postfix()
        {
            Runner.InManagement = false;
        }

        public static void ClearAction_Prefix()
        {
            Guard("ClearAction", () =>
            {
                Runner.InManagement = false;
                Tracker.OnDayCleared();
                if (Plugin.Runner != null) Plugin.Runner.FlushChecks();
            });
        }

        public static void ToCredit_Prefix()
        {
            Guard("ToCredit", Tracker.OnFinalDayCleared);
        }

        public static void DontTouchMeExit_Prefix()
        {
            Guard("DontTouchMe", Tracker.OnDontTouchMeCrash);
        }

        // ----------------------------------------------------------------------------------------------------------
        // Agent slots: the department an agent is moved to must have a free slot, otherwise the move is undone.
        // ----------------------------------------------------------------------------------------------------------
        private static string _previousSefira;
        private static bool _reverting;

        public static void SetCurrentSefira_Prefix(object __instance)
        {
            if (_reverting) return;
            _previousSefira = Reflect.Get(__instance, "currentSefira") as string;
        }

        public static void SetCurrentSefira_Postfix(object __instance)
        {
            if (_reverting) return;
            try
            {
                Session session = Plugin.Session;
                if (!session.HasSlot || !session.AgentSlotsRandomized || Runner.InManagement) return;
                string now = Reflect.Get(__instance, "currentSefira") as string;
                if (string.IsNullOrEmpty(now) || now == "0" || now == _previousSefira) return;
                object sefira = GameApi.SefiraByName(now);
                if (sefira == null) return;
                int sefiraEnum = GameApi.SefiraEnumOf(sefira);
                int cap = session.AgentSlotCap(sefiraEnum);
                int assigned = 0;
                foreach (object agent in GameApi.Agents())
                {
                    if (!GameApi.IsDead(agent) && (Reflect.Get(agent, "currentSefira") as string) == now) assigned++;
                }
                if (assigned <= cap) return;
                _reverting = true;
                Reflect.Call(__instance, "SetCurrentSefira", string.IsNullOrEmpty(_previousSefira) ? "0" : _previousSefira);
                Plugin.Message((Reflect.Get(sefira, "name") ?? "This department") + " only has " + cap +
                               " agent slot(s). Find more agent slots in the multiworld!");
            }
            catch (Exception e)
            {
                Log.Error("Agent slot check failed", e);
            }
            finally
            {
                _reverting = false;
            }
        }

        // ----------------------------------------------------------------------------------------------------------
        // DeathLink
        // ----------------------------------------------------------------------------------------------------------
        public static void RestartAtCheckpoint_Postfix()
        {
            Guard("RestartAtCheckpoint", () =>
            {
                Session session = Plugin.Session;
                if (session.DeathLink && session.DeathLinkTrigger == 0)
                {
                    Runner.SendDeathLink(session.SlotName + " restarted a failed day.");
                }
            });
        }

        public static void AgentOnDie_Postfix(object __instance)
        {
            Guard("AgentOnDie", () =>
            {
                Session session = Plugin.Session;
                if (!session.DeathLink || session.DeathLinkTrigger != 1 || !Runner.InManagement) return;
                string sefiraName = Reflect.Get(__instance, "currentSefira") as string;
                object sefira = string.IsNullOrEmpty(sefiraName) ? null : GameApi.SefiraByName(sefiraName);
                if (sefira == null) return;
                List<object> agents = GameApi.AgentsOf(sefira);
                if (agents.Count == 0) return;
                foreach (object agent in agents)
                {
                    if (!GameApi.IsDead(agent)) return;
                }
                Runner.SendDeathLink("Every agent of " + (Reflect.Get(sefira, "name") ?? "a department") + " in " +
                                     session.SlotName + "'s facility died.");
            });
        }
    }
}
