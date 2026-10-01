# Lobotomy Corporation

## Where is the options page?

The [player options page for this game](../player-options) contains all the options you need to configure and export
a config file.

## What does randomization do to this game?

You manage the facility as usual, but almost everything that makes your facility stronger now comes from the
multiworld:

- **Abnormalities.** On every extraction day you can only extract abnormalities whose item you have received. Before a
  day with an extraction starts, the mod waits until enough abnormality items are available (the game would otherwise
  hand you a random vanilla abnormality).
- **E.G.O equipment.** Spending PE boxes to extract an E.G.O weapon or suit sends a location check instead of creating
  the equipment. E.G.O weapons and suits are shuffled into the item pool and appear in your inventory when received.
  E.G.O received from Archipelago is restored at the start of the day if it was destroyed.
- **Research.** Completing a Sephirah mission sends a check instead of letting you pick a research. All 27 department
  researches are items, including the **shield bullets** (Physical Intervention, Trauma and Erosion Shield),
  **Execution Bullets**, the **Qliphoth Intervention Field** slow bullet, **HP-N/SP-E bullets** and the
  **Rabbit Team** protocol.
- **Agent slots.** Every department starts with only a few agent slots (2 by default). Additional slots per department
  are items, up to the vanilla maximum of 5.
- **Resources.** LOB points, PE box shipments, Enkephalin canisters (energy), training manuals and medical supplies
  are filler items.

## What are the locations?

- **Observation levels:** `Abnormality #07 - Observation Level 2` is sent when the 7th abnormality you contained reaches
  observation level 2, whichever abnormality that is. Checks are tied to containment order because the facility has a
  fixed number of containment units that fill up one (or two) per day.
- **E.G.O extraction:** `Abnormality #07 - E.G.O Weapon Extraction` / `... Suit Extraction` (1 to 3 per abnormality).
  Abnormalities without a weapon or suit (tools, Don't Touch Me, ...) complete those checks automatically at
  observation level 4.
- **E.G.O gifts** (optional): obtaining an abnormality's gift.
- **Days:** `Day 07 Cleared` for every day up to your goal.
- **Sephirah missions:** all 36 missions, e.g. `Malkuth Mission 1 - You Can Do This!`.
- **Core Suppressions** of all nine Sephirot (when your goal day leaves room for them).

## What is the goal?

Clear the goal day (5 to 50, default 25). Optionally also complete a number of Core Suppressions. Day 50 is the true
ending of the game.

## What does the logic expect?

The hard requirement is the number of abnormality items: a day can only be played once you have received enough
abnormalities to fill every containment unit opened up to that day. Depending on the logic difficulty, later days also
expect some E.G.O equipment ("combat power", weighted by risk level), extra agent slots and bullet research. Some
Sephirah missions have their own requirements (e.g. Binah's "The Ego's Shells" expects 8 ALEPH E.G.O items). Days 47-49
require the Core Suppressions of the corresponding layer, as in the vanilla game.

## Traps

- **Ordeal traps** (Dawn, Noon, Dusk, Midnight): summon an Ordeal. Tiers the current day would not normally allow are
  downgraded (Noon from day 11, Dusk from day 21, Midnight from day 26).
- **Abnormality Breach trap:** a random abnormality's Qliphoth counter drops to 0.
- **Qliphoth Meltdown trap**, **Energy Drain trap** (20% of today's energy) and **Panic trap** (one department panics).

Traps wait until the management phase is running and fire at most once every 20 seconds.

## DeathLink

When enabled, a DeathLink is sent when you restart a failed day (or, optionally, when every agent of a department dies).
A received DeathLink kills a random agent, breaches an abnormality or summons an Ordeal, depending on your options.

## Which items can be in another player's world?

Any of them. Abnormality items are named after the abnormality, E.G.O items after the E.G.O, for example
`Penitence (E.G.O Weapon)`.

## What does another world's item look like in Lobotomy Corporation?

Items are not visible in the game world. When you send an item, the in-game system log and the Archipelago overlay
show who received what.

## Is there anything else I should know?

- Observation levels, PE boxes and E.G.O are global in Lobotomy Corporation (they are shared between runs). Use a fresh
  save for every seed, otherwise progress from earlier runs sends checks immediately. See the setup guide.
- The mod shows a small status overlay in the top right corner (F8 hides it) and writes a diagnostics report with F9.
