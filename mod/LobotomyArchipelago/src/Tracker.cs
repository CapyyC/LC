using System;
using System.Collections.Generic;
using System.Globalization;

namespace LobotomyArchipelago
{
    /// <summary>Detects completed locations by looking at the game state (robust against save/load and missed hooks).</summary>
    public static class Tracker
    {
        private static readonly int[] SuppressionSefiras = { 0, 1, 2, 3, 4, 6, 7, 8, 9 };

        public static void Sweep()
        {
            if (!Plugin.Session.HasSlot || !GameApi.InFacility) return;
            UpdateContainmentOrder();
            SweepContainment();
            SweepDays();
            SweepMissions(false);
            SweepSuppressions();
            if (Plugin.Session.GiftChecks) SweepGifts();
        }

        /// <summary>Assigns containment indices (#01, #02, ...) to abnormalities in the order they appear.</summary>
        public static void UpdateContainmentOrder()
        {
            Session session = Plugin.Session;
            var creatures = new List<object>(GameApi.Creatures());
            creatures.Sort((a, b) => GameApi.InstanceId(a).CompareTo(GameApi.InstanceId(b)));
            foreach (object creature in creatures)
            {
                long id = GameApi.CreatureId(creature);
                if (id <= 0 || GameApi.IsTransient(id)) continue;
                long tracking = GameApi.TrackingId(id);
                if (session.ContainmentOrder.Contains(tracking)) continue;
                session.ContainmentOrder.Add(tracking);
                session.Dirty = true;
                int index = session.ContainmentOrder.Count;
                if (index <= session.ContainmentCount)
                {
                    Plugin.Message(GameApi.CreatureName(id) + " is Abnormality #" + index.ToString("00"), false);
                }
            }
        }

        private static void SweepContainment()
        {
            Session session = Plugin.Session;
            foreach (object creature in GameApi.Creatures())
            {
                long id = GameApi.CreatureId(creature);
                int index = session.ContainmentIndexOf(id);
                if (index < 1 || index > session.ContainmentCount) continue;
                long tracking = GameApi.TrackingId(id);
                int level = Math.Min(4, GameApi.ObservationLevel(creature));
                if (tracking == GameApi.DontTouchMeId && session.DontTouchMeCrashed) level = 4;
                for (int l = 1; l <= Math.Min(level, session.ObservationChecks); l++)
                {
                    session.AddCheck(Ids.Observation(index, l));
                }
                if (level < 4) continue;

                // Abnormalities without a weapon / suit / gift complete those checks at full observation.
                object meta = Reflect.Get(creature, "metaInfo") ?? GameApi.CreatureMeta(id);
                bool tool = GameApi.IsTool(id);
                bool hasWeapon = !tool && GameApi.FindMakeInfo(meta, "WEAPON") != null;
                bool hasSuit = !tool && GameApi.FindMakeInfo(meta, "ARMOR") != null;
                bool dontTouchMe = tracking == GameApi.DontTouchMeId && session.DontTouchMeCrashed;
                for (int copy = 1; copy <= session.EgoChecks; copy++)
                {
                    if (!hasWeapon || dontTouchMe) session.AddCheck(Ids.EgoWeapon(index, copy));
                    if (!hasSuit || dontTouchMe) session.AddCheck(Ids.EgoSuit(index, copy));
                }
                if (session.GiftChecks && (tool || dontTouchMe || GameApi.FindMakeInfo(meta, "SPECIAL") == null))
                {
                    session.AddCheck(Ids.Gift(index));
                }
            }
        }

        private static void SweepDays()
        {
            Session session = Plugin.Session;
            int dayIndex = GameApi.DayIndex;
            for (int day = 1; day <= Math.Min(dayIndex, session.GoalDay); day++)
            {
                session.AddCheck(Ids.Day(day));
            }
        }

        public static void OnDayCleared()
        {
            Session session = Plugin.Session;
            if (!session.HasSlot) return;
            int day = GameApi.DayIndex + 1;
            if (day >= 1 && day <= session.GoalDay && session.AddCheck(Ids.Day(day)))
            {
                Plugin.Message("Day " + day + " cleared!", false);
            }
            Sweep();
            CheckGoal();
            session.Save();
        }

