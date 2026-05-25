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
| 0 — Visual Prototype | ✅ COMPLETE (committed to GitHub) |
| 1 — Core Mechanic | 🔲 3 prereqs needed first (see plan) |
| 2 — Game Feel | 🔲 bg_music.mp3 already done |
| 3 — CrazyGames SDK | 🔲 |
| 4 — Multi-Platform | 🔲 |

Sprint 1 prereqs (manual in Unity):
1. Import Nunito Bold → TMP Font Asset Creator
2. Flip player BoxCollider2D `isTrigger = true`
3. Confirm Physics2D gravity = (0, 0)
