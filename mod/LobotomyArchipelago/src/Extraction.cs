using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Controls which abnormalities are offered on extraction days (only received abnormality items) and turns
    /// E.G.O extraction into location checks.
    /// </summary>
    public static class Extraction
    {
        public static int ExtractionsOnDay(int day)
        {
            if (day < 1 || day > 49 || day % 5 == 0) return 0;
            if ((day >= 21 && day <= 24) || (day >= 46 && day <= 49)) return 2;
            return 1;
        }

        /// <summary>Contained abnormalities (tracking ids) currently in the facility.</summary>
        public static HashSet<long> ContainedIds()
        {
            var ids = new HashSet<long>();
            foreach (object creature in GameApi.Creatures())
            {
                long id = GameApi.CreatureId(creature);
                if (id > 0) ids.Add(GameApi.TrackingId(id));
            }
            return ids;
        }

        /// <summary>Received abnormalities that can still be extracted, in the order they were received.</summary>
        public static List<long> AvailablePool(int dayIndex)
        {
            var result = new List<long>();
            Session session = Plugin.Session;
            HashSet<long> contained = ContainedIds();
            foreach (long id in session.ReceivedAbnormalities)
            {
                if (contained.Contains(id)) continue;
                if (dayIndex <= 0 && GameApi.IsTool(id)) continue;
                if (GameApi.IsWaitingCreature(id)) continue;
                result.Add(id);
            }
            return result;
        }

        private static int _rerollDay = -1;
        private static int _rerolls;

        /// <summary>
        /// The abnormalities offered today: the oldest received ones first. When the game generates offers again on
        /// the same day (Binah's Re-extraction research) the selection rotates through the pool.
        /// </summary>
        private static List<long> Offers(int dayIndex, bool newRoll)
        {
            List<long> pool = AvailablePool(dayIndex);
            int count = Math.Min(Plugin.Session.ExtractionOffers, pool.Count);
            if (newRoll)
            {
                if (_rerollDay == dayIndex) _rerolls++;
                else
                {
                    _rerollDay = dayIndex;
                    _rerolls = 0;
                }
            }
            int start = pool.Count == 0 || _rerollDay != dayIndex ? 0 : (_rerolls * count) % pool.Count;
            var offers = new List<long>();
            for (int i = 0; i < count; i++) offers.Add(pool[(start + i) % pool.Count]);
            return offers;
        }

        /// <summary>Postfix of CreatureGenerate.CreatureGenerateModel.SetCreature: replaces the offered abnormalities.</summary>
        public static void ReplaceGeneratedCreatures(object model)
        {
            Session session = Plugin.Session;
            if (!session.HasSlot) return;
            var list = Reflect.Get(model, "creature") as IList;
            if (list == null || list.Count == 0) return; // no extraction today

            int dayIndex = GameApi.DayIndex;
            List<long> offers = Offers(dayIndex, true);
            if (offers.Count == 0)
            {
                Plugin.Message("No received abnormality is available for extraction! The vanilla selection is used, " +
                               "which takes a containment unit away from Archipelago abnormalities.");
                return;
            }
            list.Clear();
            foreach (long id in offers) list.Add(id);
            Log.Info("Extraction offers: " + string.Join(", ", offers.ConvertAll(GameApi.CreatureName).ToArray()));
        }

        /// <summary>
        /// Postfix of CreatureSelectUI.GetCreatureList: makes sure the selection UI only offers received
        /// abnormalities (also covers BaseMod's CurrentCreatures_Mod list).
        /// </summary>
        public static void SanitizeSelectionUi(object ui)
        {
            Session session = Plugin.Session;
            if (!session.HasSlot) return;
            int dayIndex = GameApi.DayIndex;
            List<long> offers = Offers(dayIndex, false);
            if (offers.Count == 0) return;
            var allowed = new HashSet<long>(AvailablePool(dayIndex));

            foreach (string field in new[] { "CurrentCreatures", "CurrentCreatures_Mod" })
            {
                var list = Reflect.Has(ui, field) ? Reflect.Get(ui, field) as IList : null;
                if (list == null || list.Count == 0) continue;
                bool clean = true;
                foreach (object entry in list)
                {
                    if (!allowed.Contains(EntryId(entry)))
                    {
                        clean = false;
                        break;
                    }
                }
                if (clean) continue;
                Type elementType = list.GetType().IsGenericType ? list.GetType().GetGenericArguments()[0] : typeof(long);
                list.Clear();
                foreach (long id in offers)
                {
                    object entry = MakeEntry(elementType, id);
                    if (entry != null) list.Add(entry);
                }
                Log.Info("Selection UI list " + field + " replaced with Archipelago abnormalities");
            }
        }

        private static long EntryId(object entry)
        {
            if (entry == null) return -1;
            if (entry is long || entry is int) return Convert.ToInt64(entry, CultureInfo.InvariantCulture);
            return Reflect.GetLong(entry, "id");
        }

        private static object MakeEntry(Type elementType, long id)
        {
            if (elementType == typeof(long)) return id;
            try
            {
                return Activator.CreateInstance(elementType, id);
            }
            catch (Exception)
            {
                try
                {
                    return Activator.CreateInstance(elementType, string.Empty, id);
                }
                catch (Exception e)
                {
                    Reflect.ReportOnce("lcid", "Cannot create " + elementType.Name + ": " + e.Message);
                    return null;
                }
            }
        }

        /// <summary>Prefix of GameSceneController.OnClickNextDay: waits for enough abnormality items.</summary>
        public static bool AllowNextDay()
        {
            Session session = Plugin.Session;
            if (!session.HasSlot || Plugin.BypassDayBlock) return true;
            int nextDayIndex = GameApi.DayIndex;
            if (nextDayIndex < 0) return true;
            int needed = ExtractionsOnDay(nextDayIndex + 1);
            if (needed == 0) return true;
            int available = AvailablePool(nextDayIndex).Count;
            if (available >= needed) return true;
            Plugin.Message("Day " + (nextDayIndex + 1) + " needs " + needed + " new abnormalit" +
                           (needed == 1 ? "y" : "ies") + " but only " + available + " received abnormality " +
                           "item(s) are available. Waiting for Archipelago items (F11 overrides).");
            return false;
        }

        // ------------------------------------------------------------------------------------------------------------
        // E.G.O extraction
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Prefix of CreatureInfoEquipmentRoot.OnClickWeapon/OnClickArmor. Returns false when the extraction was turned
        /// into a location check.
        /// </summary>
        public static bool TryExtractionCheck(object root, string kind)
        {
            Session session = Plugin.Session;
            try
            {
                if (!session.HasSlot || session.EgoChecks <= 0) return true;
                object meta = Reflect.Get(root, "MetaInfo", "metaInfo");
                if (meta == null) return true;
                long creatureId = Reflect.GetLong(meta, "id");
                int index = session.ContainmentIndexOf(creatureId);
                if (index < 1 || index > session.ContainmentCount) return true;

                long location = -1;
                for (int copy = 1; copy <= session.EgoChecks; copy++)
                {
                    long candidate = kind == "WEAPON" ? Ids.EgoWeapon(index, copy) : Ids.EgoSuit(index, copy);
                    if (session.IsAllowed(candidate) && !session.IsChecked(candidate))
                    {
                        location = candidate;
                        break;
                    }
                }
                if (location < 0)
                {
                    if (session.ExtractionAfterChecksVanilla) return true;
                    Plugin.Message("Every E.G.O " + (kind == "WEAPON" ? "weapon" : "suit") + " check of this " +
                                   "abnormality is done. E.G.O only comes from Archipelago items.");
                    PlaySound(0);
                    return false;
                }

                object makeInfo = GameApi.FindMakeInfo(meta, kind);
                if (makeInfo == null) return true;
                int requiredLevel = Reflect.GetInt(makeInfo, "level");
                object observeLevel = Reflect.TryCall(root, "GetObserveLevel");
                if (observeLevel != null && requiredLevel > Convert.ToInt32(observeLevel, CultureInfo.InvariantCulture))
                {
                    return true; // let the game refuse it the usual way
                }
                object costValue = Reflect.TryCall(makeInfo, "GetCostAfterUpgrade") ?? Reflect.Get(makeInfo, "cost");
                int cost = costValue == null ? 0 : Convert.ToInt32(costValue, CultureInfo.InvariantCulture);
                object observe = Reflect.Get(root, "ObserveInfo", "observeInfo");
                if (observe == null) return true;
                int cubes = Reflect.GetInt(observe, "cubeNum");
                if (cost > cubes) return true;

                if (Reflect.Method(observe.GetType(), "Transaction", 1) != null)
                {
                    Reflect.Call(observe, "Transaction", -cost);
                }
                else
                {
                    Reflect.Set(observe, cubes - cost, "cubeNum");
                }

                session.AddCheck(location);
                Plugin.Runner.FlushChecksSoon();
                object window = Reflect.Singleton("CreatureInfoWindow");
                if (window != null) Reflect.TryCall(window, "PurchaseAnim", cost);
                PlaySound(2);
                Reflect.TryCall(root, "Initialize");
                AdvanceExtractionMissions(meta);
                Plugin.Message("E.G.O " + (kind == "WEAPON" ? "weapon" : "suit") + " extraction of " +
                               GameApi.CreatureName(creatureId) + " sent as an Archipelago check.");
                return false;
            }
            catch (Exception e)
            {
                Log.Error("E.G.O extraction check failed, falling back to vanilla extraction", e);
                return true;
            }
        }

        private static void PlaySound(int index)
        {
            object window = Reflect.Singleton("CreatureInfoWindow");
            object player = window == null ? null : Reflect.Get(window, "audioClipPlayer");
            if (player != null) Reflect.TryCall(player, "OnPlayInList", index);
        }

        /// <summary>Binah's "extract E.G.O" missions count Archipelago extractions too.</summary>
        private static void AdvanceExtractionMissions(object meta)
        {
            object risk = Reflect.TryCall(meta, "GetRiskLevel");
            int grade = risk == null ? 0 : Convert.ToInt32(risk, CultureInfo.InvariantCulture);
            // RiskLevel enum: ZAYIN = 0 ... ALEPH = 4 (GetRiskLevel on the creature model is 1-based in some versions)
            if (grade < 2) return;
            foreach (object mission in GameApi.MissionsInProgress())
            {
                if (GameApi.MissionCondition(mission) != "MAKE_EQUIPMENT") continue;
                object condition = Reflect.Get(mission, "successCondition");
                if (condition == null) continue;
                Reflect.Set(condition, Reflect.GetInt(condition, "current") + 1, "current");
                Reflect.TryCall(mission, "CheckSuccess");
            }
        }
    }
}
