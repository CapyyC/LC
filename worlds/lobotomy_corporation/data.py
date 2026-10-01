"""
Static game data for Lobotomy Corporation.

Everything the generator needs to know about the game lives here: abnormalities (with their internal game IDs),
departments, Sephirah missions, research and the day/extraction schedule. The game mod resolves most of this at
runtime from the game's own data, so the IDs here are mainly used to build stable Archipelago IDs.
"""
from __future__ import annotations

from dataclasses import dataclass
from enum import IntEnum

GAME_NAME = "Lobotomy Corporation"

# Steam app id 568220, scaled up. Every Archipelago item/location id of this world is BASE_ID + offset.
BASE_ID = 568_220_000

# Item id offsets
ABNORMALITY_ITEM_OFFSET = 0            # + internal creature id (100001..300110)
EGO_WEAPON_ITEM_OFFSET = 1_000_000     # + internal creature id
EGO_SUIT_ITEM_OFFSET = 2_000_000       # + internal creature id
RESEARCH_ITEM_OFFSET = 3_000_000       # + research key (sefira * 10 + slot)
AGENT_SLOT_ITEM_OFFSET = 4_000_000     # + department index
FILLER_ITEM_OFFSET = 5_000_000         # + filler index
TRAP_ITEM_OFFSET = 6_000_000           # + trap index

# Location id offsets
CONTAINMENT_LOCATION_OFFSET = 10_000_000  # + containment index * 100 + sub id (see locations.py)
DAY_LOCATION_OFFSET = 11_000_000          # + day
MISSION_LOCATION_OFFSET = 12_000_000      # + sefira * 10 + mission number
SUPPRESSION_LOCATION_OFFSET = 13_000_000  # + sefira


class Risk(IntEnum):
    ZAYIN = 0
    TETH = 1
    HE = 2
    WAW = 3
    ALEPH = 4


@dataclass(frozen=True)
class Abnormality:
    game_id: int
    name: str
    risk: Risk
    ego: str = ""
    weapon: bool = True
    suit: bool = True
    gift: bool = True
    breaches: bool = False
    tool: bool = False


Z, T, H, W, A = Risk.ZAYIN, Risk.TETH, Risk.HE, Risk.WAW, Risk.ALEPH

