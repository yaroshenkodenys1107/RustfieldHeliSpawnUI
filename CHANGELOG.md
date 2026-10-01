# Changelog

All notable changes to RustfieldHeliSpawnUI. Versions follow `major.minor.patch`.

---

## 1.0.2

**Changed**

- The strip 0.5 canvas units lower than 1.0.0 in all (1.0.1 moved it only 0.25, half a pixel on 2K,
  which the screen rounds away).

---

## 1.0.1

**Changed**

- Plates take RustfieldButtons' bar look: `0.969 0.922 0.882 0.035` over the blur material. Green and
  red now lie on them as faces.
- The whole strip 0.5 f (0.25 canvas units) lower.

---

## 1.0.0

**Added**

- Three helicopter blocks over the clothing slots (variant E): icon, name, status dot, a spawn or
  fetch button with the cooldown as a draining red bar and the time, and a remove button.
- Spawn, fetch and remove run SpawnHeli's chat commands as the player; cooldowns are read from
  SpawnHeli's data and config.
- Takes over the vehicle column of RustfieldButtons.

**Lang keys**

- `Name.mini`, `Name.attack`, `Name.scrap`
- `Button.Spawn`, `Button.Fetch`, `Button.Remove`
