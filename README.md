# Lobotomy Corporation × Archipelago

An [Archipelago](https://archipelago.gg) multiworld randomizer for
[Lobotomy Corporation](https://store.steampowered.com/app/568220/) (Project Moon).

The repository contains:

| Part | Path | What it is |
|---|---|---|
| APWorld | [`worlds/lobotomy_corporation`](worlds/lobotomy_corporation) | Items, locations, logic, options, docs, tests and the **Lobotomy Corporation Client** |
| Packaged APWorld | [`dist/lobotomy_corporation.apworld`](dist/lobotomy_corporation.apworld) | Drop-in file for Archipelago 0.6.4+ |
| Game mod (source) | [`mod/LobotomyArchipelago`](mod/LobotomyArchipelago) | C# (.NET 3.5) BaseMods mod that talks to the client |
| Game mod (build) | [`dist/BaseMods/LobotomyArchipelago`](dist/BaseMods/LobotomyArchipelago) | Copy into `LobotomyCorp_Data/BaseMods/` |
| YAML template | [`templates/Lobotomy Corporation.yaml`](templates/Lobotomy%20Corporation.yaml) | Player options template |
| Mod test harness | [`mod/tests`](mod/tests) | Simulated game + driver that tests the mod against a mock or a real server |

Player documentation: [game page](worlds/lobotomy_corporation/docs/en_Lobotomy%20Corporation.md) ·
[setup guide](worlds/lobotomy_corporation/docs/setup_en.md).

## What is randomized

| Requested | How it works |
|---|---|
| **Abnormalities** | One item per abnormality (65 regular + 17 tools). Extraction days only offer abnormalities you have received; the next day waits until enough have arrived. |
| **Bullet shields** | `Physical Intervention Shield Bullet`, `Trauma Shield Bullet`, `Erosion Shield Bullet` research items. |
| **Rabbit squads** | `Rabbit Team Protocol` research item. |
| **Execution bullets** | `Execution Bullet` research item (plus the slow, HP-N, SP-E and refining bullets). |
| **Quest rewards** | Completing any of the 36 Sephirah missions is a location check instead of a research pick; all 27 researches are items. |
| **E.G.O gear / E.G.O weapons** | Extracting an E.G.O suit or weapon is a check; the equipment itself (`Penitence (E.G.O Weapon)`, ...) is an item. |
| **Employee slots** | Every department starts with 2 agent slots (configurable); `<Department> Agent Slot` items raise it up to 5. |
| **LOB points** | `LOB Points` filler items. |
| **PE boxes** | `PE Box Shipment` filler items (PE boxes for a random abnormality) and `Enkephalin Canister` (energy). |
| *Extra* | Observation levels, cleared days, Core Suppressions and (optionally) E.G.O gifts are checks; `Training Manual` and `Emergency Medical Supplies` filler; DeathLink. |
| **Traps: Ordeals** | `Dawn/Noon/Dusk/Midnight Ordeal Trap` (downgraded when the day is too early for the tier). |
| **Traps: Abnormality breach** | `Abnormality Breach Trap` (a random abnormality's Qliphoth counter drops to 0). Also `Qliphoth Meltdown`, `Energy Drain` and `Panic` traps. |

## How it fits together

```
 Archipelago server ──websocket── Lobotomy Corporation Client ──TCP 127.0.0.1:38269── LobotomyArchipelago.dll
 (MultiServer / archipelago.gg)   (Python, part of the apworld)   (one JSON object per line)  (inside the game)
```

The game runs on Unity 2017.4 with the old .NET 3.5 Mono runtime (no modern TLS), so the mod does not connect to the
server directly. The client handles the Archipelago protocol and relays slot data, items, checks, chat and DeathLinks.

The mod is loaded by the game's built-in `BaseMods` loader (class `Harmony_Patch`). It patches the game with Harmony
(the 1.x bundled with the game, or Harmony 2 if a mod loader provides it) and accesses every game class **through
reflection**, so it has no compile-time dependency on `Assembly-CSharp.dll`. A missing member disables only the
feature that needs it and is listed in the F9 diagnostics report.

Hooks used (class.method): `CreatureGenerate.CreatureGenerateModel.SetCreature` and `CreatureSelectUI.GetCreatureList`
(extraction offers), `GameSceneController.OnClickNextDay` (wait for abnormality items),
`CreatureInfoEquipmentRoot.OnClickWeapon/OnClickArmor` (E.G.O extraction checks), `DeployUI.CheckResearchAvailable`
(mission checks instead of research), `GameManager.StartGame/EndGame/ClearAction`, `KetherLastBossBase.ToCredit`,
`DontTouchMe.ExitGame`, `AgentModel.SetCurrentSefira` (agent slots), `EscapeUI.RestartAtCheckpoint` and
`AgentModel.OnDie` (DeathLink). Observation levels, cleared days, missions and Core Suppressions are additionally
detected by polling the game state, so progress made while hooks were missed or offline is still sent.

### Logic in short

- Day *N* needs as many abnormality items as there are containment units by day *N* (1 per extraction day,
  2 on days 21-24 and 46-49, none on days ending in 5 or 0; 48 in total).
- Location checks are tied to containment order (`Abnormality #07 - Observation Level 2`), which keeps the logic
  sound no matter which abnormality you extract when.
- Depending on `logic_difficulty`, later days also expect E.G.O "combat power" (weighted by risk level), extra agent
  slots and shield/recovery bullets. Day 47-49 require the Core Suppressions of each layer, like the vanilla game.
- Missions with special conditions get matching requirements (e.g. 3 ALEPH abnormalities, 8 ALEPH E.G.O, 10 agents).

## Installing and playing

See the [setup guide](worlds/lobotomy_corporation/docs/setup_en.md). Short version:

1. Double-click `dist/lobotomy_corporation.apworld` (or put it into Archipelago's `custom_worlds` folder).
2. Copy `dist/BaseMods/LobotomyArchipelago` into `<game>/LobotomyCorp_Data/BaseMods/`.
3. Back up `%USERPROFILE%\AppData\LocalLow\Project_Moon\Lobotomy` and start from a fresh save (observation, PE boxes
   and E.G.O are shared between runs in this game).
4. Start **Lobotomy Corporation Client** from the Archipelago Launcher, connect, then start the game. The overlay in the
   top right shows the connection state (F8 hide, F9 diagnostics, F10 resync, F11 stop waiting for abnormalities).

## Building

APWorld (either way works):

```sh
python tools/build_apworld.py                                  # -> dist/lobotomy_corporation.apworld
python Launcher.py "Build APWorlds" -- "Lobotomy Corporation"  # from an Archipelago checkout containing the world
```

Mod (.NET SDK 6+; targets net35, output copied to `dist/BaseMods/LobotomyArchipelago`):

```sh
cd mod/LobotomyArchipelago
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\LobotomyCorp\"
```

Without `GameDir` the project compiles against a small UnityEngine reference stub (`mod/stubs/UnityEngine`); the
prebuilt dll in `dist/` was built that way. Building against your own game files is the most reliable option.

## Testing

- **World tests** (Archipelago's `WorldTestBase` + custom logic tests): copy or symlink `worlds/lobotomy_corporation`
  into an Archipelago checkout and run `python -m pytest worlds/lobotomy_corporation/test`. The world also passes
  Archipelago's general cross-world suite (`test/general`, `test/webhost/test_docs.py`,
  `test/webhost/test_option_presets.py`).
- **Mod harness** (`mod/tests`): `FakeGame` is a small simulation of the game classes the mod uses (built as
  `Assembly-CSharp`), and `ModHarness` loads the real `LobotomyArchipelago.dll` with Harmony 2 on .NET 8.
  - `dotnet run --project mod/tests/ModHarness -c Release -- mock` plays a scripted session against a mock client and
    checks extraction offers, observation/E.G.O/mission/day checks, item application (abnormalities, E.G.O, research,
    LOB points), traps, agent slot limits, the goal, DeathLink and persistence.
  - `... -- live 38269` connects to a running Lobotomy Corporation Client; together with `MultiServer.py` this tests
    the whole chain (server ⇄ client ⇄ mod) end to end.

## Status and known limitations

- The mod has been verified against the simulated game and a live Archipelago server, **not yet inside the real
  game**. The game member names it uses come from the decompilation-based code of existing open-source Lobotomy
  Corporation mods; the most likely things to need adjustment are the agent slot hook (`AgentModel.SetCurrentSefira`),
  the energy quota lookup (`StageTypeInfo.GetEnergyNeed`), ordeal class names other than the Dawn/Midnight ones known
  from other mods, and research name matching (`research_overrides.json` can pin research ids). Press F9 in game and
  check the "Patches" section of `diagnostics.txt` to see what attached.
- If you restart a day, filler items received during that day (LOB points, energy) are not granted again.
- Agent slot limits are enforced when agents are assigned in the deployment phase; agents already assigned when the
  mod loads are only flagged in the overlay.

## Credits and references

- [Archipelago](https://github.com/ArchipelagoMW/Archipelago) (world API, APQuest example world, CommonClient)
- [LobCorp-AP](https://github.com/Kli333/LobCorp-AP) by Kli333 — an earlier Lobotomy Corporation AP integration, used
  as a reference for which game classes and methods exist
- [lobotomy-corporation-mods](https://github.com/CTristan/lobotomy-corporation-mods),
  [LobotomyCorpTrainerMod](https://github.com/xmmmmmovo/LobotomyCorpTrainerMod),
  [LobotomyCorporation_ExtraEditMod](https://github.com/nekgreen/LobotomyCorporation_ExtraEditMod) and
  [DefensivePositions](https://github.com/oinkratpig/DefensivePositions) for the mod loader, Harmony and game APIs
- [Lobotomy Corporation Wiki](https://lobotomycorp.fandom.com) for abnormality ids, E.G.O, missions, research and
  Core Suppression rules
