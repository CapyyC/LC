"""
Archipelago world for Lobotomy Corporation (Project Moon, 2018).
"""
from __future__ import annotations

from typing import Any

from BaseClasses import CollectionState, Item, ItemClassification, Region, Tutorial
from Options import OptionError
from worlds.AutoWorld import WebWorld, World
from worlds.LauncherComponents import Component, Type, components, launch

from .data import (
    ABNORMALITIES,
    ABNORMALITY_BY_NAME,
    BASE_ID,
    CONTAINED_BY_DAY,
    DEPARTMENTS,
    EGO_POWER,
    GAME_NAME,
    MAX_AGENTS_PER_DEPARTMENT,
    MAX_CONTAINMENT,
    MISSIONS,
    RESEARCH,
    RESEARCH_GAME_IDS,
    RESEARCH_GAME_NAMES,
    SAFE_STARTERS,
    SUPPRESSION_LAYERS,
    SUPPRESSIONS,
    TOOLS,
    Abnormality,
    Risk,
    agent_slot_item_name,
    ego_suit_name,
    ego_weapon_name,
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
    LobotomyCorporationItem,
    item_descriptions,
    item_name_groups,
    item_name_to_id,
    item_table,
)
from .locations import (
    LobotomyCorporationLocation,
    location_descriptions,
    location_name_groups,
    location_name_to_id,
    location_table,
)
from .options import LobotomyCorporationOptions, option_groups, option_presets
from .rules import LobotomyCorporationLogic

WORLD_VERSION = "0.1.0"


def launch_client(*args: str) -> None:
    from .client import launch as launch_lobcorp_client
    launch(launch_lobcorp_client, name="Lobotomy Corporation Client", args=args)


components.append(Component("Lobotomy Corporation Client", func=launch_client, component_type=Type.CLIENT,
                            game_name=GAME_NAME, supports_uri=True))


class LobotomyCorporationWeb(WebWorld):
    theme = "stone"
    rich_text_options_doc = True
    option_groups = option_groups
    options_presets = option_presets
    item_descriptions = item_descriptions
    location_descriptions = location_descriptions
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up the Lobotomy Corporation randomizer for Archipelago.",
        "English",
        "setup_en.md",
        "setup/en",
        ["Archipelago Lobotomy Corporation contributors"],
    )]


