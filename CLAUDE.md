# Floatile — Project Rules

Unity 6 LTS + 2D URP casual browser game. Fish-eat-growing mechanic + 2048 number system.
WebGL target → CrazyGames submission.

Do not make any changes until you have 95% confidence in what needs to be built.

---

## Key Paths

| What | Where |
|------|-------|
| Unity project | `C:\Dev\Floatile\` |
| Design doc + devlog | `C:\AI\Obsidian\SecondBrain\projects\Floatile\` |
| Plan | `C:\Users\Dell\.claude\plans\first-thing-install-squishy-moler.md` |
| Legal | `C:\AI\Obsidian\SecondBrain\projects\Floatile\legal.md` |

---

## Tech Stack

- Unity 6 LTS (6000.4.7f1), 2D URP, C#, WebGL
- Input: Legacy Input Manager (both old + new active)
- Physics2D gravity must be (0, 0) — confirm before any physics work
- Tile labels: TextMesh (legacy), NOT TMP — TMP world-space sizing is unreliable without RectTransform
- UI / HUD: TextMeshPro on Canvas

---

## Patterns

- Player movement: kinematic Rigidbody2D + `rb.MovePosition()` in FixedUpdate only
- Unity null checks: always `!= null`, never `??` — Unity overrides `==` not `??`
- Background sprite: world space, never camera-parented (breaks parallax)
- TextMesh: `fontSize = 32` for quality, `characterSize` for physical size only
- Tier scaling: `TileVisual.TierScale(tier)` = `Clamp(0.5 × 1.22^log2(tier), 0.5, 4.0)`
- Same number = same color — hero identified by YOU label (Sprint 1), not color
- Editor scripts: always end with `EditorSceneManager.SaveOpenScenes()`
- Extend `TileVisual.cs`, never replace it — Sprint 0 code is working

## Anti-Patterns

- Never use `??` with any Unity Object
- Never parent background to camera
- Never create a TileEntity base class — extend TileVisual
- Never add a separate score — `player.tier` IS the game state
- Never use TMP for world-space tile numbers

---

## Sprint Status

| Sprint | Status |
|--------|--------|
| 0 — Visual Prototype | ✅ COMPLETE |
| 1 — Core Mechanic | ✅ COMPLETE |
| 2 — Visual Polish | ✅ COMPLETE |
| 3 — CrazyGames SDK | 🔲 |
| 4 — Multi-Platform | 🔲 |

## Sprint 2 — What Changed (fixed-screen arena + Cube2048 polish)

- **Arena**: fixed camera at (0,0,-10), 25×14 world units (no scrolling)
- **NPCDrift**: left-to-right only, WANDER=1.5f, interior scatter on start, edge respawn
- **ArenaSpawner**: 7 NPC target, special tiles rare (every 60–105s), proportional tier distribution
- **TileVisual**: 0.25 world-unit base scale, integer labels (4K not 4.1K)
- **TierColorTable**: 31 explicit Cube2048 hex colors indexed by log2(tier)
- **PlayerDrift**: updated arena bounds to match fixed-screen
- **CameraFollow**: fixed (no longer follows player — camera is static)

## To run on a new machine

1. `git clone https://github.com/asadcs/Floatile.git C:\Dev\Floatile`
2. Open in Unity Hub — version **6000.4.7f1** required
3. Menu → **Floatile → Setup Sprint 1 Scene (Full Reset)**
4. Press Play
