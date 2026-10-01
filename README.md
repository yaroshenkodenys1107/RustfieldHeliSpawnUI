# RustfieldHeliSpawnUI

**Helicopter buttons in the inventory: spawn, fetch and remove the Minicopter, the Attack Helicopter and
the Scrap Transport, with their cooldowns drawn as draining bars.**

Three blocks over the clothing slots, right of the backpack slot. Each has the machine's icon, its
name and a status dot over two buttons:

- **Not in the world** – the left button spawns it. While the spawn cooldown runs, the button is a
  red bar draining right to left with the time left on it.
- **In the world** – the left button brings it to you (fetch cooldown drawn the same way), the right
  one removes it.
- **The dot** – lime: in the world; orange: cooling down; green: ready.

Author: **Denys Yaroshenko** · Rust (Carbon / Oxide) · **v1.0.0**

## Requirements

- **[SpawnHeli](https://umod.org/plugins/spawn-heli)** – does the spawning; the buttons run its chat
  commands, so its permissions, checks and messages apply. It is not modified.
- **RustfieldPanel**, optional: the blocks speak the language picked in the menu.

## Installation

1. Drop `RustfieldHeliSpawnUI.cs` into `carbon/plugins/`.
2. On load it writes `carbon/configs/RustfieldHeliSpawnUI.json` and the en, ru and uk lang files.
3. Remove the vehicle column from RustfieldButtons (1.1.0 does it by itself).

## Config

Per machine: `Show`, the SpawnHeli `Spawn` / `Fetch` / `Remove` commands, and the icon URL (a square PNG). `Seconds between updates` – how often the cooldowns tick (1 by default).

The icons are in `images/`; the URLs point at this repository, so the images must be reachable for
the server's clients – host them elsewhere if the repository is private.

## Commands

`rfheli spawn|fetch|remove mini|attack|scrap` – what the buttons run. Only works when the block on
screen offers that action.

## Licence

Proprietary. See [LICENSE](LICENSE).
