from __future__ import annotations

from typing import NamedTuple

from BaseClasses import Item, ItemClassification

from .data import (
    ABNORMALITIES,
    ABNORMALITY_ITEM_OFFSET,
    AGENT_SLOT_ITEM_OFFSET,
    ALL_ABNORMALITIES,
    BASE_ID,
    DEPARTMENTS,
    EGO_SUIT_ITEM_OFFSET,
    EGO_WEAPON_ITEM_OFFSET,
    FILLER_ITEM_OFFSET,
    FILLER_ITEMS,
    GAME_NAME,
    RECOVERY_BULLETS,
    RESEARCH,
    RESEARCH_ITEM_OFFSET,
    SHIELD_BULLETS,
    TRAP_ITEM_OFFSET,
    TRAP_ITEMS,
    Abnormality,
    Risk,
    agent_slot_item_name,
    ego_suit_name,
    ego_weapon_name,
)


class LobotomyCorporationItem(Item):
    game = GAME_NAME


class ItemData(NamedTuple):
    code: int
    kind: str  # abnormality, tool, ego_weapon, ego_suit, research, agent_slot, filler, trap
    classification: ItemClassification
    abnormality: Abnormality | None = None


item_table: dict[str, ItemData] = {}

for _abno in ABNORMALITIES:
    item_table[_abno.name] = ItemData(BASE_ID + ABNORMALITY_ITEM_OFFSET + _abno.game_id, "abnormality",
                                      ItemClassification.progression, _abno)
for _abno in ALL_ABNORMALITIES:
    if _abno.tool:
        item_table[_abno.name] = ItemData(BASE_ID + ABNORMALITY_ITEM_OFFSET + _abno.game_id, "tool",
                                          ItemClassification.progression, _abno)
for _abno in ABNORMALITIES:
    if _abno.weapon:
        item_table[ego_weapon_name(_abno)] = ItemData(BASE_ID + EGO_WEAPON_ITEM_OFFSET + _abno.game_id,
                                                      "ego_weapon", ItemClassification.progression, _abno)
    if _abno.suit:
        item_table[ego_suit_name(_abno)] = ItemData(BASE_ID + EGO_SUIT_ITEM_OFFSET + _abno.game_id,
                                                    "ego_suit", ItemClassification.progression, _abno)
for _research in RESEARCH:
    item_table[_research.name] = ItemData(BASE_ID + RESEARCH_ITEM_OFFSET + _research.key, "research",
                                          ItemClassification.useful)
for _dept in DEPARTMENTS:
    item_table[agent_slot_item_name(_dept)] = ItemData(BASE_ID + AGENT_SLOT_ITEM_OFFSET + _dept.index, "agent_slot",
                                                       ItemClassification.progression)
for _name, _index in FILLER_ITEMS.items():
    item_table[_name] = ItemData(BASE_ID + FILLER_ITEM_OFFSET + _index, "filler", ItemClassification.filler)
for _name, _index in TRAP_ITEMS.items():
    item_table[_name] = ItemData(BASE_ID + TRAP_ITEM_OFFSET + _index, "trap", ItemClassification.trap)

item_name_to_id: dict[str, int] = {name: data.code for name, data in item_table.items()}
assert len(set(item_name_to_id.values())) == len(item_name_to_id), "duplicate item ids"

# Research referenced by logic. These become progression when the logic difficulty uses them.
LOGIC_RESEARCH: set[str] = {"Joint Command", *SHIELD_BULLETS, *RECOVERY_BULLETS}

# Names of the virtual counters stored in CollectionState.prog_items by LobotomyCorporationWorld.collect().
ABNORMALITY_COUNT = "__lobcorp_abnormalities"
EGO_POWER_COUNT = "__lobcorp_ego_power"
HE_EGO_COUNT = "__lobcorp_he_plus_ego"
ALEPH_EGO_COUNT = "__lobcorp_aleph_ego"
WAW_ABNORMALITY_COUNT = "__lobcorp_waw_abnormalities"
ALEPH_ABNORMALITY_COUNT = "__lobcorp_aleph_abnormalities"
BREACHING_ABNORMALITY_COUNT = "__lobcorp_breaching_abnormalities"


def _group(predicate) -> set[str]:
    return {name for name, data in item_table.items() if predicate(data)}


item_name_groups: dict[str, set[str]] = {
    "Abnormalities": _group(lambda d: d.kind in ("abnormality", "tool")),
    "Tool Abnormalities": _group(lambda d: d.kind == "tool"),
    "ZAYIN Abnormalities": _group(lambda d: d.kind in ("abnormality", "tool") and d.abnormality.risk == Risk.ZAYIN),
    "TETH Abnormalities": _group(lambda d: d.kind in ("abnormality", "tool") and d.abnormality.risk == Risk.TETH),
    "HE Abnormalities": _group(lambda d: d.kind in ("abnormality", "tool") and d.abnormality.risk == Risk.HE),
    "WAW Abnormalities": _group(lambda d: d.kind in ("abnormality", "tool") and d.abnormality.risk == Risk.WAW),
    "ALEPH Abnormalities": _group(lambda d: d.kind in ("abnormality", "tool") and d.abnormality.risk == Risk.ALEPH),
    "E.G.O": _group(lambda d: d.kind in ("ego_weapon", "ego_suit")),
    "E.G.O Weapons": _group(lambda d: d.kind == "ego_weapon"),
    "E.G.O Suits": _group(lambda d: d.kind == "ego_suit"),
    "Research": _group(lambda d: d.kind == "research"),
    "Bullets": {r.name for r in RESEARCH if r.category == "bullet"},
    "Shield Bullets": set(SHIELD_BULLETS),
    "Agent Slots": _group(lambda d: d.kind == "agent_slot"),
    "Traps": _group(lambda d: d.kind == "trap"),
    "Ordeal Traps": {"Dawn Ordeal Trap", "Noon Ordeal Trap", "Dusk Ordeal Trap", "Midnight Ordeal Trap"},
    "Filler": _group(lambda d: d.kind == "filler"),
}

item_descriptions: dict[str, str] = {
    "LOB Points": "Adds LOB points to the facility budget.",
    "PE Box Shipment": "Adds PE boxes to a random contained abnormality, usable for observation and E.G.O.",
    "Enkephalin Canister": "Adds 10% of today's energy quota when received during a work day.",
    "Training Manual": "Gives every agent experience in all four stats.",
    "Emergency Medical Supplies": "Fully restores the HP and SP of every employee.",
    "Dawn Ordeal Trap": "Summons a Dawn Ordeal.",
    "Noon Ordeal Trap": "Summons a Noon Ordeal (downgraded before day 11).",
    "Dusk Ordeal Trap": "Summons a Dusk Ordeal (downgraded before day 21).",
    "Midnight Ordeal Trap": "Summons a Midnight Ordeal (downgraded before day 26).",
    "Abnormality Breach Trap": "A random abnormality's Qliphoth counter drops to 0.",
    "Qliphoth Meltdown Trap": "Fills the Qliphoth meltdown gauge.",
    "Energy Drain Trap": "Removes 20% of the energy collected today.",
    "Panic Trap": "The agents of a random department panic.",
}
for _abno in ALL_ABNORMALITIES:
    item_descriptions[_abno.name] = (
        f"Adds the {_abno.risk.name} {'tool ' if _abno.tool else ''}abnormality {_abno.name} to the pool of "
        f"abnormalities offered on extraction days."
    )
for _dept in DEPARTMENTS:
    item_descriptions[agent_slot_item_name(_dept)] = f"The {_dept.name} can hold one more agent."
