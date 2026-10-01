"""
Lobotomy Corporation Client.

The game mod cannot talk to the Archipelago server directly (the game runs on an old Mono runtime without modern TLS),
so this client connects to the server and relays everything to the mod over a local TCP socket, one JSON object per
line.

Mod -> client:  Hello, LocationChecks, Goal, DeathLink, Sync, Log
Client -> mod:  Connected, ReceivedItems, LocationsChecked, Print, DeathLink, Disconnected
"""
from __future__ import annotations

import asyncio
import copy
import json
import logging
from typing import Any

import Utils
from CommonClient import (
    ClientCommandProcessor,
    CommonContext,
    get_base_parser,
    gui_enabled,
    handle_url_arg,
    logger,
    server_loop,
)
from NetUtils import ClientStatus, NetworkItem

from .data import GAME_NAME

DEFAULT_MOD_PORT = 38269
MOD_PROTOCOL_VERSION = 1


class LobotomyCorporationCommandProcessor(ClientCommandProcessor):
    def _cmd_mod(self) -> bool:
        """Show the status of the connection to the game mod."""
        ctx = self.ctx
        if isinstance(ctx, LobotomyCorporationContext):
            if ctx.mod_writer is not None:
                logger.info(f"Game mod connected (mod version {ctx.mod_version or 'unknown'}).")
            else:
                logger.info(f"Game mod not connected. Waiting on 127.0.0.1:{ctx.mod_port}.")
        return True

    def _cmd_resync(self) -> bool:
        """Resend everything (slot data and all received items) to the game mod."""
        ctx = self.ctx
        if isinstance(ctx, LobotomyCorporationContext):
            Utils.async_start(ctx.send_full_state())
        return True


