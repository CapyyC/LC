from ..data import CONTAINED_BY_DAY, MAX_CONTAINMENT, day_of_containment, extractions_on_day
from ..items import ABNORMALITY_COUNT
from .bases import LobotomyCorporationTestBase


class TestSchedule(LobotomyCorporationTestBase):
    run_default_tests = False

    def test_extraction_schedule(self) -> None:
        self.assertEqual(CONTAINED_BY_DAY[1], 1)
        self.assertEqual(CONTAINED_BY_DAY[4], 4)
        self.assertEqual(CONTAINED_BY_DAY[5], 4, "no extraction on days ending in 5 or 0")
        self.assertEqual(CONTAINED_BY_DAY[20], 16)
        self.assertEqual(CONTAINED_BY_DAY[21], 18, "Central Command gets two units per day")
        self.assertEqual(CONTAINED_BY_DAY[45], 40)
        self.assertEqual(MAX_CONTAINMENT, 48)
        self.assertEqual(extractions_on_day(50), 0)
        self.assertEqual(day_of_containment(1), 1)
        self.assertEqual(day_of_containment(5), 6)
        self.assertEqual(day_of_containment(18), 21)


class TestDefaultLogic(LobotomyCorporationTestBase):
    options = {
        "goal_day": 25,
        "starting_abnormalities": 1,
        "logic_difficulty": "normal",
    }

    def test_pool_size(self) -> None:
        world = self.world
        self.assertEqual(len(world.pool_abnormalities), CONTAINED_BY_DAY[25])
        self.assertEqual(len(world.starting_abnormalities), 1)
        names = [abno.name for abno in world.pool_abnormalities]
        self.assertEqual(len(names), len(set(names)), "pool abnormalities must be unique")
        self.assertEqual(world.starting_abnormalities[0].name, "One Sin and Hundreds of Good Deeds")

    def test_abnormalities_gate_days(self) -> None:
        state = self.multiworld.state
        self.assertEqual(state.count(ABNORMALITY_COUNT, self.player), 1)
        self.assertTrue(self.can_reach_location("Day 01 Cleared"))
        self.assertTrue(self.can_reach_location("Abnormality #01 - Observation Level 4"))
        self.assertFalse(self.can_reach_location("Day 02 Cleared"))
        self.assertFalse(self.can_reach_location("Abnormality #02 - Observation Level 1"))

        others = [abno.name for abno in self.world.pool_abnormalities if abno not in self.world.starting_abnormalities]
        self.collect_by_name(others[0])
        self.assertTrue(self.can_reach_location("Day 02 Cleared"))
        self.assertTrue(self.can_reach_location("Abnormality #02 - Observation Level 1"))
        self.assertFalse(self.can_reach_location("Day 03 Cleared"))

    def test_goal_requires_everything_needed(self) -> None:
        self.assertBeatable(False)
        self.collect_all_but(["Victory"])
        self.assertBeatable(True)


class TestShortGame(LobotomyCorporationTestBase):
    options = {
        "goal_day": 5,
        "observation_checks": 1,
        "ego_extraction_checks": 0,
    }

    def test_no_late_locations(self) -> None:
        names = {location.name for location in self.multiworld.get_locations(self.player)}
        self.assertIn("Day 05 Cleared", names)
        self.assertNotIn("Day 06 Cleared", names)
        self.assertNotIn("Abnormality #05 - Observation Level 1", names)
        self.assertNotIn("Malkuth Mission 4 - When Dusk Falls", names)


class TestMinimalLogic(LobotomyCorporationTestBase):
    options = {
        "goal_day": 30,
        "logic_difficulty": "minimal",
        "randomize_agent_slots": False,
        "randomize_research": False,
    }

    def test_only_abnormalities_gate_days(self) -> None:
        abnos = [abno.name for abno in self.world.pool_abnormalities if abno not in self.world.starting_abnormalities]
        self.collect_by_name(abnos)
        self.assertTrue(self.can_reach_location("Day 30 Cleared"))


class TestFullGame(LobotomyCorporationTestBase):
    options = {
        "goal_day": 50,
        "required_core_suppressions": 9,
        "ego_extraction_checks": 2,
        "ego_gift_checks": True,
        "logic_difficulty": "easy",
    }

    def test_kether_needs_suppressions(self) -> None:
        self.assertEqual(self.world.required_suppressions, 9)
        self.assertEqual(len(self.world.active_suppressions), 9)
        self.assertEqual(len(self.world.pool_abnormalities), MAX_CONTAINMENT)


class TestHardNoTools(LobotomyCorporationTestBase):
    options = {
        "goal_day": 40,
        "tool_abnormalities": 0,
        "logic_difficulty": "hard",
        "trap_percentage": 100,
        "starting_agent_slots": 4,
    }

    def test_no_tools(self) -> None:
        self.assertFalse(any(abno.tool for abno in self.world.pool_abnormalities))