# Selectable (non-tool) abnormalities. Apocalypse Bird (100038), WhiteNight (100015) and the tutorial
# Standard Training-Dummy Rabbit (100000) cannot be extracted normally and are not part of the pool.
ABNORMALITIES: list[Abnormality] = [
    Abnormality(100001, "Scorched Girl", T, "Fourth Match Flame", breaches=True),
    Abnormality(100002, "Happy Teddy Bear", H, "Bear Paws"),
    Abnormality(100003, "The Red Shoes", H, "Sanguine Desire", breaches=True),
    Abnormality(100004, "The Queen of Hatred", W, "In the Name of Love and Hate", breaches=True),
    Abnormality(100005, "Nothing There", A, "Mimicry", breaches=True),
    Abnormality(100006, "Singing Machine", H, "Harmony", suit=False),
    Abnormality(100007, "1.76 MHz", T, "Noise", weapon=False),
    Abnormality(100008, "Big Bird", W, "Lamp", breaches=True),
    Abnormality(100009, "One Sin and Hundreds of Good Deeds", Z, "Penitence"),
    Abnormality(100011, "All-Around Helper", H, "Grinder Mk4", breaches=True),
    Abnormality(100012, "Spider Bud", T, "Red Eyes"),
    Abnormality(100013, "Beauty and the Beast", T, "Horn"),
    Abnormality(100014, "Plague Doctor", Z, "Bless", weapon=False, suit=False),
    Abnormality(100016, "Rudolta of the Sleigh", H, "Christmas", breaches=True),
    Abnormality(100017, "Nameless Fetus", H, "Syrinx"),
    Abnormality(100018, "Forsaken Murderer", T, "Regret", breaches=True),
    Abnormality(100019, "The Silent Orchestra", A, "Da Capo", breaches=True),
    Abnormality(100020, "Punishing Bird", T, "Beak", breaches=True),
    Abnormality(100021, "Old Lady", T, "Solitude"),
    Abnormality(100022, "The Lady Facing the Wall", T, "Screaming Wedge", gift=False),
    Abnormality(100023, "Snow White's Apple", W, "Green Stem", breaches=True),
    Abnormality(100024, "Don't Touch Me", Z, "", weapon=False, suit=False, gift=False),
    Abnormality(100026, "Queen Bee", W, "Hornet", breaches=True),
    Abnormality(100027, "Bloodbath", T, "Wrist Cutter"),
    Abnormality(100028, "Opened Can of WellCheers", Z, "Soda"),
    Abnormality(100029, "Alriune", W, "Faint Aroma", breaches=True),
    Abnormality(100031, "Child of the Galaxy", H, "Our Galaxy"),
    Abnormality(100032, "Little Red Riding Hooded Mercenary", W, "Crimson Scar", breaches=True),
    Abnormality(100033, "Big and Will be Bad Wolf", W, "Cobalt Scar", breaches=True),
    Abnormality(100034, "You're Bald...", Z, "Tough"),
    Abnormality(100035, "Judgement Bird", W, "Justitia", breaches=True),
    Abnormality(100036, "Fragment of the Universe", T, "Fragments from Somewhere", breaches=True),
    Abnormality(100037, "Crumbling Armor", T, "Life for a Daredevil"),
    Abnormality(100039, "The King of Greed", W, "Gold Rush", breaches=True),
    Abnormality(100040, "The Little Prince", W, "Spore"),
    Abnormality(100041, "Laetitia", H, "Laetitia", breaches=True),
    Abnormality(100042, "Mountain of Smiling Bodies", A, "Smile", breaches=True),
    Abnormality(100043, "The Funeral of the Dead Butterflies", H, "Solemn Lament", breaches=True),
    Abnormality(100044, "The Burrowing Heaven", W, "Heaven"),
    Abnormality(100045, "Dream of a Black Swan", W, "Black Swan", breaches=True),
    Abnormality(100046, "The Naked Nest", W, "Exuviae", breaches=True),
    Abnormality(100047, "The Dreaming Current", W, "Ecstasy", breaches=True),
    Abnormality(100048, "The Knight of Despair", W, "The Sword Sharpened with Tears", breaches=True),
    Abnormality(100049, "Schadenfreude", H, "Gaze"),
    Abnormality(100050, "Scarecrow Searching for Wisdom", H, "Harvest", breaches=True),
    Abnormality(100051, "Warm-hearted Woodsman", H, "Logging", breaches=True),
    Abnormality(100052, "Today's Shy Look", T, "Today's Expression"),
    Abnormality(100053, "Fairy Festival", Z, "Wingbeat"),
    Abnormality(100054, "Meat Lantern", T, "Lantern", breaches=True),
    Abnormality(100055, "Dimensional Refraction Variant", W, "Diffraction", suit=False, breaches=True),
    Abnormality(100056, "CENSORED", A, "CENSORED", suit=False, breaches=True),
    Abnormality(100057, "Porccubus", H, "Pleasure"),
    Abnormality(100058, "Blue Star", A, "Sound of a Star", breaches=True),
    Abnormality(100059, "Grave of Cherry Blossoms", T, "Cherry Blossoms"),
    Abnormality(100060, "Void Dream", T, "Engulfing Dream", breaches=True),
    Abnormality(100061, "The Firebird", W, "Feather of Honor", breaches=True),
    Abnormality(100062, "Parasite Tree", W, "Hypocrisy"),
    Abnormality(100063, "Melting Love", A, "Adoration", breaches=True),
    Abnormality(100064, "Army in Black", Z, "Pink", breaches=True),
    Abnormality(100065, "Il Pianto della Luna", W, "Moonlight"),
    Abnormality(100102, "The Snow Queen", H, "Frost Splinter", breaches=True),
    Abnormality(100103, "Der Freischütz", H, "Magic Bullet"),
    Abnormality(100104, "Yin", W, "Discord", breaches=True),
    Abnormality(100105, "Clouded Monk", W, "Amita", breaches=True),
    Abnormality(100106, "Ppodae", T, "SO CUTE!!!", breaches=True),
]

