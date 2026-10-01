"""
Logic for Lobotomy Corporation.

The game is a sequence of days. Before a day can be played the mod requires enough abnormalities in the
abnormality pool to fill that day's new containment units, so the number of received abnormality items is the one hard
requirement for progressing. On top of that, depending on the logic difficulty, later days expect a certain amount of
E.G.O equipment ("combat power"), extra agent slots and bullet research.

Every requirement is monotonic in the day number, so "can play day N" implies "can play every earlier day".
"""
from __future__ import annotations

import math
from collections.abc import Callable
from typing import TYPE_CHECKING

from BaseClasses import CollectionState

from .data import (
    CONTAINED_BY_DAY,
    DEPARTMENTS,
    FINAL_DAY,
    MAX_AGENTS_PER_DEPARTMENT,
    MISSIONS,
    RECOVERY_BULLETS,
    SHIELD_BULLETS,
    SUPPRESSION_LAYERS,
    SUPPRESSIONS,
    Mission,
    agent_slot_item_name,
    mission_location_name,
    suppression_location_name,
)
from .items import (
    ABNORMALITY_COUNT,
    ALEPH_ABNORMALITY_COUNT,
    ALEPH_EGO_COUNT,
    BREACHING_ABNORMALITY_COUNT,
    EGO_POWER_COUNT,
    HE_EGO_COUNT,
    WAW_ABNORMALITY_COUNT,
)

if TYPE_CHECKING:
    from . import LobotomyCorporationWorld

Rule = Callable[[CollectionState], bool]

# (first day, E.G.O power expected from that day on) at "normal" difficulty
POWER_TABLE = [(11, 3), (16, 6), (21, 12), (26, 20), (31, 28), (36, 36), (41, 44), (46, 56)]
# (first day, extra agent slots expected from that day on) at "normal" difficulty
SLOT_TABLE = [(6, 1), (11, 2), (16, 4), (21, 6), (26, 8), (31, 10), (36, 12), (41, 14), (46, 16)]
DIFFICULTY_FACTOR = {0: 1.5, 1: 1.0, 2: 0.5, 3: 0.0}

# (first day, number of research items from the list expected), per difficulty
RESEARCH_TABLE: dict[int, dict[str, list[tuple]]] = {
    0: {"Joint Command": [(11, 1)], "shield": [(21, 1), (26, 2), (36, 3)], "recovery": [(31, 1), (41, 2)]},
    1: {"Joint Command": [(16, 1)], "shield": [(26, 1), (36, 2), (46, 3)], "recovery": [(41, 1)]},
    2: {"Joint Command": [], "shield": [(36, 1), (46, 2)], "recovery": []},
    3: {"Joint Command": [], "shield": [], "recovery": []},
}


def _table_value(table, day: int, factor: float) -> int:
    value = 0
    for first_day, amount in table:
        if day >= first_day:
            value = amount
    return math.ceil(value * factor)