        /// <summary>Sends the checks of cleared (non Core Suppression) missions; optionally closes them.</summary>
        public static void SweepMissions(bool closeForResearch)
        {
            Session session = Plugin.Session;
            Dictionary<string, object> missions = session.SlotData.Obj("missions");
            var closed = new HashSet<int>();
            foreach (object mission in GameApi.ClearedMissions())
            {
                if (GameApi.MissionCondition(mission) == "DESTROY_CORE") continue;
                int missionId = GameApi.MissionId(mission);
                List<object> info = missions == null ? null : missions.Arr(missionId.ToString(CultureInfo.InvariantCulture));
                if (info != null && info.Count >= 3)
                {
                    if (session.AddCheck(JsonExt.ToLong(info[2])))
                    {
                        Plugin.Message("Sephirah mission completed (mission " + missionId + ")");
                    }
                }
                else
                {
                    Reflect.ReportOnce("mission:" + missionId, "Unknown mission id " + missionId);
                }
                int sefira = GameApi.MissionSefira(mission);
                if (closeForResearch && sefira >= 0 && closed.Add(sefira))
                {
                    GameApi.CloseClearedMissionExceptBoss(sefira);
                }
            }
        }

        private static void SweepSuppressions()
        {
            Session session = Plugin.Session;
            Dictionary<string, object> suppressions = session.SlotData.Obj("suppressions");
            foreach (int sefira in SuppressionSefiras)
            {
                long location = suppressions == null
                    ? Ids.Suppression(sefira)
                    : suppressions.Long(sefira.ToString(CultureInfo.InvariantCulture), Ids.Suppression(sefira));
                if (session.IsChecked(location) || !session.IsAllowed(location)) continue;
                if (GameApi.IsBossFinished(sefira) && session.AddCheck(location))
                {
                    Plugin.Message("Core Suppression completed!");
                }
            }
        }

        public static int SuppressionsDone()
        {
            int done = 0;
            foreach (int sefira in SuppressionSefiras)
            {
                if (GameApi.IsBossFinished(sefira)) done++;
            }
            return done;
        }

        private static Dictionary<int, long> _giftOwners;

        private static void SweepGifts()
        {
            Session session = Plugin.Session;
            if (_giftOwners == null || _giftOwners.Count < session.ContainmentOrder.Count)
            {
                _giftOwners = new Dictionary<int, long>();
                foreach (long creatureId in session.ContainmentOrder)
                {
                    object info = GameApi.FindMakeInfo(GameApi.CreatureMeta(creatureId), "SPECIAL");
                    object typeInfo = info == null ? null : Reflect.Get(info, "equipTypeInfo");
                    int giftId = typeInfo == null ? (int)(400000 + creatureId - 100000) : Reflect.GetInt(typeInfo, "id");
                    _giftOwners[giftId] = creatureId;
                }
            }
            foreach (object agent in GameApi.Agents())
            {
                object gifts = Reflect.TryCall(agent, "GetAllGifts");
                foreach (object gift in Reflect.Enumerate(gifts))
                {
                    long creatureId;
                    if (!_giftOwners.TryGetValue(GameApi.EquipmentId(gift), out creatureId)) continue;
                    int index = session.ContainmentIndexOf(creatureId);
                    if (index >= 1 && index <= session.ContainmentCount) session.AddCheck(Ids.Gift(index));
                }
            }
        }

        public static void OnDontTouchMeCrash()
        {
            Session session = Plugin.Session;
            session.DontTouchMeCrashed = true;
            int index = session.ContainmentIndexOf(GameApi.DontTouchMeId);
            if (index >= 1 && index <= session.ContainmentCount)
            {
                for (int level = 1; level <= session.ObservationChecks; level++)
                    session.AddCheck(Ids.Observation(index, level));
                for (int copy = 1; copy <= session.EgoChecks; copy++)
                {
                    session.AddCheck(Ids.EgoWeapon(index, copy));
                    session.AddCheck(Ids.EgoSuit(index, copy));
                }
                if (session.GiftChecks) session.AddCheck(Ids.Gift(index));
            }
            session.Save();
            if (Plugin.Runner != null) Plugin.Runner.FlushChecks();
        }

        public static void OnFinalDayCleared()
        {
            Session session = Plugin.Session;
            if (!session.HasSlot) return;
            for (int day = 1; day <= Math.Min(50, session.GoalDay); day++) session.AddCheck(Ids.Day(day));
            CheckGoal();
            session.Save();
            if (Plugin.Runner != null) Plugin.Runner.FlushChecks();
        }

        public static bool GoalReached()
        {
            Session session = Plugin.Session;
            if (!session.HasSlot) return false;
            if (!session.IsChecked(Ids.Day(session.GoalDay))) return false;
            return session.RequiredSuppressions <= 0 || SuppressionsDone() >= session.RequiredSuppressions;
        }

        public static void CheckGoal()
        {
            Session session = Plugin.Session;
            if (!GoalReached()) return;
            if (!session.GoalReported) Plugin.Message("Goal complete! Congratulations, manager.");
            session.GoalReported = true;
            session.Dirty = true;
            if (Plugin.Bridge != null && Plugin.Bridge.Connected)
            {
                Plugin.Bridge.Send(new Dictionary<string, object> { { "cmd", "Goal" } });
            }
        }
    }
}