# Tool abnormalities: they occupy a containment unit like any other abnormality, but have no E.G.O equipment.
TOOLS: list[Abnormality] = [
    Abnormality(game_id, name, risk, weapon=False, suit=False, gift=False, tool=True)
    for game_id, name, risk in (
        (300001, "The Heart of Aspiration", T),
        (300002, "Notes from a Crazed Researcher", H),
        (300003, "Theresia", T),
        (300004, "Flesh Idol", W),
        (300005, "Giant Tree Sap", H),
        (300006, "Shelter from the 27th of March", H),
        (300007, "Mirror of Adjustment", Z),
        (300101, "We Can Change Anything", Z),
        (300102, "Express Train to Hell", W),
        (300103, "Skin Prophecy", T),
        (300104, "Portrait of Another World", H),
        (300105, "You Must Be Happy", Z),
        (300106, "Luminous Bracelet", T),
        (300107, "Behavior Adjustment", T),
        (300108, "Old Faith and Promise", Z),
        (300109, "Yang", W),
        (300110, "Backward Clock", W),
    )
]

ALL_ABNORMALITIES: list[Abnormality] = ABNORMALITIES + TOOLS
ABNORMALITY_BY_NAME: dict[str, Abnormality] = {abno.name: abno for abno in ALL_ABNORMALITIES}
ABNORMALITY_BY_ID: dict[int, Abnormality] = {abno.game_id: abno for abno in ALL_ABNORMALITIES}

# Starting-abnormality candidates: low risk and harmless enough to run day 1 with.
SAFE_STARTERS = [
    "One Sin and Hundreds of Good Deeds", "Opened Can of WellCheers", "You're Bald...", "Fairy Festival",
    "Spider Bud", "Old Lady", "Today's Shy Look", "Bloodbath", "Beauty and the Beast",
    "Grave of Cherry Blossoms", "Fragment of the Universe", "Punishing Bird",
]


def ego_weapon_name(abno: Abnormality) -> str:
    return f"{abno.ego} (E.G.O Weapon)"


def ego_suit_name(abno: Abnormality) -> str:
    return f"{abno.ego} (E.G.O Suit)"


# --------------------------------------------------------------------------------------------------------------------
# Departments
# --------------------------------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Department:
    index: int          # index used for agent slot item ids
    name: str           # "Control Team"
    sephirah: str       # "Malkuth"
    sefira: int         # value of the game's SefiraEnum
    open_day: int       # day on which the department opens in the vanilla game
    logic_day: int      # conservative opening day used by logic (some unlock orders are ambiguous)
    has_missions: bool = True


# SefiraEnum: MALKUT=0, YESOD=1, HOD=2, NETZACH=3, TIPERERTH1=4, TIPERERTH2=5, GEBURAH=6, CHESED=7, BINAH=8,
# CHOKHMAH=9, KETHER=10.
DEPARTMENTS: list[Department] = [
    Department(0, "Control Team", "Malkuth", 0, 1, 1),
    Department(1, "Information Team", "Yesod", 1, 6, 6),
    Department(2, "Safety Team", "Netzach", 3, 11, 16),
    Department(3, "Training Team", "Hod", 2, 16, 16),
    Department(4, "Central Command Team (Upper)", "Tiphereth", 4, 21, 21),
    Department(5, "Central Command Team (Lower)", "Tiphereth", 5, 21, 21, has_missions=False),
    Department(6, "Disciplinary Team", "Gebura", 6, 26, 31),
    Department(7, "Welfare Team", "Chesed", 7, 31, 31),
    Department(8, "Extraction Team", "Binah", 8, 36, 41),
    Department(9, "Record Team", "Hokma", 9, 41, 41),
    Department(10, "Architecture Team", "Kether", 10, 46, 46, has_missions=False),
]
DEPARTMENT_BY_NAME: dict[str, Department] = {dept.name: dept for dept in DEPARTMENTS}