class LobotomyCorporationWorld(World):
    """
    Lobotomy Corporation is a monster management simulation. As the manager of a facility you extract abnormalities,
    order your agents to work with them to harvest energy, and try to survive containment breaches and Ordeals.
    In this randomizer, abnormalities, E.G.O equipment, research, agent slots and resources come from the multiworld.
    """
    game = GAME_NAME
    web = LobotomyCorporationWeb()
    options_dataclass = LobotomyCorporationOptions
    options: LobotomyCorporationOptions
    topology_present = True
    required_client_version = (0, 6, 0)

    item_name_to_id = item_name_to_id
    location_name_to_id = location_name_to_id
    item_name_groups = item_name_groups
    location_name_groups = location_name_groups

    def __init__(self, multiworld, player: int) -> None:
        super().__init__(multiworld, player)
        self.goal_day = 25
        self.containment_count = 0
        self.pool_abnormalities: list[Abnormality] = []
        self.starting_abnormalities: list[Abnormality] = []
        self.ego_item_count = 0
        self.total_ego_power = 0
        self.ego_copies = 0
        self.research_randomized = False
        self.agent_slots_randomized = False
        self.agent_slot_departments: set[int] = set()
        self.agent_slot_items_per_department = 0
        self.agent_slot_counts: dict[int, int] = {}
        self.active_missions: list[str] = []
        self.active_suppressions: list[int] = []
        self.required_suppressions = 0
        self.logic: LobotomyCorporationLogic | None = None
        self.progression_research: set[str] = set()
        self.ego_progression = True
        self.slots_progression = True

    # ----------------------------------------------------------------------------------------------------------------
    # Generation
    # ----------------------------------------------------------------------------------------------------------------
    def generate_early(self) -> None:
        options = self.options
        self.goal_day = options.goal_day.value
        self.containment_count = CONTAINED_BY_DAY[self.goal_day]
        self.ego_copies = options.ego_extraction_checks.value
        self.research_randomized = bool(options.randomize_research.value)
        self.agent_slots_randomized = bool(options.randomize_agent_slots.value)
        if self.agent_slots_randomized:
            self.agent_slot_items_per_department = MAX_AGENTS_PER_DEPARTMENT - options.starting_agent_slots.value
            self.agent_slot_departments = {dept.index for dept in DEPARTMENTS if dept.open_day <= self.goal_day}

        self._choose_abnormalities()

        self.ego_item_count = 0
        self.total_ego_power = 0
        for abno in self.pool_abnormalities:
            for has_type in (abno.weapon, abno.suit):
                if has_type and not abno.tool and self.ego_copies:
                    self.ego_item_count += self.ego_copies
                    self.total_ego_power += EGO_POWER[abno.risk] * self.ego_copies

        self._choose_missions_and_suppressions()

    def _choose_abnormalities(self) -> None:
        options = self.options
        random = self.random
        excluded = set(options.excluded_abnormalities.value)
        needed = self.containment_count
        non_tools = [abno for abno in ABNORMALITIES if abno.name not in excluded]
        tools = [abno for abno in TOOLS if abno.name not in excluded]
        # The option is the tool count of a full 48 abnormality facility; shorter games get proportionally fewer.
        max_tools = min(len(tools), round(options.tool_abnormalities.value * needed / MAX_CONTAINMENT))
        if len(non_tools) + max_tools < needed:
            raise OptionError(f"{self.player_name}: Lobotomy Corporation needs {needed} abnormalities for goal day "
                              f"{self.goal_day}, but only {len(non_tools) + max_tools} are allowed by the excluded "
                              f"abnormalities and tool settings.")

        pool: list[Abnormality] = []

        # Starting abnormalities (never tools, low risk)
        starting_count = min(options.starting_abnormalities.value, needed)
        starters: list[Abnormality] = []
        if options.start_with_one_sin and "One Sin and Hundreds of Good Deeds" not in excluded:
            starters.append(ABNORMALITY_BY_NAME["One Sin and Hundreds of Good Deeds"])
        safe = [ABNORMALITY_BY_NAME[name] for name in SAFE_STARTERS if name not in excluded]
        low_risk = [abno for abno in non_tools if abno.risk <= Risk.TETH]
        for group in (safe, low_risk, non_tools):
            candidates = [abno for abno in group if abno not in starters]
            random.shuffle(candidates)
            while len(starters) < starting_count and candidates:
                starters.append(candidates.pop())
        starters = starters[:starting_count]
        self.starting_abnormalities = starters
        pool += starters

        def tool_count() -> int:
            return sum(1 for abno in pool if abno.tool)

        # Abnormalities the player asked for
        for name in sorted(options.priority_abnormalities.value):
            abno = ABNORMALITY_BY_NAME[name]
            if abno in pool or name in excluded or len(pool) >= needed:
                continue
            if abno.tool and tool_count() >= max_tools:
                continue
            pool.append(abno)

        # Make sure the missions that need specific abnormalities can be completed before the goal
        def ensure(predicate, amount: int, prefer=None) -> None:
            have = sum(1 for abno in pool if predicate(abno))
            candidates = [abno for abno in non_tools if abno not in pool and predicate(abno)]
            random.shuffle(candidates)
            if prefer:
                candidates.sort(key=lambda abno: 0 if prefer(abno) else 1)
            while have < amount and candidates and len(pool) < needed:
                pool.append(candidates.pop(0))
                have += 1

        if self.goal_day >= 44 and self.ego_copies:
            ensure(lambda abno: abno.risk == Risk.ALEPH, 4, prefer=lambda abno: abno.weapon and abno.suit)
        elif self.goal_day >= 34:
            ensure(lambda abno: abno.risk == Risk.ALEPH, 3)
        if self.goal_day >= 33:
            ensure(lambda abno: abno.risk == Risk.WAW, 2)
        if self.goal_day >= 19:
            ensure(lambda abno: abno.breaches, 4)

        # Tools
        remaining_tools = [abno for abno in tools if abno not in pool]
        random.shuffle(remaining_tools)
        while tool_count() < max_tools and remaining_tools and len(pool) < needed:
            pool.append(remaining_tools.pop())

        # Everything else
        remaining = [abno for abno in non_tools if abno not in pool]
        random.shuffle(remaining)
        while len(pool) < needed and remaining:
            pool.append(remaining.pop())
        while len(pool) < needed and remaining_tools:
            pool.append(remaining_tools.pop())

        self.pool_abnormalities = pool

    def _choose_missions_and_suppressions(self) -> None:
        pool = self.pool_abnormalities
        aleph = sum(1 for abno in pool if abno.risk == Risk.ALEPH and not abno.tool)
        waw = sum(1 for abno in pool if abno.risk == Risk.WAW and not abno.tool)
        breaching = sum(1 for abno in pool if abno.breaches)
        he_ego = sum(self.ego_copies * (abno.weapon + abno.suit) for abno in pool
                     if abno.risk >= Risk.HE and not abno.tool)
        aleph_ego = sum(self.ego_copies * (abno.weapon + abno.suit) for abno in pool
                        if abno.risk == Risk.ALEPH and not abno.tool)

        feasible = {
            "": True, "dusk": True, "midnight": True, "level5_agents": True,
            "breachers_4": breaching >= 4,
            "waw_3": waw >= 2,
            "aleph_3": aleph >= 3,
            "he_ego_5": he_ego >= 5 if self.ego_copies else True,
            "aleph_ego_8": aleph_ego >= 8 if self.ego_copies else aleph >= 4,
            "agents_3": True, "agents_5": True, "agents_8": True, "agents_10": True,
        }
        blocked_sephirah: set[int] = set()
        active: list[str] = []
        for mission in sorted(MISSIONS, key=lambda m: (m.sefira, m.number)):
            if mission.sefira in blocked_sephirah:
                continue
            if mission.min_day > self.goal_day or not feasible[mission.requirement]:
                blocked_sephirah.add(mission.sefira)
                continue
            active.append(mission_location_name(mission))
        self.active_missions = active

        active_names = set(active)
        suppressions: list[int] = []
        for suppression in SUPPRESSIONS:
            if suppression.min_day > self.goal_day:
                continue
            prerequisites_ok = all(
                name in active_names or name in {suppression_location_name(s) for s in suppressions}
                for name in suppression.prerequisites)
            if prerequisites_ok:
                suppressions.append(suppression.sefira)
        self.active_suppressions = suppressions

        # The Kether days require whole layers of Core Suppressions; make sure the goal is reachable.
        if self.goal_day >= 47:
            layers_needed = {"upper"} | ({"middle"} if self.goal_day >= 48 else set()) | (
                {"lower"} if self.goal_day >= 49 else set())
            for layer in layers_needed:
                missing = [s for s in SUPPRESSION_LAYERS[layer] if s not in suppressions]
                if missing:
                    raise OptionError(f"{self.player_name}: goal day {self.goal_day} needs every {layer} layer Core "
                                      f"Suppression, but the excluded abnormalities make some Sephirah missions "
                                      f"impossible.")

        self.required_suppressions = min(self.options.required_core_suppressions.value, len(suppressions))

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu)
        day_regions: dict[int, Region] = {}
        previous = menu
        for day in range(1, self.goal_day + 1):
            region = Region(f"Day {day:02d}", self.player, self.multiworld)
            self.multiworld.regions.append(region)
            day_regions[day] = region
            previous.connect(region, f"Start Day {day:02d}")
            previous = region

        def add(name: str, day: int) -> None:
            region = day_regions[day]
            region.locations.append(LobotomyCorporationLocation(self.player, name, location_name_to_id[name], region))

        options = self.options
        for name, data in location_table.items():
            if data.kind == "day":
                if data.index <= self.goal_day:
                    add(name, data.index)
            elif data.kind in ("observation", "ego_weapon", "ego_suit", "ego_gift"):
                if data.index > self.containment_count:
                    continue
                if data.kind == "observation" and data.level > options.observation_checks.value:
                    continue
                if data.kind in ("ego_weapon", "ego_suit") and data.level > self.ego_copies:
                    continue
                if data.kind == "ego_gift" and not options.ego_gift_checks:
                    continue
                add(name, data.min_day)

        for name in self.active_missions:
            add(name, location_table[name].min_day)
        for sefira in self.active_suppressions:
            name = suppression_location_name(sefira)
            add(name, location_table[name].min_day)

        goal_region = day_regions[self.goal_day]
        goal = LobotomyCorporationLocation(self.player, "Facility Goal", None, goal_region)
        goal.place_locked_item(LobotomyCorporationItem("Victory", ItemClassification.progression, None, self.player))
        goal_region.locations.append(goal)

    def create_item(self, name: str) -> LobotomyCorporationItem:
        data = item_table[name]
        classification = data.classification
        if data.kind == "research":
            classification = (ItemClassification.progression if name in self.progression_research
                              else ItemClassification.useful)
        elif data.kind in ("ego_weapon", "ego_suit"):
            classification = ItemClassification.progression if self.ego_progression else ItemClassification.useful
        elif data.kind == "agent_slot":
            classification = (ItemClassification.progression if self.slots_progression
                              else ItemClassification.useful)
        return LobotomyCorporationItem(name, classification, data.code, self.player)

    def create_items(self) -> None:
        options = self.options
        self.agent_slot_counts = dict.fromkeys(self.agent_slot_departments, self.agent_slot_items_per_department)
        self.logic = LobotomyCorporationLogic(self)
        last_day = self.goal_day
        self.progression_research = set(self.logic.logic_research(last_day))
        missions = set(self.active_missions)
        self.ego_progression = (self.logic.uses_power(last_day)
                                or any(location_table[m].min_day >= 42 for m in missions if "Binah" in m))
        self.slots_progression = self.logic.uses_slots(last_day) or any("Hokma" in m for m in missions)

        for abno in self.starting_abnormalities:
            self.push_precollected(self.create_item(abno.name))

        required: list[Item] = [self.create_item(abno.name) for abno in self.pool_abnormalities
                                if abno not in self.starting_abnormalities]

        ego_items: list[Item] = []
        extra_ego_items: list[Item] = []
        for abno in self.pool_abnormalities:
            if abno.tool:
                continue
            for has_type, name in ((abno.weapon, ego_weapon_name(abno)), (abno.suit, ego_suit_name(abno))):
                if not has_type:
                    continue
                for copy in range(self.ego_copies):
                    (ego_items if copy == 0 else extra_ego_items).append(self.create_item(name))

        research_items: list[Item] = []
        if self.research_randomized:
            for research in RESEARCH:
                if research.name == "TT2 Protocol" and options.start_with_tt2_protocol:
                    self.push_precollected(self.create_item(research.name))
                    continue
                research_items.append(self.create_item(research.name))

        slot_items: list[Item] = []
        for dept in DEPARTMENTS:
            if dept.index in self.agent_slot_departments:
                slot_items += [self.create_item(agent_slot_item_name(dept))
                               for _ in range(self.agent_slot_items_per_department)]

        locations = len(self.multiworld.get_unfilled_locations(self.player))
        logic_research = [item for item in research_items if item.name in self.progression_research]
        optional_research = [item for item in research_items if item.name not in self.progression_research]
        # Research of departments that open during the game is more relevant than research of later departments.
        research_day = {r.name: next((d.open_day for d in DEPARTMENTS if d.sefira == r.sefira), 1) for r in RESEARCH}
        optional_research.sort(key=lambda item: research_day[item.name])

        pool: list[Item] = required + logic_research
        if len(pool) > locations:
            raise OptionError(f"{self.player_name}: not enough Lobotomy Corporation locations for the required "
                              f"items; raise the observation checks or the goal day.")

        # Progression items that can be trimmed when there is not enough room, most important first.
        def power(item: Item) -> int:
            return EGO_POWER[item_table[item.name].abnormality.risk]

        flexible = sorted((item for item in ego_items if power(item) >= 3), key=power, reverse=True)
        flexible += slot_items
        flexible += sorted((item for item in ego_items if power(item) < 3), key=power, reverse=True)
        kept = flexible[:locations - len(pool)]
        pool += kept

        for item in optional_research + extra_ego_items:
            if len(pool) >= locations:
                break
            pool.append(item)
            kept.append(item)

        final_ego = [item for item in kept if item_table[item.name].kind in ("ego_weapon", "ego_suit")]
        self.ego_item_count = len(final_ego)
        self.total_ego_power = sum(power(item) for item in final_ego)
        self.agent_slot_counts = {}
        for item in kept:
            if item_table[item.name].kind == "agent_slot":
                dept = next(d for d in DEPARTMENTS if agent_slot_item_name(d) == item.name)
                self.agent_slot_counts[dept.index] = self.agent_slot_counts.get(dept.index, 0) + 1
        # Rebuild the logic with the final item counts so that every expectation is satisfiable.
        self.logic = LobotomyCorporationLogic(self)

        while len(pool) < locations:
            pool.append(self.create_filler())

        self.multiworld.itempool += pool

    def set_rules(self) -> None:
        logic = self.logic
        logic.build()
        player = self.player

        for day in range(1, self.goal_day + 1):
            entrance = self.multiworld.get_entrance(f"Start Day {day:02d}", player)
            entrance.access_rule = lambda state, day=day: logic.can_play(state, day)

        for name in self.active_missions:
            self.get_location(name).access_rule = logic.mission_rules[name]
        for sefira in self.active_suppressions:
            self.get_location(suppression_location_name(sefira)).access_rule = logic.suppression_rules[sefira]

        required = self.required_suppressions
        suppressions = list(self.active_suppressions)
        self.get_location("Facility Goal").access_rule = (
            lambda state: logic.suppressions_done(state, suppressions, required))
        self.multiworld.completion_condition[player] = lambda state: state.has("Victory", player)

    def get_filler_item_name(self) -> str:
        options = self.options
        if options.trap_percentage.value and self.random.randint(1, 100) <= options.trap_percentage.value:
            weights = {
                "Dawn Ordeal Trap": options.ordeal_trap_weight.value * 4,
                "Noon Ordeal Trap": options.ordeal_trap_weight.value * 3,
                "Dusk Ordeal Trap": options.ordeal_trap_weight.value * 2,
                "Midnight Ordeal Trap": options.ordeal_trap_weight.value,
                "Abnormality Breach Trap": options.breach_trap_weight.value * 4,
                "Qliphoth Meltdown Trap": options.meltdown_trap_weight.value * 4,
                "Energy Drain Trap": options.energy_drain_trap_weight.value * 4,
                "Panic Trap": options.panic_trap_weight.value * 4,
            }
            if sum(weights.values()):
                return self.random.choices(list(weights), list(weights.values()))[0]
        filler_weights = {"LOB Points": 35, "PE Box Shipment": 30, "Enkephalin Canister": 15, "Training Manual": 15,
                          "Emergency Medical Supplies": 5}
        return self.random.choices(list(filler_weights), list(filler_weights.values()))[0]

    # ----------------------------------------------------------------------------------------------------------------
    # Logic counters
    # ----------------------------------------------------------------------------------------------------------------
    def collect(self, state: CollectionState, item: Item) -> bool:
        changed = super().collect(state, item)
        if changed:
            self._update_counters(state, item, 1)
        return changed

    def remove(self, state: CollectionState, item: Item) -> bool:
        changed = super().remove(state, item)
        if changed:
            self._update_counters(state, item, -1)
        return changed

    def _update_counters(self, state: CollectionState, item: Item, sign: int) -> None:
        data = item_table.get(item.name)
        if data is None or data.abnormality is None:
            return
        counters = state.prog_items[self.player]
        abno = data.abnormality
        if data.kind in ("abnormality", "tool"):
            counters[ABNORMALITY_COUNT] += sign
            if abno.risk == Risk.WAW and not abno.tool:
                counters[WAW_ABNORMALITY_COUNT] += sign
            if abno.risk == Risk.ALEPH and not abno.tool:
                counters[ALEPH_ABNORMALITY_COUNT] += sign
            if abno.breaches:
                counters[BREACHING_ABNORMALITY_COUNT] += sign
        elif data.kind in ("ego_weapon", "ego_suit"):
            counters[EGO_POWER_COUNT] += sign * EGO_POWER[abno.risk]
            if abno.risk >= Risk.HE:
                counters[HE_EGO_COUNT] += sign
            if abno.risk == Risk.ALEPH:
                counters[ALEPH_EGO_COUNT] += sign

    # ----------------------------------------------------------------------------------------------------------------
    # Output
    # ----------------------------------------------------------------------------------------------------------------
    def fill_slot_data(self) -> dict[str, Any]:
        options = self.options
        research = {}
        for entry in RESEARCH:
            research[str(item_name_to_id[entry.name])] = [entry.sefira, entry.slot,
                                                          RESEARCH_GAME_NAMES.get(entry.name, entry.name),
                                                          RESEARCH_GAME_IDS[entry.name]]
        agent_slot_items = {}
        for dept in DEPARTMENTS:
            agent_slot_items[str(item_name_to_id[agent_slot_item_name(dept)])] = dept.sefira
        missions = {}
        for mission in MISSIONS:
            name = mission_location_name(mission)
            missions[str(mission.game_id)] = [mission.sefira, mission.number, location_name_to_id[name]]
        suppressions = {str(s.sefira): location_name_to_id[suppression_location_name(s.sefira)]
                        for s in SUPPRESSIONS}
        return {
            "world_version": WORLD_VERSION,
            "base_id": BASE_ID,
            "goal_day": self.goal_day,
            "required_core_suppressions": self.required_suppressions,
            "containment_count": self.containment_count,
            "abnormality_pool": [abno.game_id for abno in self.pool_abnormalities],
            "starting_abnormalities": [abno.game_id for abno in self.starting_abnormalities],
            "extraction_offers": options.extraction_offers.value,
            "observation_checks": options.observation_checks.value,
            "ego_extraction_checks": self.ego_copies,
            "ego_gift_checks": bool(options.ego_gift_checks.value),
            "extraction_after_checks": options.extraction_after_checks.value,
            "randomize_research": self.research_randomized,
            "research": research,
            "randomize_agent_slots": self.agent_slots_randomized,
            "starting_agent_slots": options.starting_agent_slots.value,
            "agent_slot_items": agent_slot_items,
            "missions": missions,
            "suppressions": suppressions,
            "lob_points_per_item": options.lob_points_per_item.value,
            "pe_boxes_per_item": options.pe_boxes_per_item.value,
            "death_link": bool(options.death_link.value),
            "death_link_trigger": options.death_link_trigger.value,
            "death_link_effect": options.death_link_effect.value,
        }

    def write_spoiler(self, spoiler_handle) -> None:
        spoiler_handle.write(f"\n\nLobotomy Corporation abnormality pool ({self.player_name}):\n")
        for abno in sorted(self.pool_abnormalities, key=lambda a: (a.risk, a.name)):
            start = " (starting)" if abno in self.starting_abnormalities else ""
            spoiler_handle.write(f"    {abno.risk.name:5} {abno.name}{start}\n")
