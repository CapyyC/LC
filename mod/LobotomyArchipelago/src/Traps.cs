using System;
using System.Collections.Generic;
using System.Globalization;

namespace LobotomyArchipelago
{
    /// <summary>Trap and DeathLink effects. All of them only run during the management phase.</summary>
    public static class Traps
    {
        private static readonly System.Random Rng = new System.Random();

        // Ordeal classes of the game per tier (unknown class names are skipped).
        private static readonly string[][] OrdealClasses =
        {
            new[] { "MachineDawnOrdeal", "CircusDawnOrdeal", "BugDawnOrdeal", "OutterGodDawnOrdeal" },
            new[] { "MachineNoonOrdeal", "CircusNoonOrdeal", "OutterGodNoonOrdeal", "ScavengerNoonOrdeal" },
            new[] { "MachineDuskOrdeal", "CircusDuskOrdeal", "BugDuskOrdeal" },
            new[] { "MachineMidnightOrdeal", "BugMidnightOrdeal", "OutterGodMidnightOrdeal" },
        };
        private static readonly string[] OrdealLevels = { "DAWN", "NOON", "DUSK", "MIDNIGHT" };
        private static readonly string[] TierNames = { "Dawn", "Noon", "Dusk", "Midnight" };

        public static bool Fire(int trap)
        {
            try
            {
                switch (trap)
                {
                    case Ids.TrapDawn: return SpawnOrdeal(0);
                    case Ids.TrapNoon: return SpawnOrdeal(1);
                    case Ids.TrapDusk: return SpawnOrdeal(2);
                    case Ids.TrapMidnight: return SpawnOrdeal(3);
                    case Ids.TrapBreach: return Breach();
                    case Ids.TrapMeltdown: return Meltdown();
                    case Ids.TrapEnergyDrain: return EnergyDrain();
                    case Ids.TrapPanic: return Panic();
                    default: return false;
                }
            }
            catch (Exception e)
            {
                Log.Error("Trap " + trap + " failed", e);
                return false;
            }
        }

        /// <summary>Highest ordeal tier the current day allows (ordeals beyond it are downgraded).</summary>
        private static int MaxTierForDay(int day)
        {
            if (day >= 26) return 3;
            if (day >= 21) return 2;
            if (day >= 11) return 1;
            return 0;
        }

        public static bool SpawnOrdeal(int tier)
        {
            int day = GameApi.DayIndex + 1;
            tier = Math.Min(tier, MaxTierForDay(day));
            object manager = Reflect.Singleton("OrdealManager");
            if (manager == null) return false;

            for (int t = tier; t >= 0; t--)
            {
                var candidates = new List<Type>();
                foreach (string name in OrdealClasses[t])
                {
                    Type type = Reflect.Type(name);
                    if (type != null && type.GetConstructor(Type.EmptyTypes) != null) candidates.Add(type);
                }
                object ordeal = null;
                if (candidates.Count > 0)
                {
                    ordeal = Activator.CreateInstance(candidates[Rng.Next(candidates.Count)]);
                }
                else
                {
                    Type fixer = Reflect.Type("FixerOrdeal");
                    object level = Reflect.EnumValue("OrdealLevel", OrdealLevels[t]);
                    if (fixer != null && level != null) ordeal = Activator.CreateInstance(fixer, level);
                }
                if (ordeal == null) continue;
                Reflect.Call(manager, "ActivateOrdeal", ordeal, false);
                Plugin.Message("TRAP: an Ordeal of " + TierNames[t] + " has begun!");
                return true;
            }
            return false;
        }

        public static bool Breach()
        {
            var candidates = new List<object>();
            foreach (object creature in GameApi.Creatures())
            {
                long id = GameApi.CreatureId(creature);
                if (GameApi.IsTool(id)) continue;
                object counter = Reflect.Get(creature, "qliphothCounter");
                if (counter != null && Convert.ToInt32(counter, CultureInfo.InvariantCulture) <= 0) continue;
                candidates.Add(creature);
            }
            if (candidates.Count == 0) return false;
            object target = candidates[Rng.Next(candidates.Count)];
            Reflect.Call(target, "SetQliphothCounter", 0);
            Plugin.Message("TRAP: " + GameApi.CreatureName(GameApi.CreatureId(target)) + "'s Qliphoth counter hit 0!");
            return true;
        }

        public static bool Meltdown()
        {
            object manager = Reflect.Singleton("CreatureOverloadManager");
            if (manager == null) return false;
            object overloadType = Reflect.EnumValue("OverloadType", "DEFAULT");
            bool done = false;
            if (overloadType != null && Reflect.Method(manager.GetType(), "ActivateOverload", 7) != null)
            {
                Reflect.Call(manager, "ActivateOverload", 3, overloadType, 60f, true, true, false, new long[0]);
                done = true;
            }
            else if (Reflect.Method(manager.GetType(), "AddOverloadGague", 0) != null)
            {
                // (sic) the game's method name is misspelled
                for (int i = 0; i < 3; i++) Reflect.TryCall(manager, "AddOverloadGague");
                done = true;
            }
            if (done) Plugin.Message("TRAP: Qliphoth meltdown!");
            return done;
        }

        public static bool EnergyDrain()
        {
            float energy = GameApi.Energy();
            if (energy <= 0f) return true;
            GameApi.AddEnergy(-energy * 0.2f);
            Plugin.Message("TRAP: " + Math.Round(energy * 0.2f) + " energy was drained!");
            return true;
        }

        public static bool Panic()
        {
            var departments = new List<object>();
            foreach (object sefira in GameApi.ActivatedSefiras())
            {
                int sefiraEnum = GameApi.SefiraEnumOf(sefira);
                if (sefiraEnum < 0 || sefiraEnum == (int)SefiraIds.DAAT || sefiraEnum == (int)SefiraIds.KETHER) continue;
                if (GameApi.AgentsOf(sefira).Count > 0) departments.Add(sefira);
            }
            if (departments.Count == 0) return false;
            object target = departments[Rng.Next(departments.Count)];
            foreach (object agent in GameApi.AgentsOf(target))
            {
                if (GameApi.IsDead(agent) || GameApi.IsPanic(agent)) continue;
                Reflect.Set(agent, 0f, "mental");
                Reflect.TryCall(agent, "Panic");
            }
            Plugin.Message("TRAP: the agents of " + (Reflect.Get(target, "name") ?? "a department") + " panic!");
            return true;
        }

        // ------------------------------------------------------------------------------------------------------------
        // DeathLink
        // ------------------------------------------------------------------------------------------------------------

        public static bool DeathLinkEffect(int effect, string source)
        {
            try
            {
                switch (effect)
                {
                    case 1:
                        return Breach();
                    case 2:
                        return SpawnOrdeal(3);
                    default:
                    {
                        var alive = new List<object>();
                        foreach (object agent in GameApi.Agents())
                        {
                            if (!GameApi.IsDead(agent)) alive.Add(agent);
                        }
                        if (alive.Count == 0) return false;
                        object victim = alive[Rng.Next(alive.Count)];
                        Reflect.Call(victim, "Die");
                        Plugin.Message("DeathLink from " + source + ": an agent died.");
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("DeathLink effect failed", e);
                return false;
            }
        }
    }
}
