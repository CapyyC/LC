using System;
using System.Collections.Generic;
using System.Globalization;

namespace LobotomyArchipelago
{
    /// <summary>Applies received items to the game.</summary>
    public static class ItemApplier
    {
        private static readonly Dictionary<long, int> ResolvedResearch = new Dictionary<long, int>();
        private static readonly System.Random Rng = new System.Random();

        /// <summary>Announces newly received items and queues one-shot items (fillers and traps).</summary>
        public static void ProcessNewItems()
        {
            Session session = Plugin.Session;
            while (session.ProcessedOneShots < session.Items.Count)
            {
                ReceivedItem item = session.Items[session.ProcessedOneShots];
                long value;
                Ids.ItemKind kind = Ids.Kind(item.Item, out value);
                if (kind == Ids.ItemKind.Filler) session.PendingFillers.Add(item.Item);
                else if (kind == Ids.ItemKind.Trap) session.PendingTraps.Add(item.Item);
                string from = string.IsNullOrEmpty(item.Sender) || item.Player == session.Slot ? "" : " from " + item.Sender;
                Plugin.Message("Received " + (string.IsNullOrEmpty(item.Name) ? "item " + item.Item : item.Name) + from);
                session.ProcessedOneShots++;
                session.Dirty = true;
            }
        }

        /// <summary>Applies the state-based items (research and E.G.O equipment). Safe to call repeatedly.</summary>
        public static void ReconcileState()
        {
            Session session = Plugin.Session;
            if (!session.HasSlot || !GameApi.InFacility) return;
            if (session.ResearchRandomized) ApplyResearch();
            ReconcileEgo();
        }

        private static void ApplyResearch()
        {
            Session session = Plugin.Session;
            Dictionary<string, object> table = session.SlotData.Obj("research");
            if (table == null) return;
            foreach (long itemId in session.ReceivedResearch)
            {
                List<object> info = table.Arr(itemId.ToString(CultureInfo.InvariantCulture));
                if (info == null || info.Count < 3) continue;
                int sefira = (int)JsonExt.ToLong(info[0]);
                int slot = (int)JsonExt.ToLong(info[1]);
                string name = Convert.ToString(info[2], CultureInfo.InvariantCulture);
                int knownId = info.Count >= 4 ? (int)JsonExt.ToLong(info[3], -1) : -1;
                int researchId = ResolveResearch(itemId, sefira, slot, name, knownId);
                if (researchId < 0) continue;
                if (GameApi.UpgradedResearchIds(sefira).Contains(researchId)) continue;
                if (GameApi.UpgradeResearch(researchId))
                {
                    Plugin.Message("Research unlocked: " + name);
                }
            }
        }