class LobotomyCorporationContext(CommonContext):
    game = GAME_NAME
    items_handling = 0b111  # receive own items, items from other worlds and starting inventory
    command_processor = LobotomyCorporationCommandProcessor

    def __init__(self, server_address: str | None, password: str | None, mod_port: int = DEFAULT_MOD_PORT):
        super().__init__(server_address, password)
        self.mod_port = mod_port
        self.mod_writer: asyncio.StreamWriter | None = None
        self.mod_version: str | None = None
        self.mod_server: asyncio.base_events.Server | None = None
        self.slot_data: dict[str, Any] = {}
        self.connected_to_server = False
        self.goal_sent = False

    # ----------------------------------------------------------------------------------------------------------------
    # Archipelago server side
    # ----------------------------------------------------------------------------------------------------------------
    async def server_auth(self, password_requested: bool = False):
        if password_requested and not self.password:
            await super().server_auth(password_requested)
        await self.get_username()
        await self.send_connect()

    def on_package(self, cmd: str, args: dict) -> None:
        if cmd == "Connected":
            self.slot_data = args.get("slot_data", {}) or {}
            self.connected_to_server = True
            self.goal_sent = False
            Utils.async_start(self.update_death_link(bool(self.slot_data.get("death_link", False))))
            Utils.async_start(self.send_full_state())
        elif cmd == "ReceivedItems":
            start = args["index"]
            items = [self._item_to_json(NetworkItem(*item)) for item in args["items"]]
            Utils.async_start(self.send_to_mod({"cmd": "ReceivedItems", "index": start, "items": items}))
        elif cmd == "RoomUpdate":
            if "checked_locations" in args:
                Utils.async_start(self.send_to_mod({"cmd": "LocationsChecked",
                                                   "locations": list(args["checked_locations"])}))

    def on_print_json(self, args: dict) -> None:
        super().on_print_json(args)
        if not self.mod_writer:
            return
        msg_type = args.get("type", "")
        relevant = msg_type in ("Chat", "ServerChat", "Goal", "Countdown")
        if msg_type in ("ItemSend", "ItemCheat", "Hint"):
            item = args.get("item")
            receiving = args.get("receiving")
            relevant = receiving == self.slot or (item is not None and self.slot_concerns_self(item.player))
        if relevant:
            text = self.rawjsontotextparser(copy.deepcopy(args["data"]))
            Utils.async_start(self.send_to_mod({"cmd": "Print", "text": text}))

    def on_deathlink(self, data: dict[str, Any]) -> None:
        super().on_deathlink(data)
        Utils.async_start(self.send_to_mod({"cmd": "DeathLink", "source": data.get("source", ""),
                                           "cause": data.get("cause", "")}))

    async def connection_closed(self):
        await super().connection_closed()
        self.connected_to_server = False
        await self.send_to_mod({"cmd": "Disconnected"})

    async def disconnect(self, allow_autoreconnect: bool = False):
        self.connected_to_server = False
        await super().disconnect(allow_autoreconnect)
        await self.send_to_mod({"cmd": "Disconnected"})

    def _item_name(self, code: int) -> str:
        try:
            return self.item_names.lookup_in_slot(code, self.slot)
        except KeyError:
            return self.item_names.lookup_in_game(code, GAME_NAME)

    def _item_to_json(self, item: NetworkItem) -> dict[str, Any]:
        sender = self.player_names.get(item.player, f"Player {item.player}")
        return {
            "item": item.item,
            "location": item.location,
            "player": item.player,
            "flags": item.flags,
            "item_name": self._item_name(item.item),
            "sender": sender,
        }

    # ----------------------------------------------------------------------------------------------------------------
    # Game mod side
    # ----------------------------------------------------------------------------------------------------------------
    async def start_mod_server(self) -> None:
        try:
            self.mod_server = await asyncio.start_server(self.handle_mod, "127.0.0.1", self.mod_port)
        except OSError as error:
            logger.error(f"Could not listen on 127.0.0.1:{self.mod_port} for the game mod ({error}). Start the client "
                         f"with --port <port> and set the same port in the mod's config.json.")
            return
        logger.info(f"Waiting for the Lobotomy Corporation mod on 127.0.0.1:{self.mod_port}")

    async def handle_mod(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        if self.mod_writer is not None:
            try:
                self.mod_writer.close()
            except Exception:
                pass
        self.mod_writer = writer
        logger.info("Lobotomy Corporation mod connected.")
        try:
            while not reader.at_eof():
                line = await reader.readline()
                if not line:
                    break
                try:
                    message = json.loads(line.decode("utf-8"))
                except json.JSONDecodeError:
                    logger.warning(f"Invalid message from the game mod: {line!r}")
                    continue
                await self.handle_mod_message(message)
        except (ConnectionError, asyncio.IncompleteReadError):
            pass
        finally:
            if self.mod_writer is writer:
                self.mod_writer = None
                logger.info("Lobotomy Corporation mod disconnected.")

    async def handle_mod_message(self, message: dict[str, Any]) -> None:
        cmd = message.get("cmd")
        if cmd == "Hello":
            self.mod_version = message.get("version")
            if message.get("protocol", MOD_PROTOCOL_VERSION) != MOD_PROTOCOL_VERSION:
                logger.warning("The game mod uses a different protocol version than this client. "
                               "Please update both to the same release.")
            await self.send_full_state()
        elif cmd == "Sync":
            await self.send_full_state()
        elif cmd == "LocationChecks":
            locations = [int(location) for location in message.get("locations", [])]
            if self.connected_to_server and locations:
                await self.check_locations(locations)
        elif cmd == "Goal":
            if self.connected_to_server and not self.goal_sent:
                self.goal_sent = True
                await self.send_msgs([{"cmd": "StatusUpdate", "status": ClientStatus.CLIENT_GOAL}])
                logger.info("Goal completed!")
        elif cmd == "DeathLink":
            if "DeathLink" in self.tags:
                await self.send_death(message.get("cause", "") or f"{self.player_names.get(self.slot, 'A manager')}"
                                                                  f"'s facility fell into chaos.")
        elif cmd == "Log":
            logger.info(f"[Game] {message.get('text', '')}")
        else:
            logger.debug(f"Unknown message from the game mod: {message}")

    async def send_full_state(self) -> None:
        if not self.connected_to_server or self.slot is None:
            await self.send_to_mod({"cmd": "Disconnected"})
            return
        await self.send_to_mod({
            "cmd": "Connected",
            "protocol": MOD_PROTOCOL_VERSION,
            "seed": self.seed_name or "",
            "slot": self.slot,
            "team": self.team,
            "slot_name": self.player_names.get(self.slot, ""),
            "slot_data": self.slot_data,
            "checked_locations": sorted(self.checked_locations),
            "missing_locations": sorted(self.missing_locations),
        })
        items = [self._item_to_json(item) for item in self.items_received]
        await self.send_to_mod({"cmd": "ReceivedItems", "index": 0, "items": items})

    async def send_to_mod(self, message: dict[str, Any]) -> None:
        writer = self.mod_writer
        if writer is None:
            return
        try:
            writer.write((json.dumps(message, ensure_ascii=False) + "\n").encode("utf-8"))
            await writer.drain()
        except (ConnectionError, RuntimeError) as error:
            logger.debug(f"Could not send to the game mod: {error}")
            if self.mod_writer is writer:
                self.mod_writer = None

    async def shutdown(self):
        if self.mod_server is not None:
            self.mod_server.close()
        await super().shutdown()

    def make_gui(self):
        ui = super().make_gui()
        ui.base_title = "Archipelago Lobotomy Corporation Client"
        return ui


async def main(args) -> None:
    ctx = LobotomyCorporationContext(args.connect, args.password, args.port)
    ctx.auth = args.name
    ctx.server_task = asyncio.create_task(server_loop(ctx), name="server loop")
    await ctx.start_mod_server()
    if gui_enabled:
        ctx.run_gui()
    ctx.run_cli()
    await ctx.exit_event.wait()
    await ctx.shutdown()


def launch(*launch_args: str) -> None:
    import colorama

    parser = get_base_parser(description="Lobotomy Corporation Archipelago client.")
    parser.add_argument("--name", default=None, help="Slot name to connect as.")
    parser.add_argument("--port", default=DEFAULT_MOD_PORT, type=int,
                        help="Local port the game mod connects to (default %(default)s).")
    parser.add_argument("url", nargs="?", help="Archipelago connection url")
    args = handle_url_arg(parser.parse_args(launch_args), parser)

    colorama.just_fix_windows_console()
    asyncio.run(main(args))
    colorama.deinit()


if __name__ == "__main__":
    logging.getLogger().setLevel(logging.INFO)
    launch()