class LobotomyCorporationLogic:
    def __init__(self, world: LobotomyCorporationWorld) -> None:
        self.world = world
        self.player = world.player
        options = world.options
        difficulty = options.logic_difficulty.value
        factor = DIFFICULTY_FACTOR[difficulty]

        # E.G.O power expectations, capped so that they are always satisfiable by the generated item pool.
        if world.ego_item_count > 0:
            cap = int(world.total_ego_power * 0.6)
            self.power_by_day = [min(cap, _table_value(POWER_TABLE, day, factor)) for day in range(FINAL_DAY + 1)]
        else:
            self.power_by_day = [0] * (FINAL_DAY + 1)

        # Agent slot expectations, only counting departments that are open (per logic) on that day.
        self.slot_items_by_dept = {dept.index: agent_slot_item_name(dept) for dept in DEPARTMENTS}
        self.starting_slots = options.starting_agent_slots.value
        self.slot_depts_by_day: list[list[str]] = []
        self.slots_by_day: list[int] = []
        for day in range(FINAL_DAY + 1):
            open_depts = [dept for dept in DEPARTMENTS
                          if dept.logic_day <= day and dept.index in world.agent_slot_departments]
            self.slot_depts_by_day.append([self.slot_items_by_dept[dept.index] for dept in open_depts])
            available = sum(world.agent_slot_counts.get(dept.index, 0) for dept in open_depts)
            expected = _table_value(SLOT_TABLE, day, factor) if world.agent_slots_randomized else 0
            self.slots_by_day.append(min(expected, int(available * 0.8)))

        # Research expectations
        research_table = RESEARCH_TABLE[difficulty] if world.research_randomized else RESEARCH_TABLE[3]
        self.joint_command_by_day = [_table_value(research_table["Joint Command"], day, 1.0)
                                     for day in range(FINAL_DAY + 1)]
        self.shields_by_day = [_table_value(research_table["shield"], day, 1.0) for day in range(FINAL_DAY + 1)]
        self.recovery_by_day = [_table_value(research_table["recovery"], day, 1.0) for day in range(FINAL_DAY + 1)]

        self.mission_rules: dict[str, Rule] = {}
        self.suppression_rules: dict[int, Rule] = {}

    # ----------------------------------------------------------------------------------------------------------------
    # Research items that the logic actually references (they need to be progression items)
    # ----------------------------------------------------------------------------------------------------------------
    def logic_research(self, last_day: int) -> list[str]:
        result = []
        if max(self.joint_command_by_day[:last_day + 1]) > 0:
            result.append("Joint Command")
        if max(self.shields_by_day[:last_day + 1]) > 0:
            result += SHIELD_BULLETS
        if max(self.recovery_by_day[:last_day + 1]) > 0:
            result += RECOVERY_BULLETS
        return result

    def uses_power(self, last_day: int) -> bool:
        return max(self.power_by_day[:last_day + 1]) > 0

    def uses_slots(self, last_day: int) -> bool:
        return max(self.slots_by_day[:last_day + 1]) > 0

    # ----------------------------------------------------------------------------------------------------------------
    # Core rules
    # ----------------------------------------------------------------------------------------------------------------
    def extra_slots(self, state: CollectionState, slot_items: list[str]) -> int:
        limit = MAX_AGENTS_PER_DEPARTMENT - self.starting_slots
        prog_items = state.prog_items[self.player]
        return sum(min(limit, prog_items[item]) for item in slot_items)

    def agent_capacity(self, state: CollectionState, day: int) -> int:
        """Total number of agents the facility can hold on the given day (per logic)."""
        open_depts = [dept for dept in DEPARTMENTS if dept.logic_day <= day]
        if not self.world.agent_slots_randomized:
            return len(open_depts) * MAX_AGENTS_PER_DEPARTMENT
        prog_items = state.prog_items[self.player]
        limit = MAX_AGENTS_PER_DEPARTMENT - self.starting_slots
        return sum(self.starting_slots + min(limit, prog_items[self.slot_items_by_dept[dept.index]])
                   for dept in open_depts)

    def can_play(self, state: CollectionState, day: int) -> bool:
        player = self.player
        if state.count(ABNORMALITY_COUNT, player) < CONTAINED_BY_DAY[day]:
            return False
        if state.count(EGO_POWER_COUNT, player) < self.power_by_day[day]:
            return False
        if self.slots_by_day[day] and self.extra_slots(state, self.slot_depts_by_day[day]) < self.slots_by_day[day]:
            return False
        if self.joint_command_by_day[day] and not state.has("Joint Command", player):
            return False
        if self.shields_by_day[day] and not state.has_from_list(SHIELD_BULLETS, player, self.shields_by_day[day]):
            return False
        if self.recovery_by_day[day] and not state.has_from_list(RECOVERY_BULLETS, player,
                                                                 self.recovery_by_day[day]):
            return False
        # The game itself refuses to continue the Kether days without the Core Suppressions of each layer.
        if day >= 47 and not self.layer_suppressed(state, "upper"):
            return False
        if day >= 48 and not self.layer_suppressed(state, "middle"):
            return False
        if day >= 49 and not self.layer_suppressed(state, "lower"):
            return False
        return True

    def layer_suppressed(self, state: CollectionState, layer: str) -> bool:
        return all(self.suppression_rules[sefira](state) for sefira in SUPPRESSION_LAYERS[layer])

    def requirement(self, key: str, mission: Mission) -> Rule | None:
        player = self.player
        if not key:
            return None
        if key in ("dusk", "midnight", "level5_agents"):
            return None  # covered by the mission's minimum day
        if key == "breachers_4":
            return lambda state: state.count(BREACHING_ABNORMALITY_COUNT, player) >= 4
        if key == "waw_3":
            return lambda state: state.count(WAW_ABNORMALITY_COUNT, player) >= 2
        if key == "aleph_3":
            return lambda state: state.count(ALEPH_ABNORMALITY_COUNT, player) >= 3
        if key == "he_ego_5":
            if self.world.ego_item_count > 0:
                return lambda state: state.count(HE_EGO_COUNT, player) >= 5
            return None
        if key == "aleph_ego_8":
            if self.world.ego_item_count > 0:
                return lambda state: state.count(ALEPH_EGO_COUNT, player) >= 8
            return lambda state: state.count(ALEPH_ABNORMALITY_COUNT, player) >= 4
        if key.startswith("agents_"):
            needed = int(key.split("_")[1])
            day = mission.min_day
            return lambda state: self.agent_capacity(state, day) >= needed
        raise KeyError(key)

    def build(self) -> None:
        """Builds the mission and suppression rules (they reference each other by name)."""
        previous: dict[int, Rule] = {}
        for mission in sorted(MISSIONS, key=lambda m: (m.sefira, m.number)):
            day = mission.min_day
            extra = self.requirement(mission.requirement, mission)
            before = previous.get(mission.sefira)

            def rule(state: CollectionState, day=day, extra=extra, before=before) -> bool:
                return (self.can_play(state, day) and (extra is None or extra(state))
                        and (before is None or before(state)))

            self.mission_rules[mission_location_name(mission)] = rule
            previous[mission.sefira] = rule

        named: dict[str, Rule] = dict(self.mission_rules)
        for suppression in SUPPRESSIONS:
            day = suppression.min_day
            prerequisites = tuple(suppression.prerequisites)

            def rule(state: CollectionState, day=day, prerequisites=prerequisites) -> bool:
                return self.can_play(state, day) and all(named[name](state) for name in prerequisites)

            self.suppression_rules[suppression.sefira] = rule
            named[suppression_location_name(suppression.sefira)] = rule

    def suppressions_done(self, state: CollectionState, sefiras: list[int], needed: int) -> bool:
        if needed <= 0:
            return True
        done = 0
        for sefira in sefiras:
            if self.suppression_rules[sefira](state):
                done += 1
                if done >= needed:
                    return True
        return False