        /// <summary>
        /// Finds the game's research id for an item: the id sent by the apworld, then the (English) name, then the
        /// position in the department's research list. research_overrides.json in the mod folder takes precedence.
        /// </summary>
        public static int ResolveResearch(long itemId, int sefira, int slot, string name, int knownId = -1)
        {
            int cached;
            if (ResolvedResearch.TryGetValue(itemId, out cached)) return cached;
            int overrideId = ResearchOverride(name);
            if (overrideId >= 0)
            {
                ResolvedResearch[itemId] = overrideId;
                return overrideId;
            }
            List<object> list = GameApi.ResearchOfSefira(sefira);
            var candidates = new List<object>();
            foreach (object research in list)
            {
                int id = GameApi.ResearchId(research);
                if (id >= 0) candidates.Add(research);
            }
            int result = -1;
            foreach (object research in candidates)
            {
                if (knownId >= 0 && GameApi.ResearchId(research) == knownId) result = knownId;
            }
            foreach (object research in candidates)
            {
                if (result >= 0) break;
                if (string.Equals(GameApi.ResearchName(research).Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    result = GameApi.ResearchId(research);
                    break;
                }
            }
            if (result < 0 && slot < candidates.Count) result = GameApi.ResearchId(candidates[slot]);
            if (result < 0)
            {
                Reflect.ReportOnce("research:" + name, "Could not find the research '" + name + "' in the game data");
                return -1;
            }
            ResolvedResearch[itemId] = result;
            return result;
        }

        private static Dictionary<string, object> _overrides;

        private static int ResearchOverride(string name)
        {
            if (_overrides == null)
            {
                _overrides = new Dictionary<string, object>();
                try
                {
                    string path = System.IO.Path.Combine(Plugin.ModDirectory, "research_overrides.json");
                    if (System.IO.File.Exists(path))
                    {
                        _overrides = Json.Parse(System.IO.File.ReadAllText(path)) as Dictionary<string, object> ??
                                     new Dictionary<string, object>();
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Could not read research_overrides.json: " + e.Message);
                }
            }
            return _overrides.ContainsKey(name) ? _overrides.Int(name, -1) : -1;
        }

        private static void ReconcileEgo()
        {
            Session session = Plugin.Session;
            Reconcile(session.ExpectedWeapons, "WEAPON");
            Reconcile(session.ExpectedSuits, "ARMOR");
        }

        private static void Reconcile(Dictionary<long, int> expected, string kind)
        {
            foreach (KeyValuePair<long, int> entry in expected)
            {
                int typeId = GameApi.EquipmentTypeId(entry.Key, kind);
                int have = GameApi.CountEquipment(typeId);
                for (int i = have; i < entry.Value; i++)
                {
                    object created = GameApi.CreateEquipment(typeId);
                    if (created == null)
                    {
                        Reflect.ReportOnce("ego:" + typeId, "Could not create E.G.O equipment " + typeId);
                        break;
                    }
                    Plugin.Message("E.G.O " + (kind == "WEAPON" ? "weapon" : "suit") + " of " +
                                   GameApi.CreatureName(entry.Key) + " added to the inventory");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Fillers
        // ------------------------------------------------------------------------------------------------------------

        public static void ApplyPendingFillers(bool inManagement)
        {
            Session session = Plugin.Session;
            if (!GameApi.InFacility) return;
            for (int i = 0; i < session.PendingFillers.Count; i++)
            {
                long itemId = session.PendingFillers[i];
                long value;
                Ids.Kind(itemId, out value);
                if (!ApplyFiller((int)value, inManagement)) continue;
                session.PendingFillers.RemoveAt(i);
                session.Dirty = true;
                i--;
            }
        }

        private static bool ApplyFiller(int filler, bool inManagement)
        {
            Session session = Plugin.Session;
            switch (filler)
            {
                case Ids.FillerLob:
                    if (!GameApi.AddLob(session.LobPerItem)) return false;
                    Plugin.Message("+" + session.LobPerItem + " LOB points");
                    return true;
                case Ids.FillerPeBoxes:
                {
                    var creatures = new List<object>();
                    foreach (object creature in GameApi.Creatures())
                    {
                        if (!GameApi.IsTool(GameApi.CreatureId(creature))) creatures.Add(creature);
                    }
                    if (creatures.Count == 0) return false;
                    object target = creatures[Rng.Next(creatures.Count)];
                    if (!GameApi.AddCubes(target, session.PeBoxesPerItem)) return false;
                    Plugin.Message("+" + session.PeBoxesPerItem + " PE boxes for " +
                                   GameApi.CreatureName(GameApi.CreatureId(target)));
                    return true;
                }
                case Ids.FillerEnkephalin:
                {
                    if (!inManagement) return false;
                    float amount = GameApi.EnergyQuota() * 0.1f;
                    GameApi.AddEnergy(amount);
                    Plugin.Message("+" + Math.Round(amount) + " energy from an Enkephalin canister");
                    return true;
                }
                case Ids.FillerTraining:
                {
                    List<object> agents = GameApi.Agents();
                    if (agents.Count == 0) return false;
                    foreach (object agent in agents)
                    {
                        object stat = Reflect.Get(agent, "primaryStat");
                        if (stat == null) continue;
                        foreach (string field in new[] { "hp", "mental", "work", "battle" })
                        {
                            Reflect.Set(stat, Reflect.GetInt(stat, field) + 2, field);
                        }
                        GameApi.Notify("UpdateAgentState_" + GameApi.InstanceId(agent));
                    }
                    Plugin.Message("Training manual: every agent gained +2 in all stats");
                    return true;
                }
                case Ids.FillerMedical:
                {
                    List<object> workers = GameApi.Workers();
                    if (workers.Count == 0) return false;
                    foreach (object worker in workers)
                    {
                        if (GameApi.IsDead(worker)) continue;
                        Reflect.Set(worker, Reflect.GetFloat(worker, "maxHp"), "hp");
                        Reflect.Set(worker, Reflect.GetFloat(worker, "maxMental"), "mental");
                    }
                    Plugin.Message("Emergency medical supplies: every employee was healed");
                    return true;
                }
                default:
                    return true;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Traps
        // ------------------------------------------------------------------------------------------------------------

        public static void FireNextTrap()
        {
            Session session = Plugin.Session;
            if (session.PendingTraps.Count == 0) return;
            long itemId = session.PendingTraps[0];
            long value;
            Ids.Kind(itemId, out value);
            session.PendingTraps.RemoveAt(0);
            session.Dirty = true;
            if (!Traps.Fire((int)value)) Log.Warn("Trap " + value + " could not be applied");
        }
    }
}
