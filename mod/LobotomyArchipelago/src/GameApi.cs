using System;
using System.Collections.Generic;
using System.Globalization;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Thin, defensive facade over the game's classes. Every call goes through <see cref="Reflect"/> and fails soft:
    /// a missing member is logged once and the feature using it is skipped.
    /// </summary>
    public static class GameApi
    {
        public const long WhiteNightId = 100015L;
        public const long PlagueDoctorId = 100014L;
        public const long DontTouchMeId = 100024L;
        public const long ToolIdStart = 300000L;

        /// <summary>Creatures that can appear in the facility without being extracted (event abnormalities).</summary>
        public static bool IsTransient(long id)
        {
            return id == 100038L; // Apocalypse Bird
        }

        // ------------------------------------------------------------------------------------------------------------
        // General state
        // ------------------------------------------------------------------------------------------------------------

        public static object Player { get { return Reflect.Singleton("PlayerModel"); } }

        /// <summary>0-based index of the current day, or -1 when unknown.</summary>
        public static int DayIndex
        {
            get
            {
                object player = Player;
                if (player == null) return -1;
                object day = Reflect.TryCall(player, "GetDay");
                return day == null ? -1 : Convert.ToInt32(day, CultureInfo.InvariantCulture);
            }
        }

        public static object GameManager
        {
            get
            {
                Type type = Reflect.Type("GameManager");
                return type == null ? null : Reflect.Get(type, "currentGameManager");
            }
        }

        /// <summary>True while a facility (deployment or management phase) is loaded.</summary>
        public static bool InFacility { get { return Reflect.IsAlive(GameManager); } }

        public static string GameStateName
        {
            get
            {
                object manager = GameManager;
                if (!Reflect.IsAlive(manager)) return "";
                object state = Reflect.Get(manager, "state");
                return state == null ? "" : state.ToString();
            }
        }

        public static void SystemLog(string text)
        {
            try
            {
                object notice = Reflect.Singleton("Notice");
                if (notice != null) Reflect.Call(notice, "Send", "AddSystemLog", new object[] { text });
            }
            catch (Exception e)
            {
                Reflect.ReportOnce("systemlog", "Could not write to the in-game system log: " + e.Message);
            }
        }

        public static void Notify(string noticeName)
        {
            object notice = Reflect.Singleton("Notice");
            if (notice != null) Reflect.TryCall(notice, "Send", noticeName, new object[0]);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Abnormalities
        // ------------------------------------------------------------------------------------------------------------

        public static List<object> Creatures()
        {
            object manager = Reflect.Singleton("CreatureManager");
            return manager == null ? new List<object>() : Reflect.ToList(Reflect.TryCall(manager, "GetCreatureList"));
        }

        public static long CreatureId(object creature)
        {
            if (creature == null) return -1;
            if (Reflect.Has(creature, "metadataId")) return Reflect.GetLong(creature, "metadataId");
            object meta = Reflect.Get(creature, "metaInfo");
            return meta == null ? -1 : Reflect.GetLong(meta, "id");
        }

        /// <summary>The id used for tracking: WhiteNight counts as the Plague Doctor it came from.</summary>
        public static long TrackingId(long id)
        {
            return id == WhiteNightId ? PlagueDoctorId : id;
        }

        public static long InstanceId(object unit)
        {
            return Reflect.Has(unit, "instanceId") ? Reflect.GetLong(unit, "instanceId") : 0L;
        }

        public static int ObservationLevel(object creature)
        {
            object level = Reflect.TryCall(creature, "GetObservationLevel");
            return level == null ? 0 : Convert.ToInt32(level, CultureInfo.InvariantCulture);
        }

        public static bool IsTool(long id)
        {
            return id >= ToolIdStart;
        }

        public static object CreatureMeta(long id)
        {
            object list = Reflect.Singleton("CreatureTypeList");
            return list == null ? null : Reflect.TryCall(list, "GetData", id);
        }

        public static string CreatureName(long id)
        {
            object meta = CreatureMeta(id);
            if (meta == null) return "#" + id;
            object name = Reflect.Get(meta, "name", "collectionName");
            return name == null ? "#" + id : name.ToString();
        }

        /// <summary>Finds the E.G.O making info of the given kind ("WEAPON" or "ARMOR") on a creature type.</summary>
        public static object FindMakeInfo(object meta, string kind)
        {
            if (meta == null) return null;
            foreach (object info in Reflect.Enumerate(Reflect.Get(meta, "equipMakeInfos")))
            {
                object typeInfo = Reflect.Get(info, "equipTypeInfo");
                object type = typeInfo == null ? null : Reflect.Get(typeInfo, "type");
                if (type != null && type.ToString() == kind) return info;
            }
            return null;
        }

        public static int EquipmentTypeId(long creatureId, string kind)
        {
            object info = FindMakeInfo(CreatureMeta(creatureId), kind);
            object typeInfo = info == null ? null : Reflect.Get(info, "equipTypeInfo");
            if (typeInfo != null) return Reflect.GetInt(typeInfo, "id");
            // Fallback to the id convention: 2xxxxx weapons, 3xxxxx armor, 4xxxxx gifts.
            int suffix = (int)(creatureId - 100000L);
            return (kind == "WEAPON" ? 200000 : kind == "ARMOR" ? 300000 : 400000) + suffix;
        }

        public static object ObserveInfo(object creature)
        {
            return Reflect.Get(creature, "observeInfo", "ObserveInfo", "_observeInfo");
        }

        /// <summary>Adds unique PE boxes to an abnormality's information stash.</summary>
        public static bool AddCubes(object creature, int amount)
        {
            object observe = ObserveInfo(creature);
            if (observe == null) return false;
            if (Reflect.Method(observe.GetType(), "Transaction", 1) != null)
            {
                Reflect.TryCall(observe, "Transaction", amount);
                return true;
            }
            int current = Reflect.GetInt(observe, "cubeNum", "totalCubeNum");
            return Reflect.Set(observe, current + amount, "cubeNum", "totalCubeNum");
        }

        public static bool IsWaitingCreature(long id)
        {
            object player = Player;
            object result = player == null ? null : Reflect.TryCall(player, "IsWaitingCreature", id);
            return result is bool && (bool)result;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Inventory / E.G.O
        // ------------------------------------------------------------------------------------------------------------

        public static object Inventory { get { return Reflect.Singleton("InventoryModel"); } }

        public static List<object> Equipment()
        {
            object inventory = Inventory;
            if (inventory == null) return new List<object>();
            object list = Reflect.Has(inventory, "equipList")
                ? Reflect.Get(inventory, "equipList")
                : Reflect.TryCall(inventory, "GetAllEquipmentList");
            return Reflect.ToList(list);
        }

        public static int EquipmentId(object equipment)
        {
            object meta = Reflect.Get(equipment, "metaInfo");
            return meta == null ? -1 : Reflect.GetInt(meta, "id");
        }

        public static int EquipmentGrade(object equipment)
        {
            object meta = Reflect.Get(equipment, "metaInfo");
            if (meta == null) return 0;
            object grade = Reflect.Get(meta, "Grade", "grade");
            if (grade == null) return 0;
            try
            {
                return Convert.ToInt32(grade, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int CountEquipment(int typeId)
        {
            int count = 0;
            foreach (object equipment in Equipment())
            {
                if (EquipmentId(equipment) == typeId) count++;
            }
            return count;
        }

        public static object CreateEquipment(int typeId)
        {
            object inventory = Inventory;
            if (inventory == null) return null;
            object created = null;
            if (Reflect.Method(inventory.GetType(), "CreateEquipmentForcely", 1) != null)
            {
                created = Reflect.TryCall(inventory, "CreateEquipmentForcely", typeId);
            }
            if (created == null) created = Reflect.TryCall(inventory, "CreateEquipment", typeId);
            return created;
        }

        public static void RemoveEquipment(object equipment)
        {
            object inventory = Inventory;
            if (inventory != null) Reflect.TryCall(inventory, "RemoveEquipment", equipment);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Resources
        // ------------------------------------------------------------------------------------------------------------

        public static bool AddLob(int amount)
        {
            object money = Reflect.Singleton("MoneyModel");
            if (money == null) return false;
            Reflect.TryCall(money, "Add", amount);
            return true;
        }

        public static float Energy()
        {
            object energy = Reflect.Singleton("EnergyModel");
            object value = energy == null ? null : Reflect.TryCall(energy, "GetEnergy");
            return value == null ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        public static void AddEnergy(float amount)
        {
            object energy = Reflect.Singleton("EnergyModel");
            if (energy == null) return;
            if (amount >= 0) Reflect.TryCall(energy, "AddEnergy", amount);
            else Reflect.TryCall(energy, "SubEnergy", -amount);
        }

        /// <summary>Today's energy quota, with an estimate as fallback.</summary>
        public static float EnergyQuota()
        {
            int day = Math.Max(0, DayIndex);
            object stage = Reflect.Singleton("StageTypeInfo");
            if (stage != null)
            {
                object need = Reflect.TryCall(stage, "GetEnergyNeed", day);
                if (need != null) return Convert.ToSingle(need, CultureInfo.InvariantCulture);
            }
            return 100f + 40f * day;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Employees and departments
        // ------------------------------------------------------------------------------------------------------------

        public static List<object> Agents()
        {
            object manager = Reflect.Singleton("AgentManager");
            return manager == null ? new List<object>() : Reflect.ToList(Reflect.TryCall(manager, "GetAgentList"));
        }

        public static List<object> Workers()
        {
            object manager = Reflect.Singleton("WorkerManager");
            List<object> workers = manager == null ? null : Reflect.ToList(Reflect.TryCall(manager, "GetWorkerList"));
            return workers == null || workers.Count == 0 ? Agents() : workers;
        }

        public static bool IsDead(object unit)
        {
            object dead = Reflect.TryCall(unit, "IsDead");
            return dead is bool && (bool)dead;
        }

        public static bool IsPanic(object unit)
        {
            object panic = Reflect.TryCall(unit, "IsPanic");
            return panic is bool && (bool)panic;
        }

        public static object Sefira(int sefiraEnum)
        {
            object manager = Reflect.Singleton("SefiraManager");
            if (manager == null) return null;
            object value = Reflect.EnumValue("SefiraEnum", Enum.GetName(typeof(SefiraIds), sefiraEnum) ?? "");
            return value == null ? null : Reflect.TryCall(manager, "GetSefira", value);
        }

        public static object SefiraByName(string name)
        {
            object manager = Reflect.Singleton("SefiraManager");
            return manager == null ? null : Reflect.TryCall(manager, "GetSefira", name);
        }

        public static List<object> ActivatedSefiras()
        {
            object manager = Reflect.Singleton("SefiraManager");
            return manager == null ? new List<object>() : Reflect.ToList(Reflect.TryCall(manager, "GetActivatedSefiras"));
        }

        public static int SefiraEnumOf(object sefira)
        {
            object value = Reflect.Get(sefira, "sefiraEnum");
            return value == null ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static List<object> AgentsOf(object sefira)
        {
            return Reflect.ToList(Reflect.Get(sefira, "agentList"));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Research
        // ------------------------------------------------------------------------------------------------------------

        public static object ResearchModel { get { return Reflect.Singleton("ResearchDataModel"); } }

        public static List<object> ResearchOfSefira(int sefiraEnum)
        {
            object model = ResearchModel;
            object sefira = Sefira(sefiraEnum);
            if (model == null) return new List<object>();
            string index = sefira == null ? null : Reflect.Get(sefira, "indexString") as string;
            if (index == null) index = DefaultIndexString(sefiraEnum);
            var result = Reflect.ToList(Reflect.TryCall(model, "GetModelBySefira", index));
            result.Sort((a, b) => ResearchId(a).CompareTo(ResearchId(b)));
            return result;
        }

        private static string DefaultIndexString(int sefiraEnum)
        {
            switch (sefiraEnum)
            {
                case 0: return "1";
                case 1: return "4";
                case 2: return "3";
                case 3: return "2";
                case 4: return "5";
                case 5: return "6";
                case 6: return "7";
                case 7: return "8";
                case 8: return "9";
                case 9: return "10";
                default: return (sefiraEnum + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        public static int ResearchId(object research)
        {
            object info = Reflect.Get(research, "info");
            return info == null ? -1 : Reflect.GetInt(info, "id");
        }

        public static string ResearchName(object research)
        {
            object info = Reflect.Get(research, "info");
            object desc = info == null ? null : Reflect.TryCall(info, "GetDesc");
            object name = desc == null ? null : Reflect.Get(desc, "name");
            return name == null ? "" : name.ToString();
        }

        public static bool IsResearchUpgraded(object research)
        {
            object level = Reflect.Get(research, "curLevel", "level");
            if (level != null)
            {
                try
                {
                    return Convert.ToInt32(level, CultureInfo.InvariantCulture) > 0;
                }
                catch (Exception)
                {
                    // fall through
                }
            }
            object upgraded = Reflect.Get(research, "upgraded", "isUpgraded");
            return upgraded is bool && (bool)upgraded;
        }

        public static HashSet<int> UpgradedResearchIds(int sefiraEnum)
        {
            var ids = new HashSet<int>();
            object model = ResearchModel;
            object sefiraValue = Reflect.EnumValue("SefiraEnum", Enum.GetName(typeof(SefiraIds), sefiraEnum) ?? "");
            if (model == null || sefiraValue == null) return ids;
            foreach (object research in Reflect.Enumerate(
                         Reflect.TryCall(model, "GetUpgradedResearchListBySefira", sefiraValue)))
            {
                ids.Add(ResearchId(research));
            }
            return ids;
        }

        public static bool UpgradeResearch(int id)
        {
            object model = ResearchModel;
            if (model == null) return false;
            object result = Reflect.TryCall(model, "UpgradeResearch", id, true);
            if (result is bool && (bool)result)
            {
                try
                {
                    Type noticeName = Reflect.Type("NoticeName");
                    string update = noticeName == null ? null : Reflect.Get(noticeName, "UpdateResearchItem") as string;
                    if (update != null)
                    {
                        object name = Reflect.Call(noticeName, "MakeName", update,
                            new[] { id.ToString(CultureInfo.InvariantCulture) });
                        if (name != null) Notify(name.ToString());
                    }
                }
                catch (Exception e)
                {
                    Reflect.ReportOnce("research-notice", "Research notice failed: " + e.Message);
                }
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Missions
        // ------------------------------------------------------------------------------------------------------------

        public static object MissionManager { get { return Reflect.Singleton("MissionManager"); } }

        public static List<object> ClearedMissions()
        {
            object manager = MissionManager;
            return manager == null ? new List<object>() : Reflect.ToList(Reflect.TryCall(manager, "GetClearedMissions"));
        }

        public static List<object> MissionsInProgress()
        {
            object manager = MissionManager;
            return manager == null
                ? new List<object>()
                : Reflect.ToList(Reflect.TryCall(manager, "GetMissionsInProgress"));
        }

        public static int MissionId(object mission)
        {
            object meta = Reflect.Get(mission, "metaInfo");
            return meta == null ? -1 : Reflect.GetInt(meta, "id");
        }

        public static int MissionSefira(object mission)
        {
            object meta = Reflect.Get(mission, "metaInfo");
            return meta == null ? -1 : Reflect.GetInt(meta, "sefira");
        }

        public static string MissionCondition(object mission)
        {
            object condition = Reflect.Get(mission, "successCondition");
            object type = condition == null ? null : Reflect.Get(condition, "condition_Type");
            return type == null ? "" : type.ToString();
        }

        public static bool IsBossFinished(int sefiraEnum)
        {
            object manager = MissionManager;
            object sefiraValue = Reflect.EnumValue("SefiraEnum", Enum.GetName(typeof(SefiraIds), sefiraEnum) ?? "");
            if (manager == null || sefiraValue == null) return false;
            object result = Reflect.TryCall(manager, "ExistsFinishedBossMission", sefiraValue);
            return result is bool && (bool)result;
        }

        public static void CloseClearedMissionExceptBoss(int sefiraEnum)
        {
            object manager = MissionManager;
            object sefiraValue = Reflect.EnumValue("SefiraEnum", Enum.GetName(typeof(SefiraIds), sefiraEnum) ?? "");
            if (manager != null && sefiraValue != null)
            {
                Reflect.TryCall(manager, "CloseClearedMission_ExceptBoss", sefiraValue);
            }
        }
    }

    /// <summary>Mirror of the game's SefiraEnum (names must match the game's enum).</summary>
    public enum SefiraIds
    {
        MALKUT = 0,
        YESOD = 1,
        HOD = 2,
        NETZACH = 3,
        TIPERERTH1 = 4,
        TIPERERTH2 = 5,
        GEBURAH = 6,
        CHESED = 7,
        BINAH = 8,
        CHOKHMAH = 9,
        KETHER = 10,
        DAAT = 11
    }
}
