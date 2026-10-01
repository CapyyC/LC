using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LobotomyArchipelago
{
    /// <summary>F9: writes everything useful for troubleshooting to Archipelago/diagnostics.txt.</summary>
    public static class Diagnostics
    {
        public static void Write()
        {
            var builder = new StringBuilder();
            try
            {
                Session session = Plugin.Session;
                builder.Append("Lobotomy Corporation Archipelago mod ").Append(Plugin.Version).Append('\n');
                builder.Append("Seed ").Append(session.Seed).Append(" slot ").Append(session.Slot)
                    .Append(" (").Append(session.SlotName).Append(")\n");
                builder.Append("Bridge connected: ").Append(Plugin.Bridge != null && Plugin.Bridge.Connected)
                    .Append(", server connected: ").Append(session.ServerConnected).Append('\n');
                builder.Append("Day index: ").Append(GameApi.DayIndex).Append(", in facility: ")
                    .Append(GameApi.InFacility).Append(", state: ").Append(GameApi.GameStateName)
                    .Append(", management: ").Append(Runner.InManagement).Append('\n');

                builder.Append("\n== Patches ==\n");
                foreach (string line in HarmonyBridge.Report) builder.Append(line).Append('\n');

                builder.Append("\n== Containment order ==\n");
                for (int i = 0; i < session.ContainmentOrder.Count; i++)
                {
                    long id = session.ContainmentOrder[i];
                    builder.Append('#').Append((i + 1).ToString("00")).Append(' ').Append(id).Append(' ')
                        .Append(GameApi.CreatureName(id)).Append('\n');
                }

                builder.Append("\n== Abnormalities in facility ==\n");
                foreach (object creature in GameApi.Creatures())
                {
                    long id = GameApi.CreatureId(creature);
                    builder.Append(id).Append(' ').Append(GameApi.CreatureName(id)).Append(" observation ")
                        .Append(GameApi.ObservationLevel(creature)).Append(" instance ")
                        .Append(GameApi.InstanceId(creature)).Append('\n');
                }

                builder.Append("\n== Received abnormalities (in order) ==\n");
                foreach (long id in session.ReceivedAbnormalities)
                {
                    builder.Append(id).Append(' ').Append(GameApi.CreatureName(id)).Append('\n');
                }

                builder.Append("\n== Research ==\n");
                Dictionary<string, object> table = session.SlotData.Obj("research");
                foreach (int sefira in new[] { 0, 1, 2, 3, 4, 6, 7, 8, 9 })
                {
                    HashSet<int> upgraded = GameApi.UpgradedResearchIds(sefira);
                    foreach (object research in GameApi.ResearchOfSefira(sefira))
                    {
                        int id = GameApi.ResearchId(research);
                        builder.Append(Enum.GetName(typeof(SefiraIds), sefira)).Append(' ').Append(id).Append(' ')
                            .Append(GameApi.ResearchName(research))
                            .Append(upgraded.Contains(id) ? " [upgraded]" : "").Append('\n');
                    }
                }
                if (table != null)
                {
                    builder.Append("Item -> research id resolution:\n");
                    foreach (KeyValuePair<string, object> entry in table)
                    {
                        var info = entry.Value as List<object>;
                        if (info == null || info.Count < 3) continue;
                        int resolved = ItemApplier.ResolveResearch(long.Parse(entry.Key, CultureInfo.InvariantCulture),
                            (int)JsonExt.ToLong(info[0]), (int)JsonExt.ToLong(info[1]), Convert.ToString(info[2],
                                CultureInfo.InvariantCulture));
                        builder.Append("  ").Append(info[2]).Append(" -> ").Append(resolved).Append('\n');
                    }
                }

                builder.Append("\n== Missions ==\n");
                foreach (object mission in GameApi.MissionsInProgress())
                {
                    builder.Append("in progress: ").Append(GameApi.MissionId(mission)).Append(" sefira ")
                        .Append(GameApi.MissionSefira(mission)).Append(' ').Append(GameApi.MissionCondition(mission))
                        .Append('\n');
                }
                foreach (object mission in GameApi.ClearedMissions())
                {
                    builder.Append("cleared: ").Append(GameApi.MissionId(mission)).Append('\n');
                }

                builder.Append("\n== Agent slots ==\n");
                foreach (object sefira in GameApi.ActivatedSefiras())
                {
                    int sefiraEnum = GameApi.SefiraEnumOf(sefira);
                    builder.Append(Enum.GetName(typeof(SefiraIds), sefiraEnum)).Append(" agents ")
                        .Append(GameApi.AgentsOf(sefira).Count).Append(" cap ").Append(session.AgentSlotCap(sefiraEnum))
                        .Append(" index ").Append(Reflect.Get(sefira, "indexString")).Append('\n');
                }
            }
            catch (Exception e)
            {
                builder.Append("\nDiagnostics failed: ").Append(e).Append('\n');
            }
            try
            {
                string path = Path.Combine(Log.Directory, "diagnostics.txt");
                File.WriteAllText(path, builder.ToString());
                Plugin.Message("Diagnostics written to " + path, false);
            }
            catch (Exception e)
            {
                Log.Error("Could not write diagnostics", e);
            }
        }
    }
}
