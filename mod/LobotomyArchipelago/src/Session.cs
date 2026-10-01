using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LobotomyArchipelago
{
    public sealed class ReceivedItem
    {
        public long Item;
        public long Location;
        public int Player;
        public string Name;
        public string Sender;
    }

    /// <summary>
    /// Everything the mod knows about the current Archipelago slot: slot data, received items, location checks and
    /// the persistent per-seed progress (stored in LocalLow/Project_Moon/Lobotomy/Archipelago).
    /// </summary>
    public sealed class Session
    {
        // ----- From the client -----
        public bool ServerConnected;
        public string Seed = "";
        public int Slot = -1;
        public string SlotName = "";
        public Dictionary<string, object> SlotData = new Dictionary<string, object>();
        public readonly HashSet<long> AllowedLocations = new HashSet<long>();
        public readonly HashSet<long> ServerChecked = new HashSet<long>();
        public readonly List<ReceivedItem> Items = new List<ReceivedItem>();

        // ----- Persistent progress -----
        public int ProcessedOneShots;
        public readonly List<long> ContainmentOrder = new List<long>();
        public readonly HashSet<long> LocalChecks = new HashSet<long>();
        public readonly List<long> PendingFillers = new List<long>();
        public readonly List<long> PendingTraps = new List<long>();
        public bool DontTouchMeCrashed;
        public bool GoalReported;

        // ----- Derived from items -----
        public readonly List<long> ReceivedAbnormalities = new List<long>();
        public readonly HashSet<long> ReceivedResearch = new HashSet<long>();
        public readonly Dictionary<int, int> AgentSlotItems = new Dictionary<int, int>();
        public readonly Dictionary<long, int> ExpectedWeapons = new Dictionary<long, int>();
        public readonly Dictionary<long, int> ExpectedSuits = new Dictionary<long, int>();

        public bool Dirty;
        public bool HasSlot { get { return Slot >= 0 && SlotData.Count > 0; } }

        // ----- Slot data accessors -----
        public int GoalDay { get { return SlotData.Int("goal_day", 25); } }
        public int RequiredSuppressions { get { return SlotData.Int("required_core_suppressions"); } }
        public int ContainmentCount { get { return SlotData.Int("containment_count", 48); } }
        public int ObservationChecks { get { return SlotData.Int("observation_checks", 4); } }
        public int EgoChecks { get { return SlotData.Int("ego_extraction_checks", 1); } }
        public bool GiftChecks { get { return SlotData.Bool("ego_gift_checks"); } }
        public bool ExtractionAfterChecksVanilla { get { return SlotData.Int("extraction_after_checks") == 1; } }
        public int ExtractionOffers { get { return Math.Max(1, SlotData.Int("extraction_offers", 3)); } }
        public bool ResearchRandomized { get { return SlotData.Bool("randomize_research", true); } }
        public bool AgentSlotsRandomized { get { return SlotData.Bool("randomize_agent_slots", true); } }
        public int StartingAgentSlots { get { return SlotData.Int("starting_agent_slots", 2); } }
        public int LobPerItem { get { return SlotData.Int("lob_points_per_item", 4); } }
        public int PeBoxesPerItem { get { return SlotData.Int("pe_boxes_per_item", 30); } }
        public bool DeathLink { get { return SlotData.Bool("death_link"); } }
        public int DeathLinkTrigger { get { return SlotData.Int("death_link_trigger"); } }
        public int DeathLinkEffect { get { return SlotData.Int("death_link_effect"); } }

        public HashSet<long> AbnormalityPool
        {
            get
            {
                var pool = new HashSet<long>();
                List<object> list = SlotData.Arr("abnormality_pool");
                if (list != null) foreach (object id in list) pool.Add(JsonExt.ToLong(id));
                return pool;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Messages from the client
        // ------------------------------------------------------------------------------------------------------------

        public void OnConnected(Dictionary<string, object> message)
        {
            string seed = message.Str("seed");
            int slot = message.Int("slot", -1);
            if (seed != Seed || slot != Slot)
            {
                Save();
                Reset();
                Seed = seed;
                Slot = slot;
                Load();
            }
            ServerConnected = true;
            SlotName = message.Str("slot_name");
            SlotData = message.Obj("slot_data") ?? new Dictionary<string, object>();
            Ids.Base = SlotData.Long("base_id", Ids.Base);
            AllowedLocations.Clear();
            ServerChecked.Clear();
            foreach (object id in message.Arr("missing_locations") ?? new List<object>())
                AllowedLocations.Add(JsonExt.ToLong(id));
            foreach (object id in message.Arr("checked_locations") ?? new List<object>())
            {
                long location = JsonExt.ToLong(id);
                AllowedLocations.Add(location);
                ServerChecked.Add(location);
            }
            Dirty = true;
            SaveLastSession();
        }

        /// <summary>Returns false when the item list has a gap and a resync is needed.</summary>
        public bool OnReceivedItems(Dictionary<string, object> message)
        {
            int index = message.Int("index");
            if (index == 0) Items.Clear();
            else if (index != Items.Count) return false;
            foreach (object raw in message.Arr("items") ?? new List<object>())
            {
                var entry = raw as Dictionary<string, object>;
                if (entry == null) continue;
                Items.Add(new ReceivedItem
                {
                    Item = entry.Long("item"),
                    Location = entry.Long("location"),
                    Player = entry.Int("player"),
                    Name = entry.Str("item_name"),
                    Sender = entry.Str("sender")
                });
            }
            RebuildDerived();
            Dirty = true;
            return true;
        }

        public void OnLocationsChecked(Dictionary<string, object> message)
        {
            foreach (object id in message.Arr("locations") ?? new List<object>())
            {
                long location = JsonExt.ToLong(id);
                ServerChecked.Add(location);
                AllowedLocations.Add(location);
            }
        }

        public void RebuildDerived()
        {
            ReceivedAbnormalities.Clear();
            ReceivedResearch.Clear();
            AgentSlotItems.Clear();
            ExpectedWeapons.Clear();
            ExpectedSuits.Clear();
            Dictionary<string, object> slotItems = SlotData.Obj("agent_slot_items");
            foreach (ReceivedItem item in Items)
            {
                long value;
                switch (Ids.Kind(item.Item, out value))
                {
                    case Ids.ItemKind.Abnormality:
                        if (!ReceivedAbnormalities.Contains(value)) ReceivedAbnormalities.Add(value);
                        break;
                    case Ids.ItemKind.Research:
                        ReceivedResearch.Add(item.Item);
                        break;
                    case Ids.ItemKind.AgentSlot:
                        int sefira = slotItems == null ? (int)value : slotItems.Int(item.Item.ToString(CultureInfo.InvariantCulture), (int)value);
                        int count;
                        AgentSlotItems.TryGetValue(sefira, out count);
                        AgentSlotItems[sefira] = count + 1;
                        break;
                    case Ids.ItemKind.EgoWeapon:
                        Increment(ExpectedWeapons, value);
                        break;
                    case Ids.ItemKind.EgoSuit:
                        Increment(ExpectedSuits, value);
                        break;
                }
            }
        }

        private static void Increment(Dictionary<long, int> dict, long key)
        {
            int count;
            dict.TryGetValue(key, out count);
            dict[key] = count + 1;
        }

        public int AgentSlotCap(int sefiraEnum)
        {
            if (!AgentSlotsRandomized) return 5;
            int items;
            AgentSlotItems.TryGetValue(sefiraEnum, out items);
            return Math.Min(5, StartingAgentSlots + items);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Location checks
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>Records a location check. Unknown locations are ignored once the slot's location list is known.</summary>
        public bool AddCheck(long location)
        {
            if (AllowedLocations.Count > 0 && !AllowedLocations.Contains(location)) return false;
            if (!LocalChecks.Add(location)) return false;
            Dirty = true;
            return true;
        }

        public bool IsChecked(long location)
        {
            return LocalChecks.Contains(location) || ServerChecked.Contains(location);
        }

        public bool IsAllowed(long location)
        {
            return AllowedLocations.Count == 0 || AllowedLocations.Contains(location);
        }

        public List<long> UnsentChecks()
        {
            var result = new List<long>();
            foreach (long location in LocalChecks)
            {
                if (!ServerChecked.Contains(location)) result.Add(location);
            }
            return result;
        }

        public int ContainmentIndexOf(long creatureId)
        {
            int index = ContainmentOrder.IndexOf(GameApi.TrackingId(creatureId));
            return index < 0 ? -1 : index + 1;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Persistence
        // ------------------------------------------------------------------------------------------------------------

        private void Reset()
        {
            ProcessedOneShots = 0;
            ContainmentOrder.Clear();
            LocalChecks.Clear();
            PendingFillers.Clear();
            PendingTraps.Clear();
            DontTouchMeCrashed = false;
            GoalReported = false;
            Items.Clear();
            AllowedLocations.Clear();
            ServerChecked.Clear();
            RebuildDerived();
        }

        private string FilePath
        {
            get
            {
                var safe = new StringBuilder();
                foreach (char c in Seed) safe.Append(char.IsLetterOrDigit(c) ? c : '_');
                return Path.Combine(Log.Directory, "save_" + safe + "_" + Slot + ".json");
            }
        }

        public void Save()
        {
            if (Slot < 0 || string.IsNullOrEmpty(Seed)) return;
            try
            {
                var items = new List<object>();
                foreach (ReceivedItem item in Items)
                {
                    items.Add(new Dictionary<string, object>
                    {
                        { "item", item.Item }, { "location", item.Location }, { "player", item.Player },
                        { "item_name", item.Name ?? "" }, { "sender", item.Sender ?? "" }
                    });
                }
                var data = new Dictionary<string, object>
                {
                    { "seed", Seed },
                    { "slot", Slot },
                    { "slot_name", SlotName },
                    { "processed", ProcessedOneShots },
                    { "containment", new List<long>(ContainmentOrder) },
                    { "checks", new List<long>(LocalChecks) },
                    { "allowed", new List<long>(AllowedLocations) },
                    { "server_checked", new List<long>(ServerChecked) },
                    { "pending_fillers", new List<long>(PendingFillers) },
                    { "pending_traps", new List<long>(PendingTraps) },
                    { "dont_touch_me", DontTouchMeCrashed },
                    { "goal", GoalReported },
                    { "slot_data", SlotData },
                    { "items", items }
                };
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, Json.Serialize(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
                Dirty = false;
            }
            catch (Exception e)
            {
                Log.Error("Could not save Archipelago progress", e);
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var data = Json.Parse(File.ReadAllText(FilePath)) as Dictionary<string, object>;
                if (data == null) return;
                ProcessedOneShots = data.Int("processed");
                foreach (object id in data.Arr("containment") ?? new List<object>()) ContainmentOrder.Add(JsonExt.ToLong(id));
                foreach (object id in data.Arr("checks") ?? new List<object>()) LocalChecks.Add(JsonExt.ToLong(id));
                foreach (object id in data.Arr("allowed") ?? new List<object>()) AllowedLocations.Add(JsonExt.ToLong(id));
                foreach (object id in data.Arr("server_checked") ?? new List<object>()) ServerChecked.Add(JsonExt.ToLong(id));
                foreach (object id in data.Arr("pending_fillers") ?? new List<object>()) PendingFillers.Add(JsonExt.ToLong(id));
                foreach (object id in data.Arr("pending_traps") ?? new List<object>()) PendingTraps.Add(JsonExt.ToLong(id));
                DontTouchMeCrashed = data.Bool("dont_touch_me");
                GoalReported = data.Bool("goal");
                SlotName = data.Str("slot_name");
                SlotData = data.Obj("slot_data") ?? new Dictionary<string, object>();
                Ids.Base = SlotData.Long("base_id", Ids.Base);
                Items.Clear();
                foreach (object raw in data.Arr("items") ?? new List<object>())
                {
                    var entry = raw as Dictionary<string, object>;
                    if (entry == null) continue;
                    Items.Add(new ReceivedItem
                    {
                        Item = entry.Long("item"), Location = entry.Long("location"), Player = entry.Int("player"),
                        Name = entry.Str("item_name"), Sender = entry.Str("sender")
                    });
                }
                RebuildDerived();
                Log.Info("Loaded Archipelago progress for seed " + Seed + " slot " + Slot + " (" + Items.Count +
                         " items, " + LocalChecks.Count + " checks)");
            }
            catch (Exception e)
            {
                Log.Error("Could not load Archipelago progress", e);
            }
        }

        private void SaveLastSession()
        {
            try
            {
                var data = new Dictionary<string, object> { { "seed", Seed }, { "slot", Slot } };
                File.WriteAllText(Path.Combine(Log.Directory, "last_session.json"), Json.Serialize(data));
            }
            catch (Exception e)
            {
                Log.Warn("Could not store last session: " + e.Message);
            }
        }

        /// <summary>Restores the last session so the game can be played (and checks queued) while offline.</summary>
        public void LoadLastSession()
        {
            try
            {
                string path = Path.Combine(Log.Directory, "last_session.json");
                if (!File.Exists(path)) return;
                var data = Json.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                if (data == null) return;
                Seed = data.Str("seed");
                Slot = data.Int("slot", -1);
                Load();
            }
            catch (Exception e)
            {
                Log.Warn("Could not load last session: " + e.Message);
            }
        }
    }
}