def agent_slot_item_name(dept: Department) -> str:
    return f"{dept.name} Agent Slot"


# Vanilla departments hold at most 5 agents.
MAX_AGENTS_PER_DEPARTMENT = 5


# --------------------------------------------------------------------------------------------------------------------
# Research
# --------------------------------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Research:
    key: int            # sefira * 10 + slot
    name: str
    sefira: int
    slot: int           # 0..2, order inside the department's research list
    category: str       # "qol", "bullet", "facility"


RESEARCH: list[Research] = [
    Research(0, "TT2 Protocol", 0, 0, "qol"),
    Research(1, "Joint Command", 0, 1, "qol"),
    Research(2, "Assembly Summons", 0, 2, "qol"),
    Research(10, "G.O Visualization", 1, 0, "qol"),
    Research(11, "Damage Normalization", 1, 1, "qol"),
    Research(12, "Abnormality Countermeasures Manual", 1, 2, "facility"),
    Research(30, "Regenerator Mk2", 3, 0, "facility"),
    Research(31, "Mental Corruption Neutralizing Gas", 3, 1, "facility"),
    Research(32, "Regenerator Distinguisher Modification", 3, 2, "facility"),
    Research(20, "Supply Educational Manuals", 2, 0, "facility"),
    Research(21, "Improve Work Education", 2, 1, "facility"),
    Research(22, "Improve Hiring Procedures", 2, 2, "facility"),
    Research(40, "Physical Intervention Shield Bullet", 4, 0, "bullet"),
    Research(41, "Trauma Shield Bullet", 4, 1, "bullet"),
    Research(42, "Erosion Shield Bullet", 4, 2, "bullet"),
    Research(60, "Execution Bullet", 6, 0, "bullet"),
    Research(61, "Qliphoth Intervention Field Bullet", 6, 1, "bullet"),
    Research(62, "Rabbit Team Protocol", 6, 2, "facility"),
    Research(70, "HP-N Bullet", 7, 0, "bullet"),
    Research(71, "SP-E Bullet", 7, 1, "bullet"),
    Research(72, "HP & SP Bullet Refining", 7, 2, "bullet"),
    Research(80, "E.G.O Restoration", 8, 0, "facility"),
    Research(81, "Re-extraction", 8, 1, "facility"),
    Research(82, "Gift Division", 8, 2, "facility"),
    Research(90, "Limit Break: The Virtue of Protection", 9, 0, "facility"),
    Research(91, "Limit Break: The Virtue of Governance", 9, 1, "facility"),
    Research(92, "Limit Break: The Virtue of Creation", 9, 2, "facility"),
]
RESEARCH_BY_NAME: dict[str, Research] = {r.name: r for r in RESEARCH}

# Names the game itself uses for these researches (used by the mod to match research entries by name).
RESEARCH_GAME_NAMES: dict[str, str] = {
    "Physical Intervention Shield Bullet": "Physical Intervention Shield",
    "Trauma Shield Bullet": "Trauma Shield",
    "Erosion Shield Bullet": "Erosion Shield",
    "Execution Bullet": "Execution Bullets",
    "Qliphoth Intervention Field Bullet": "Qliphoth Intervention Field",
    "Rabbit Team Protocol": "Rabbit Team",
    "HP-N Bullet": "HP-N Bullets",
    "SP-E Bullet": "SP-E Bullets",
    "E.G.O Restoration": "E.G.O. Restoration",
}

SHIELD_BULLETS = ["Physical Intervention Shield Bullet", "Trauma Shield Bullet", "Erosion Shield Bullet"]
RECOVERY_BULLETS = ["HP-N Bullet", "SP-E Bullet"]


# --------------------------------------------------------------------------------------------------------------------
# Missions and Core Suppressions
# --------------------------------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Mission:
    sefira: int
    number: int         # 1..4
    game_id: int        # the game's mission id
    title: str
    min_day: int        # first day logic expects the mission to be completable
    requirement: str = ""  # extra logic requirement key, see rules.py


