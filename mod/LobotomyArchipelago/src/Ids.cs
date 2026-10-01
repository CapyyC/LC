namespace LobotomyArchipelago
{
    /// <summary>Archipelago id layout. Must match worlds/lobotomy_corporation/data.py.</summary>
    public static class Ids
    {
        public static long Base = 568220000L;

        public const long AbnormalityOffset = 0L;
        public const long EgoWeaponOffset = 1000000L;
        public const long EgoSuitOffset = 2000000L;
        public const long ResearchOffset = 3000000L;
        public const long AgentSlotOffset = 4000000L;
        public const long FillerOffset = 5000000L;
        public const long TrapOffset = 6000000L;

        public const long ContainmentOffset = 10000000L;
        public const long DayOffset = 11000000L;
        public const long MissionOffset = 12000000L;
        public const long SuppressionOffset = 13000000L;

        public const int FillerLob = 1;
        public const int FillerPeBoxes = 2;
        public const int FillerEnkephalin = 3;
        public const int FillerTraining = 4;
        public const int FillerMedical = 5;

        public const int TrapDawn = 1;
        public const int TrapNoon = 2;
        public const int TrapDusk = 3;
        public const int TrapMidnight = 4;
        public const int TrapBreach = 5;
        public const int TrapMeltdown = 6;
        public const int TrapEnergyDrain = 7;
        public const int TrapPanic = 8;

        public static long Observation(int index, int level)
        {
            return Base + ContainmentOffset + index * 100L + level;
        }

        public static long EgoWeapon(int index, int copy)
        {
            return Base + ContainmentOffset + index * 100L + 10 + (copy - 1);
        }

        public static long EgoSuit(int index, int copy)
        {
            return Base + ContainmentOffset + index * 100L + 20 + (copy - 1);
        }

        public static long Gift(int index)
        {
            return Base + ContainmentOffset + index * 100L + 30;
        }

        public static long Day(int day)
        {
            return Base + DayOffset + day;
        }

        public static long Mission(int sefira, int number)
        {
            return Base + MissionOffset + sefira * 10L + number;
        }

        public static long Suppression(int sefira)
        {
            return Base + SuppressionOffset + sefira;
        }

        public enum ItemKind
        {
            Unknown,
            Abnormality,
            EgoWeapon,
            EgoSuit,
            Research,
            AgentSlot,
            Filler,
            Trap
        }

        /// <summary>Decodes an item id. <paramref name="value"/> receives the creature id / index part.</summary>
        public static ItemKind Kind(long itemId, out long value)
        {
            long offset = itemId - Base;
            value = 0;
            if (offset < 0) return ItemKind.Unknown;
            if (offset < EgoWeaponOffset)
            {
                value = offset;
                return ItemKind.Abnormality;
            }
            if (offset < EgoSuitOffset)
            {
                value = offset - EgoWeaponOffset;
                return ItemKind.EgoWeapon;
            }
            if (offset < ResearchOffset)
            {
                value = offset - EgoSuitOffset;
                return ItemKind.EgoSuit;
            }
            if (offset < AgentSlotOffset)
            {
                value = offset - ResearchOffset;
                return ItemKind.Research;
            }
            if (offset < FillerOffset)
            {
                value = offset - AgentSlotOffset;
                return ItemKind.AgentSlot;
            }
            if (offset < TrapOffset)
            {
                value = offset - FillerOffset;
                return ItemKind.Filler;
            }
            if (offset < ContainmentOffset)
            {
                value = offset - TrapOffset;
                return ItemKind.Trap;
            }
            return ItemKind.Unknown;
        }
    }
}
