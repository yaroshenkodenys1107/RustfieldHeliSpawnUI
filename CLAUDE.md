# RustfieldHeliSpawnUI – working protocol

Rust server on Carbon, hosted with Pterodactyl. Plugins live in repos under `yaroshenkodenys1107`.
Talk to the owner in Russian, briefly. Decide yourself, ask only when a choice is truly theirs.

## UI rules (all Rustfield plugins)

- Everything sits on the cell grid of the native inventory, pixel to pixel.
- Dashes, not dots, as separators in UI texts. Never "…" – the text must fit.
- One language on screen. Every text comes from the lang files (en, ru, uk) or the config.
- Units: CUI canvas 1280×720, 1 unit = 2 px on the owner's 1440p screen. The owner gives tweaks in f
  (1 f = 1 canvas unit = 2 px on 2K, as written in offsets), step 0.5.
- Icons are flat white, tinted `1 1 1 0.22` in CUI. Exception here, by the owner: the helicopter
  icons are the game's colour pictures, untinted, web PNGs from `images/` (128 square, transparent).
- Square corners, no outlines. Font `robotocondensed-bold`, 9–10.5 px (CUI takes whole sizes: 10).
- Draw the way RustfieldSorter's `Sketch` does: named plates, a clear button over them washed on
  hover, labels with 40 spare units away from their alignment.

## Helicopter UI (variant E)

Three blocks over the clothing slots, right of the backpack slot: Minicopter, Attack Helicopter,
Scrap Transport. On screen from the top left (1280×720):

- block i: x = 107.5 + 108·i, width 104 (two clothing columns);
- header y 499.5–523.5: icon 22×22 at 4, name from 30, status dot 5×5 at 4 from the right;
- buttons y 525.5–549.5: left 50, gap 4, right 50 – exactly under the clothing columns.

In code, anchor "0.5 0": `BlocksLeft -532.5`, `BlockPitch 108`, `BlockWidth 104`,
`ButtonsBottom 170.5`, `ButtonsTop 194.5`, `HeaderBottom 196.5`, `HeaderTop 220.5`, `ButtonWidth 50`,
`RightLeft 54`. Parent layer `Inventory` – shown only with the inventory open.

Colours:

| What | Hex | CUI |
|---|---|---|
| Plate (RustfieldButtons bar) | rgba(247,235,225,0.035) + blur | `0.969 0.922 0.882 0.035`, `assets/content/ui/uibackgroundblur.mat` |
| Spawn / fetch, dot "ready" | #708943 | `0.439 0.537 0.263 1` |
| Remove, cooldown bar, dot "spawn cooling down" | #a4433a | `0.643 0.263 0.227 1` |
| Dot "in the world" | #aaee32 | `0.667 0.933 0.196 1` |
| Dot "fetch cooling down" | #cd875b | `0.804 0.529 0.357 1` |
| Spare blue | #1f5e8c | `0.122 0.369 0.549 1` |
| Inactive text | 42% white | `1 1 1 0.42` |

Behaviour: not in the world – left spawns, or shows the spawn cooldown as a red bar draining right
to left with the time; in the world – left fetches (fetch cooldown drawn the same way), right
removes. The buttons run SpawnHeli's chat commands as the player.

## SpawnHeli

Not ours, never edited. In the world: `API_GetMinicopter`, `API_GetAttackHelicopter`,
`API_GetScrapTransportHelicopter`. Cooldowns: no API – read through reflection from `_data`
(`SpawnCooldowns` / `FetchCooldowns` start times) and `_config`, length from its private
`GetPlayerCooldownSeconds`. Live config: one helicopter type at a time, others auto-despawn.

## UI work flow

1. The owner gives the task and a 2K screenshot of the native UI.
2. Mockups first, on a Design canvas, on the full 1280×720 screenshot. No code before approval.
3. After approval: code, `mcs --parse RustfieldHeliSpawnUI.cs` (no Rust DLLs here, so syntax only).
4. Commit, push, give one deploy command. Then tweak by the owner's screenshots and f numbers.

Save limit: read big files in pieces, edit in place, batch fixes.

## Git

Author `Denys Yaroshenko <yaroshenkodenys1107@gmail.com>`. Push to `main`.
Bump the version in `[Info]` and add a `CHANGELOG.md` entry; list every new or renamed lang key there.
Tags do not push from the Claude sessions (proxy 403) – backups are branches.

## Deploy (on the server)

```bash
cd /tmp && rm -rf rhs-push && git clone -q https://github.com/yaroshenkodenys1107/RustfieldHeliSpawnUI rhs-push && cd rhs-push
V=/var/lib/pterodactyl/volumes/ba3009b3-fb49-4ad0-b385-a2573f10d074/carbon
cp RustfieldHeliSpawnUI.cs $V/plugins/
rm -f $V/lang/*/RustfieldHeliSpawnUI.json   # only when lang texts changed – Carbon keeps old files
chown pterodactyl:pterodactyl $V/plugins/RustfieldHeliSpawnUI.cs
```

Then `c.reload RustfieldHeliSpawnUI` in the console.
