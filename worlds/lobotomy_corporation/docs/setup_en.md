# Lobotomy Corporation Randomizer Setup Guide

## Required software

- [Lobotomy Corporation](https://store.steampowered.com/app/568220/) (Steam, Windows)
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases) 0.6.4 or newer
- The Lobotomy Corporation randomizer release:
  - `lobotomy_corporation.apworld`
  - the `LobotomyArchipelago` mod folder

The mod is loaded by the game's built-in mod loader (`BaseMods` folder). It also works together with
[BaseMod](https://www.nexusmods.com/lobotomycorporation/mods/2) or the Lobotomy Mod Manager, but neither is required.

## Installation

### Archipelago world

Double-click `lobotomy_corporation.apworld` (or copy it into the `custom_worlds` folder of your Archipelago
installation). "Lobotomy Corporation Client" now appears in the Archipelago Launcher.

### Game mod

1. Open the game folder (Steam: right-click Lobotomy Corporation → Manage → Browse local files).
2. Copy the `LobotomyArchipelago` folder into `LobotomyCorp_Data\BaseMods\`, so that you have
   `LobotomyCorp_Data\BaseMods\LobotomyArchipelago\LobotomyArchipelago.dll`. Create the `BaseMods` folder if it does not
   exist.

### Use a fresh save

Observation levels, PE boxes and E.G.O equipment are shared between runs, so every seed needs a fresh save:

1. Close the game.
2. Back up and then remove the save folder `%USERPROFILE%\AppData\LocalLow\Project_Moon\Lobotomy`
   (keep the `Archipelago` subfolder if you want to keep the mod's logs and per-seed progress files).
3. Restore your backup when you want to play the normal game again.

## Create your options file

Use the [player options page](../player-options) or the template (`Lobotomy Corporation.yaml`) that the Archipelago
Launcher generates with "Generate Template Options". The most important options:

| Option | Default | Description |
|---|---|---|
| `goal_day` | 25 | Day you have to clear (50 = true ending) |
| `required_core_suppressions` | 0 | Core Suppressions needed in addition to the goal day |
| `starting_abnormalities` | 2 | Abnormalities you start with |
| `observation_checks` | 4 | Observation levels per abnormality that are checks |
| `ego_extraction_checks` | 1 | E.G.O weapon/suit extractions per abnormality that are checks (0 = vanilla E.G.O) |
| `randomize_research` | true | Research comes from the item pool |
| `randomize_agent_slots` | true | Agent slots per department come from the item pool |
| `logic_difficulty` | normal | How much E.G.O, slots and bullets logic expects before later days |
| `trap_percentage` | 20 | Share of filler replaced by traps |

## Joining a multiworld game

1. Start the **Lobotomy Corporation Client** from the Archipelago Launcher and connect it to the server
   (`/connect address:port`, then enter your slot name).
2. Start Lobotomy Corporation. The overlay in the top right corner shows the connection state; it says "Connected as
   <slot>" once the mod, the client and the server are linked.
3. Start a new game (or continue your randomizer save).

The client and the mod talk over `127.0.0.1:38269`. If that port is taken, start the client with `--port <port>` and
put the same port into `LobotomyCorp_Data\BaseMods\LobotomyArchipelago\config.json`.

You can also play offline: the mod remembers the last session and sends queued checks the next time it connects.

## Playing

- On extraction days only received abnormalities are offered (oldest first). If not enough abnormality items are
  available for the next day, the "next day" button waits until more arrive. **F11** lets you continue anyway (the game
  then picks a vanilla abnormality, which uses up a containment unit).
- Spend PE boxes on E.G.O like normal to send extraction checks.
- Hotkeys: **F8** overlay, **F9** diagnostics report, **F10** resynchronize items, **F11** stop waiting for abnormality
  items.

## Troubleshooting

- The mod writes `LobotomyArchipelago.log` and (F9) `diagnostics.txt` to
  `%USERPROFILE%\AppData\LocalLow\Project_Moon\Lobotomy\Archipelago`.
- The diagnostics report lists every game hook the mod could attach ("Patches"). Features whose hook is reported as
  missing are disabled; please report them together with the game version.
- If a research item unlocks the wrong research, press F9 to see the game's research ids and put the right id into
  `research_overrides.json` next to the mod dll (see `research_overrides.example.json`).
