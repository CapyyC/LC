from dataclasses import dataclass

from Options import (
    Choice,
    DeathLink,
    DefaultOnToggle,
    OptionGroup,
    OptionSet,
    PerGameCommonOptions,
    Range,
    StartInventoryPool,
    Toggle,
)

from .data import ALL_ABNORMALITIES, FINAL_DAY


class GoalDay(Range):
    """
    The day you have to clear to finish your game.

    Day 50 is the true ending and requires every Core Suppression (the game itself demands them on days 47-49).
    Locations that would only become available after this day are not created.
    """
    display_name = "Goal Day"
    range_start = 5
    range_end = FINAL_DAY
    default = 25


class RequiredCoreSuppressions(Range):
    """
    How many Sephirah Core Suppressions must be completed (in addition to clearing the goal day) to finish.

    Upper layer suppressions need a goal day of at least 29-30, middle layer 40 and lower layer 44. The value is
    lowered automatically if your goal day does not leave room for that many suppressions.
    """
    display_name = "Required Core Suppressions"
    range_start = 0
    range_end = 9
    default = 0


class StartingAbnormalities(Range):
    """How many abnormalities you start with. The first day needs one, every other extraction day needs another."""
    display_name = "Starting Abnormalities"
    range_start = 1
    range_end = 5
    default = 2


class StartWithOneSin(DefaultOnToggle):
    """One of your starting abnormalities is always One Sin and Hundreds of Good Deeds, like in the vanilla game."""
    display_name = "Start With One Sin"


class ToolAbnormalities(Range):
    """
    Number of Tool abnormalities in a full 48 abnormality pool; shorter games get proportionally fewer.
    Tools fill a containment unit like any other abnormality but have no E.G.O equipment.
    """
    display_name = "Tool Abnormalities"
    range_start = 0
    range_end = 17
    default = 4


class ExcludedAbnormalities(OptionSet):
    """Abnormalities that will never be added to your abnormality pool."""
    display_name = "Excluded Abnormalities"
    valid_keys = sorted(abno.name for abno in ALL_ABNORMALITIES)
    default = frozenset()


class PriorityAbnormalities(OptionSet):
    """Abnormalities that are always added to your abnormality pool, as long as there is room."""
    display_name = "Priority Abnormalities"
    valid_keys = sorted(abno.name for abno in ALL_ABNORMALITIES)
    default = frozenset()


class ObservationChecks(Range):
    """
    How many observation levels (1-4) of every contained abnormality are location checks.

    Checks are tied to containment order: "Abnormality #07 - Observation Level 2" is sent when the 7th abnormality
    you contained reaches observation level 2, whichever abnormality that is.
    """
    display_name = "Observation Checks"
    range_start = 1
    range_end = 4
    default = 4


class EGOExtractionChecks(Range):
    """
    How many E.G.O Weapon and E.G.O Suit extractions per contained abnormality are location checks.

    While an abnormality still has extraction checks left, spending PE boxes on its E.G.O sends a check instead of
    creating the equipment. If an abnormality has no weapon or suit, those checks are sent automatically once it
    reaches observation level 4. E.G.O equipment is shuffled into the item pool instead (one copy per check).
    """
    display_name = "E.G.O Extraction Checks"
    range_start = 0
    range_end = 3
    default = 1


class EGOGiftChecks(Toggle):
    """
    Obtaining the E.G.O Gift of each contained abnormality is a location check.
    Gifts are random rewards from work, so this adds a lot of grinding.
    """
    display_name = "E.G.O Gift Checks"


class ExtractionAfterChecks(Choice):
    """
    What happens when you extract E.G.O from an abnormality that has no extraction checks left.

    **blocked**: E.G.O can only be obtained from the item pool.
    **vanilla**: Extraction works normally once the abnormality's checks are done.
    """
    display_name = "E.G.O Extraction After Checks"
    option_blocked = 0
    option_vanilla = 1
    default = 0


class RandomizeResearch(DefaultOnToggle):
    """
    Department research (TT2 Protocol, shield bullets, execution bullets, Rabbit Team...) is shuffled into the item
    pool. Completing a Sephirah mission sends a check instead of letting you pick a research.
    """
    display_name = "Randomize Research"


class StartWithTT2Protocol(DefaultOnToggle):
    """Start with the TT2 Protocol research so the game can be sped up from day 1."""
    display_name = "Start With TT2 Protocol"


class RandomizeAgentSlots(DefaultOnToggle):
    """
    Every department can only hold a limited number of agents. Additional agent slots for each department are
    shuffled into the item pool (up to the vanilla maximum of 5).
    """
    display_name = "Randomize Agent Slots"


class StartingAgentSlots(Range):
    """How many agent slots every department starts with when agent slots are randomized."""
    display_name = "Starting Agent Slots"
    range_start = 1
    range_end = 4
    default = 2


class ExtractionOffers(Range):
    """
    How many abnormalities from your pool are offered on each extraction day.
    When more are available, the ones you received first are offered first.
    """
    display_name = "Extraction Offers"
    range_start = 1
    range_end = 3
    default = 3


class LogicDifficulty(Choice):
    """
    How much the logic expects you to have before later days are considered reachable.

    **easy**: Expects plenty of E.G.O, agent slots and bullet research before harder days.
    **normal**: Moderate expectations.
    **hard**: Only light expectations.
    **minimal**: Only the hard requirements (enough abnormalities, required Core Suppressions, mission conditions).
    """
    display_name = "Logic Difficulty"
    option_easy = 0
    option_normal = 1
    option_hard = 2
    option_minimal = 3
    default = 1