MISSIONS: list[Mission] = [
    Mission(0, 1, 1, "You Can Do This!", 1),
    Mission(0, 2, 2, "Semper Fi", 2),
    Mission(0, 3, 103, "Controlling Meltdowns", 3),
    Mission(0, 4, 104, "When Dusk Falls", 21, "dusk"),
    Mission(1, 1, 3, "Yesod the Viper", 7),
    Mission(1, 2, 4, "The Paths to Take", 8),
    Mission(1, 3, 5, "Why Won't It Rot?", 9),
    Mission(1, 4, 404, "At the Cusp of Perfection", 10),
    Mission(3, 1, 6, "Lethargic Room", 17),
    Mission(3, 2, 7, "I'd Rather Get a Beer Vending Machine", 18),
    Mission(3, 3, 203, "An Impossible Event", 19),
    Mission(3, 4, 204, "Total Efficiency", 20),
    Mission(2, 1, 8, "I Want To Help", 17),
    Mission(2, 2, 9, "Unwanted Kindness", 18),
    Mission(2, 3, 10, "I Just Want To Be A Good Person", 19, "breachers_4"),
    Mission(2, 4, 304, "Practical Training", 30, "level5_agents"),
    Mission(4, 1, 11, "Proving Your Competence 1", 22),
    Mission(4, 2, 12, "Proving Your Competence 2", 23),
    Mission(4, 3, 13, "Proving Your Competence 3", 24),
    Mission(4, 4, 504, "Proving Your Competence 4", 25),
    Mission(6, 1, 14, "The Purpose of Pain 1", 32),
    Mission(6, 2, 15, "The Purpose of Pain 2", 33, "waw_3"),
    Mission(6, 3, 703, "Proving our Strength", 34, "aleph_3"),
    Mission(6, 4, 704, "Our Work", 35, "midnight"),
    Mission(7, 1, 16, "For the Aftermath 1", 32),
    Mission(7, 2, 17, "For the Aftermath 2", 33),
    Mission(7, 3, 18, "For the Aftermath 3", 34),
    Mission(7, 4, 804, "For the Aftermath 4", 35),
    Mission(8, 1, 901, "Collecting Extracted Specimens", 42, "he_ego_5"),
    Mission(8, 2, 902, "The Ego's Accessories", 43),
    Mission(8, 3, 903, "The Ego's Shells", 44, "aleph_ego_8"),
    Mission(8, 4, 904, "Abnormality Dissolution", 45),
    Mission(9, 1, 1001, "Virtue of Protection", 42, "agents_3"),
    Mission(9, 2, 1002, "The Virtue of Governance", 43, "agents_5"),
    Mission(9, 3, 1003, "The Virtue of Creation", 44, "agents_8"),
    Mission(9, 4, 1004, "The Virtue of the Soul", 45, "agents_10"),
]

SEPHIRAH_BY_SEFIRA: dict[int, str] = {
    0: "Malkuth", 1: "Yesod", 2: "Hod", 3: "Netzach", 4: "Tiphereth", 6: "Gebura", 7: "Chesed", 8: "Binah",
    9: "Hokma",
}


def mission_location_name(mission: Mission) -> str:
    return f"{SEPHIRAH_BY_SEFIRA[mission.sefira]} Mission {mission.number} - {mission.title}"


@dataclass(frozen=True)
class Suppression:
    sefira: int
    layer: str                       # "upper", "middle", "lower"
    min_day: int                     # first day logic expects the suppression to be possible
    prerequisites: tuple[str, ...]   # other mission / suppression location names that must be possible first


def suppression_location_name(sefira: int) -> str:
    return f"{SEPHIRAH_BY_SEFIRA[sefira]} Core Suppression"


_MISSION_NAMES = {(m.sefira, m.number): mission_location_name(m) for m in MISSIONS}
# Middle layer suppressions need every upper layer one first, lower layer ones every middle layer one.
_UPPER = tuple(suppression_location_name(sefira) for sefira in (0, 1, 2, 3))
_MIDDLE = tuple(suppression_location_name(sefira) for sefira in (4, 6, 7))

