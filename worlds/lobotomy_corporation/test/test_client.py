import asyncio
import json
import unittest

from NetUtils import NetworkItem, NetworkSlot, SlotType

from ..client import LobotomyCorporationContext
from ..data import BASE_ID


class FakeWriter:
    def __init__(self) -> None:
        self.lines: list[dict] = []

    def write(self, data: bytes) -> None:
        for line in data.decode("utf-8").splitlines():
            self.lines.append(json.loads(line))

    async def drain(self) -> None:
        pass

    def close(self) -> None:
        pass


class TestClientRelay(unittest.TestCase):
    def test_relay_to_mod(self) -> None:
        async def run() -> None:
            ctx = LobotomyCorporationContext(None, None)
            writer = FakeWriter()
            ctx.mod_writer = writer

            # Not connected to a server yet: the mod is told so and checks are not forwarded.
            await ctx.handle_mod_message({"cmd": "Hello", "version": "test", "protocol": 1})
            self.assertEqual(writer.lines[-1]["cmd"], "Disconnected")
            await ctx.handle_mod_message({"cmd": "LocationChecks", "locations": [BASE_ID + 11_000_001]})

            # Pretend the server connection is up.
            ctx.connected_to_server = True
            ctx.slot = 1
            ctx.team = 0
            ctx.seed_name = "TESTSEED"
            ctx.slot_data = {"goal_day": 10}
            ctx.player_names = {0: "Archipelago", 1: "Manager"}
            ctx.slot_info = {1: NetworkSlot("Manager", "Lobotomy Corporation", SlotType.player)}
            ctx.items_received = [NetworkItem(BASE_ID + 100009, -2, 1, 1)]
            await ctx.send_full_state()
            connected, received = writer.lines[-2], writer.lines[-1]
            self.assertEqual(connected["cmd"], "Connected")
            self.assertEqual(connected["seed"], "TESTSEED")
            self.assertEqual(connected["slot_data"], {"goal_day": 10})
            self.assertEqual(received["cmd"], "ReceivedItems")
            self.assertEqual(received["index"], 0)
            self.assertEqual(received["items"][0]["item"], BASE_ID + 100009)
            self.assertEqual(received["items"][0]["sender"], "Manager")
            self.assertEqual(received["items"][0]["item_name"], "One Sin and Hundreds of Good Deeds")

            ctx.on_package("ReceivedItems", {"index": 1, "items": [NetworkItem(BASE_ID + 100012, 5, 1, 1)]})
            await asyncio.sleep(0)
            self.assertEqual(writer.lines[-1]["index"], 1)

        asyncio.run(run())