class LOBPointsPerItem(Range):
    """How many LOB points one "LOB Points" item gives."""
    display_name = "LOB Points Per Item"
    range_start = 1
    range_end = 20
    default = 4


class PEBoxesPerItem(Range):
    """How many PE boxes one "PE Box Shipment" item gives to a random contained abnormality."""
    display_name = "PE Boxes Per Item"
    range_start = 5
    range_end = 200
    default = 30


class TrapPercentage(Range):
    """Percentage of filler items that are replaced by traps."""
    display_name = "Trap Percentage"
    range_start = 0
    range_end = 100
    default = 20


class OrdealTrapWeight(Range):
    """
    Weight of Ordeal traps (Dawn, Noon, Dusk and Midnight). An Ordeal trap summons an Ordeal; tiers above what the
    current day would normally allow are downgraded.
    """
    display_name = "Ordeal Trap Weight"
    range_start = 0
    range_end = 10
    default = 5


class BreachTrapWeight(Range):
    """Weight of Abnormality Breach traps, which drop a random abnormality's Qliphoth counter to 0."""
    display_name = "Abnormality Breach Trap Weight"
    range_start = 0
    range_end = 10
    default = 5


class MeltdownTrapWeight(Range):
    """Weight of Qliphoth Meltdown traps, which push the Qliphoth meltdown gauge forward."""
    display_name = "Qliphoth Meltdown Trap Weight"
    range_start = 0
    range_end = 10
    default = 3


class EnergyDrainTrapWeight(Range):
    """Weight of Energy Drain traps, which remove 20% of the energy collected today."""
    display_name = "Energy Drain Trap Weight"
    range_start = 0
    range_end = 10
    default = 3


class PanicTrapWeight(Range):
    """Weight of Panic traps, which make the agents of one department panic."""
    display_name = "Panic Trap Weight"
    range_start = 0
    range_end = 10
    default = 2


class DeathLinkTrigger(Choice):
    """
    When a DeathLink is sent.

    **day_failure**: When you restart the day or return to a checkpoint after a failure.
    **department_wipe**: When every agent of a department is dead.
    """
    display_name = "DeathLink Trigger"
    option_day_failure = 0
    option_department_wipe = 1
    default = 0


class DeathLinkEffect(Choice):
    """
    What happens to you when another player dies.

    **agent**: A random agent dies.
    **breach**: A random abnormality breaches containment.
    **ordeal**: An Ordeal fitting the current day appears.
    """
    display_name = "DeathLink Effect"
    option_agent = 0
    option_breach = 1
    option_ordeal = 2
    default = 0


@dataclass
class LobotomyCorporationOptions(PerGameCommonOptions):
    goal_day: GoalDay
    required_core_suppressions: RequiredCoreSuppressions
    starting_abnormalities: StartingAbnormalities
    start_with_one_sin: StartWithOneSin
    tool_abnormalities: ToolAbnormalities
    excluded_abnormalities: ExcludedAbnormalities
    priority_abnormalities: PriorityAbnormalities
    extraction_offers: ExtractionOffers
    observation_checks: ObservationChecks
    ego_extraction_checks: EGOExtractionChecks
    ego_gift_checks: EGOGiftChecks
    extraction_after_checks: ExtractionAfterChecks
    randomize_research: RandomizeResearch
    start_with_tt2_protocol: StartWithTT2Protocol
    randomize_agent_slots: RandomizeAgentSlots
    starting_agent_slots: StartingAgentSlots
    logic_difficulty: LogicDifficulty
    lob_points_per_item: LOBPointsPerItem
    pe_boxes_per_item: PEBoxesPerItem
    trap_percentage: TrapPercentage
    ordeal_trap_weight: OrdealTrapWeight
    breach_trap_weight: BreachTrapWeight
    meltdown_trap_weight: MeltdownTrapWeight
    energy_drain_trap_weight: EnergyDrainTrapWeight
    panic_trap_weight: PanicTrapWeight
    death_link: DeathLink
    death_link_trigger: DeathLinkTrigger
    death_link_effect: DeathLinkEffect
    start_inventory_from_pool: StartInventoryPool


option_groups = [
    OptionGroup("Goal", [GoalDay, RequiredCoreSuppressions]),
    OptionGroup("Abnormalities", [
        StartingAbnormalities, StartWithOneSin, ToolAbnormalities, ExcludedAbnormalities, PriorityAbnormalities,
        ExtractionOffers,
    ]),
    OptionGroup("Locations", [ObservationChecks, EGOExtractionChecks, EGOGiftChecks, ExtractionAfterChecks]),
    OptionGroup("Items", [
        RandomizeResearch, StartWithTT2Protocol, RandomizeAgentSlots, StartingAgentSlots, LOBPointsPerItem,
        PEBoxesPerItem,
    ]),
    OptionGroup("Logic", [LogicDifficulty]),
    OptionGroup("Traps", [
        TrapPercentage, OrdealTrapWeight, BreachTrapWeight, MeltdownTrapWeight, EnergyDrainTrapWeight,
        PanicTrapWeight,
    ]),
    OptionGroup("DeathLink", [DeathLink, DeathLinkTrigger, DeathLinkEffect]),
]

option_presets = {
    "Short shift": {
        "goal_day": 15,
        "starting_abnormalities": 3,
        "observation_checks": 3,
        "logic_difficulty": "easy",
    },
    "Full facility": {
        "goal_day": 50,
        "required_core_suppressions": 9,
        "logic_difficulty": "normal",
        "ego_extraction_checks": 2,
    },
}
