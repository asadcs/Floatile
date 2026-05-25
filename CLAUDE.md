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
| 1 — Core Mechanic | ⚠️ CODE COMPLETE — pending play test |
| 2 — Game Feel | 🔲 bg_music.mp3 already done |
| 3 — CrazyGames SDK | 🔲 |
| 4 — Multi-Platform | 🔲 |

## Sprint 1 — Code is on GitHub (do NOT rewrite)

All Sprint 1 scripts exist in `Assets/Scripts/`. Pull and run setup:

1. `git pull` in `C:\Dev\Floatile\`
2. Open Unity — wait for recompile
3. Menu → **Floatile → Setup Sprint 1 Scene (Full Reset)**
4. Press Play

Scripts already written: `TierColorTable`, `GameManager`, `PlayerProgression`,
`EatSystem`, `NPCDrift`, `SpecialTile`, `ArenaSpawner`, `UIManager`
Modified: `TileVisual` (SetTier), `PlayerDrift` (isTrigger=true, ExternalInput)
Prefabs: `NPC_Drift.prefab`, `SpecialTile.prefab`
