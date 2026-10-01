from __future__ import annotations

from typing import NamedTuple

from BaseClasses import Location

from .data import (
    BASE_ID,
    CONTAINMENT_LOCATION_OFFSET,
    DAY_LOCATION_OFFSET,
    FINAL_DAY,
    GAME_NAME,
    MAX_CONTAINMENT,
    MISSION_LOCATION_OFFSET,
    MISSIONS,
    SEPHIRAH_BY_SEFIRA,
    SUPPRESSION_LOCATION_OFFSET,
    SUPPRESSIONS,
    day_of_containment,
    mission_location_name,
    suppression_location_name,
)


class LobotomyCorporationLocation(Location):
    game = GAME_NAME


class LocationData(NamedTuple):
    code: int
    kind: str        # observation, ego_weapon, ego_suit, ego_gift, day, mission, suppression
    min_day: int     # first day on which logic can expect the location
    index: int = 0   # containment index, day, mission number or sefira depending on kind
    level: int = 0   # observation level / extraction copy


MAX_EGO_COPIES = 3

# Sub ids inside a containment index block (index * 100 + sub id).
OBSERVATION_SUB_ID = 0     # + level (1..4)
EGO_WEAPON_SUB_ID = 10     # + copy - 1
EGO_SUIT_SUB_ID = 20       # + copy - 1
EGO_GIFT_SUB_ID = 30


def containment_prefix(index: int) -> str:
    return f"Abnormality #{index:02d}"


def observation_location_name(index: int, level: int) -> str:
    return f"{containment_prefix(index)} - Observation Level {level}"


def ego_location_name(index: int, kind: str, copy: int) -> str:
    label = "E.G.O Weapon Extraction" if kind == "ego_weapon" else "E.G.O Suit Extraction"
    suffix = "" if copy == 1 else f" {copy}"
    return f"{containment_prefix(index)} - {label}{suffix}"


def gift_location_name(index: int) -> str:
    return f"{containment_prefix(index)} - E.G.O Gift"


def day_location_name(day: int) -> str:
    return f"Day {day:02d} Cleared"


location_table: dict[str, LocationData] = {}

for _index in range(1, MAX_CONTAINMENT + 1):
    _day = day_of_containment(_index)
    _base = BASE_ID + CONTAINMENT_LOCATION_OFFSET + _index * 100
    for _level in range(1, 5):
        location_table[observation_location_name(_index, _level)] = LocationData(
            _base + OBSERVATION_SUB_ID + _level, "observation", _day, _index, _level)
    for _copy in range(1, MAX_EGO_COPIES + 1):
        location_table[ego_location_name(_index, "ego_weapon", _copy)] = LocationData(
            _base + EGO_WEAPON_SUB_ID + _copy - 1, "ego_weapon", _day, _index, _copy)
        location_table[ego_location_name(_index, "ego_suit", _copy)] = LocationData(
            _base + EGO_SUIT_SUB_ID + _copy - 1, "ego_suit", _day, _index, _copy)
    location_table[gift_location_name(_index)] = LocationData(
        _base + EGO_GIFT_SUB_ID, "ego_gift", _day, _index)

for _day in range(1, FINAL_DAY + 1):
    location_table[day_location_name(_day)] = LocationData(BASE_ID + DAY_LOCATION_OFFSET + _day, "day", _day, _day)

for _mission in MISSIONS:
    location_table[mission_location_name(_mission)] = LocationData(
        BASE_ID + MISSION_LOCATION_OFFSET + _mission.sefira * 10 + _mission.number, "mission", _mission.min_day,
        _mission.sefira, _mission.number)

for _suppression in SUPPRESSIONS:
    location_table[suppression_location_name(_suppression.sefira)] = LocationData(
        BASE_ID + SUPPRESSION_LOCATION_OFFSET + _suppression.sefira, "suppression", _suppression.min_day,
        _suppression.sefira)

location_name_to_id: dict[str, int] = {name: data.code for name, data in location_table.items()}
assert len(set(location_name_to_id.values())) == len(location_name_to_id), "duplicate location ids"


def _group(predicate) -> set[str]:
    return {name for name, data in location_table.items() if predicate(data)}


location_name_groups: dict[str, set[str]] = {
    "Observation": _group(lambda d: d.kind == "observation"),
    "E.G.O Extraction": _group(lambda d: d.kind in ("ego_weapon", "ego_suit")),
    "E.G.O Gifts": _group(lambda d: d.kind == "ego_gift"),
    "Days": _group(lambda d: d.kind == "day"),
    "Missions": _group(lambda d: d.kind == "mission"),
    "Core Suppressions": _group(lambda d: d.kind == "suppression"),
}
for _sefira, _sephirah in SEPHIRAH_BY_SEFIRA.items():
    location_name_groups[f"{_sephirah} Missions"] = _group(lambda d, s=_sefira: d.kind == "mission" and d.index == s)

location_descriptions: dict[str, str] = {
    "Abnormality #01 - Observation Level 1": (
        'Sent when the first abnormality you contained reaches observation level 1. Every "Abnormality #NN" '
        "location refers to the NN-th abnormality contained in the facility, whichever abnormality it is."
    ),
}