# Core Suppressions take a whole day each and are locked on Memory Imprint days (days ending in 1 or 6) and from day
# 46 onwards, so the minimum days below leave room to do every suppression of a layer one after another.
SUPPRESSIONS: list[Suppression] = [
    Suppression(0, "upper", 29, (_MISSION_NAMES[0, 4], _MISSION_NAMES[4, 3])),
    Suppression(1, "upper", 29, (_MISSION_NAMES[1, 4], _MISSION_NAMES[0, 4], _MISSION_NAMES[4, 3])),
    Suppression(2, "upper", 29, (_MISSION_NAMES[2, 4], _MISSION_NAMES[0, 4], _MISSION_NAMES[4, 3])),
    Suppression(3, "upper", 30, (_MISSION_NAMES[3, 4], "Hod Core Suppression")),
    Suppression(4, "middle", 40, (_MISSION_NAMES[4, 4], *_UPPER)),
    Suppression(6, "middle", 40, (_MISSION_NAMES[6, 4], _MISSION_NAMES[7, 4], *_UPPER)),
    Suppression(7, "middle", 40, (_MISSION_NAMES[7, 4], _MISSION_NAMES[6, 4], *_UPPER)),
    Suppression(8, "lower", 44, (_MISSION_NAMES[8, 4], *_MIDDLE)),
    Suppression(9, "lower", 44, (_MISSION_NAMES[9, 4], *_MIDDLE)),
]
SUPPRESSION_LAYERS: dict[str, list[int]] = {
    "upper": [0, 1, 2, 3],
    "middle": [4, 6, 7],
    "lower": [8, 9],
}


# --------------------------------------------------------------------------------------------------------------------
# Day schedule
# --------------------------------------------------------------------------------------------------------------------

FINAL_DAY = 50


def extractions_on_day(day: int) -> int:
    """How many abnormalities are extracted (contained) at the start of the given 1-based day."""
    if day < 1 or day > 49 or day % 5 == 0:
        return 0
    if 21 <= day <= 24 or 46 <= day <= 49:  # Central Command and Architecture Team get two units per expansion
        return 2
    return 1


# Cumulative number of contained abnormalities required to play each day, index = day.
CONTAINED_BY_DAY: list[int] = [0] * (FINAL_DAY + 1)
for _day in range(1, FINAL_DAY + 1):
    CONTAINED_BY_DAY[_day] = CONTAINED_BY_DAY[_day - 1] + extractions_on_day(_day)

MAX_CONTAINMENT = CONTAINED_BY_DAY[FINAL_DAY]  # 48


def day_of_containment(index: int) -> int:
    """First day on which the index-th (1-based) contained abnormality is in the facility."""
    for day in range(1, FINAL_DAY + 1):
        if CONTAINED_BY_DAY[day] >= index:
            return day
    raise ValueError(f"containment index {index} is out of range")


def departments_open_by(day: int, conservative: bool = True) -> list[Department]:
    return [dept for dept in DEPARTMENTS if (dept.logic_day if conservative else dept.open_day) <= day]


# --------------------------------------------------------------------------------------------------------------------
# Filler and traps
# --------------------------------------------------------------------------------------------------------------------

FILLER_ITEMS: dict[str, int] = {
    "LOB Points": 1,
    "PE Box Shipment": 2,
    "Enkephalin Canister": 3,
    "Training Manual": 4,
    "Emergency Medical Supplies": 5,
}

TRAP_ITEMS: dict[str, int] = {
    "Dawn Ordeal Trap": 1,
    "Noon Ordeal Trap": 2,
    "Dusk Ordeal Trap": 3,
    "Midnight Ordeal Trap": 4,
    "Abnormality Breach Trap": 5,
    "Qliphoth Meltdown Trap": 6,
    "Energy Drain Trap": 7,
    "Panic Trap": 8,
}

# Weight of an E.G.O item in the "combat power" estimate used by logic.
EGO_POWER: dict[Risk, int] = {Risk.ZAYIN: 1, Risk.TETH: 1, Risk.HE: 2, Risk.WAW: 3, Risk.ALEPH: 4}


def find_abnormality(name: str) -> Abnormality | None:
    return ABNORMALITY_BY_NAME.get(name)
