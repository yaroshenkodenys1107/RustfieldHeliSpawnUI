# Changelog

All notable changes to RustfieldHeliSpawnUI. Versions follow `major.minor.patch`.

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
