# MachineBow

A Mount & Blade II: Bannerlord (v1.5.5) mod that adds a "machine crossbow" — a full-auto crossbow with rapid fire, high-velocity penetrating bolts, aim zoom, and an explosive bolt effect.

## Features

- **Full-auto rapid fire**: Hold left mouse to continuously fire bolts; single click fires one bolt immediately.
- **High-velocity bolts**: 300 m/s — practically no projectile drop.
- **Right-click aim zoom**: Hold right mouse to zoom (FOV 0.4x).
- **Penetrating bolts**: Pierce through all units, mounts, and shields (`MultiplePenetration` + `CanPenetrateShield` + `burning_ballista` physics).
- **Burning trail + explosion on hit**: Bolts leave a flame trail and trigger an area-damage explosion (configurable radius/damage) on impact.
- **Large ammo capacity**: 500 bolts per stack, light weight (0.01).
- **Cheat mode enabled**: For easy item acquisition during testing.
- **No Harmony dependency**: Pure `MissionBehavior` via reflection.

## Install

1. Copy the `MachineBow` folder into `Mount & Blade II Bannerlord/Modules/`.
2. Launch the game, enable the module in the launcher.
3. (Cheat mode) Use the inventory cheat menu to obtain the "机关连弩" and "机关弩箭".

## Usage

- **Left mouse (hold)**: Full-auto fire.
- **Left mouse (click)**: Single shot.
- **Right mouse (hold)**: Aim zoom.
- Bolts pierce through enemies/mounts/shields and explode on impact.

## Configuration

Edit `src/AutoBow/AutoBowConfig.cs`:

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableAutoFire` | true | Full-auto continuous fire |
| `FireInterval` | 0.15s | Time between shots (~6-7 shots/sec) |
| `MissileSpeed` | 300 | Bolt velocity (m/s) |
| `EnableAimZoom` | true | Right-click zoom |
| `AimFovMultiplier` | 0.4 | Zoomed FOV multiplier |
| `EnableExplosion` | true | Explosion on bolt impact |
| `ExplosionRadius` | 3.0 | Explosion radius (m) |
| `ExplosionDamage` | 100 | Explosion center damage (decays with distance) |
| `DebugLog` | false | Key state logging |
| `VerboseLog` | false | Per-frame detail logging |

Rebuild after changing: `dotnet build -c Release` in `src/AutoBow/`.

## Build

```
cd src/AutoBow
"C:\Program Files\dotnet\dotnet.exe" build -c Release
```

The DLL is auto-copied to `bin/Win64_Shipping_Client/`. Kill the Bannerlord launcher before building (it locks the DLL).

## Files

| File | Description |
|------|-------------|
| `ModuleData/auto_bow_items.xml` | Weapon (light crossbow) + penetrating burning bolt definitions |
| `src/AutoBow/AutoBowMissionBehavior.cs` | Core per-frame logic: autofire, aim zoom, explosion on hit |
| `src/AutoBow/AutoBowSubModule.cs` | Entry point, initializes logger + MissionBehavior |
| `src/AutoBow/AutoBowConfig.cs` | Tunable constants |
| `src/AutoBow/AutoBowLogger.cs` | File logger |

## License

MIT
