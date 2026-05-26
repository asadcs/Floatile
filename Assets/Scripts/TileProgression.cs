using UnityEngine;

public static class TileProgression
{
    // ── Tuning (all public static — override from a config component if needed) ─
    public static float SizeMin             = 0.20f;
    public static float SizeMax             = 1.50f;
    public static float SizePivot           = 12f;   // P where physical size is at midpoint

    // Binary pyramid: ln(2) ≈ 0.693 means each tier-doubling halves spawn probability
    public static float AvailabilityDecay   = 0.693f;
    public static float SpawnDecay          = 0.50f;  // Gaussian relative weight (SpawnWeight)

    public static float SpeedBase           = 0.50f;
    public static float SpeedRange          = 2.00f;
    public static float SpeedDecay          = 0.30f;

    public static float WanderAmplitude     = 2.5f;   // vertical drift amplitude at tier 2
    public static float WanderDecay         = 0.15f;  // amplitude decay per P unit

    public static float OrthoBase           = 7.0f;
    public static float OrthoRange          = 11.0f;  // total zoom range: 7 → 18
    public static float OrthoSensitivity    = 0.5f;

    public static float PrestigePivot       = 20f;    // P at which PrestigeScore = 1.0
    public static float PrestigePower       = 2.0f;
    public static float FXPivot             = 30f;
    public static float InfluenceRadiusScale = 2.5f;

    public static float DominancePivot      = 15f;
    public static float DominancePower      = 1.5f;

    public static float SpawnIntervalBase        = 0.3f;   // seconds between spawns at zero pressure
    public static float SpawnIntervalScale       = 2.0f;   // max additional seconds at full pressure
    public static float SpawnIntervalSensitivity = 0.3f;

    public static float PlayerSpeedBase      = 5.0f;   // player speed floor at max tier
    public static float PlayerSpeedRange     = 15.0f;  // speed range (added to base at tier 2)
    public static float PlayerSpeedDecay     = 0.10f;  // decay per P unit (gentler than NPC)

    public static float AvoidanceMinRadius   = 2.0f;  // floor for NPC flee detection range
    public static int   ThreatRangeAboveP   = 4;     // P-units above player included in spawn pool
    public static float PredatorMinDeltaP   = 2f;    // P-unit lead before an NPC starts chasing
    public static float ChaseSpeedFactor    = 0.5f;  // fraction of baseSpeed used for chase drift
    public static float ChaseDetectFactor   = 4f;    // InfluenceRadius multiplier for chase detection
    public static float InvincibilityDuration = 1.5f; // post-penalty invincibility window (seconds)
    public static float SpecialDriftSpeed      = 0.5f;  // white tile drift speed
    public static long  SpecialTileVisualTier  = 16L;  // reference tier for special tile visual size
    public static float BlackTileChaseSpeed    = 2.5f;  // black tile movement speed (u/s)
    public static float BlackTileChaseStrength = 2.0f;  // turn rate toward player (rad/s approx)
    public static float BlackTileScaleMultiplier = 1.8f; // black tile is bigger — more threatening
    public static float BlackTileLifespan      = 10f;   // seconds before black tile disappears
    public static float WhiteTileLifespan      = 30f;   // seconds before white tile disappears

    // ── Core progression value ────────────────────────────────────────────────
    // P = log2(tier): tier 2→1, tier 4→2, tier 8→3, tier 1K→10, tier 1M→20 ...
    public static float P(long v) => v >= 2 ? Mathf.Log(v, 2f) : 0f;

    // ── Factor curves — each interprets P through a different curve type ──────

    // SIZE — tanh sigmoid, asymptotes smoothly to SizeMax. No hard cap.
    public static float PhysicalSize(long v)
    {
        float p = P(v);
        return SizeMin + (SizeMax - SizeMin) * Tanh(p / SizePivot);
    }

    // SPAWN WEIGHT — Gaussian in log-space, centered on player tier (relative weight).
    public static float SpawnWeight(long candidate, long player)
        => Mathf.Exp(-SpawnDecay * Mathf.Abs(P(candidate) - P(player)));

    // AVAILABILITY — absolute spawn weight; true binary pyramid (tier 2 always most common).
    // tier 2 (P=1): 0.50  |  tier 4 (P=2): 0.25  |  tier 8 (P=3): 0.125
    public static float AvailabilityScore(long v)
        => Mathf.Exp(-AvailabilityDecay * P(v));

    // SPEED — exponential decay: tier-2 tiles dart (2.5 u/s), high-tier drift (≈0.5 u/s).
    public static float NpcSpeed(long v)
        => SpeedBase + SpeedRange * Mathf.Exp(-SpeedDecay * P(v));

    // PLAYER SPEED — exponential decay mirroring NPC but gentler; large player feels heavy not sluggish.
    // tier 2: 18.6 u/s  |  tier 1K: 10.5 u/s  |  tier 1M: 7.0 u/s
    public static float PlayerSpeed(long v)
        => PlayerSpeedBase + PlayerSpeedRange * Mathf.Exp(-PlayerSpeedDecay * P(v));

    // WANDER — vertical drift amplitude; small tiles dart chaotically, large tiles drift calmly.
    // tier 2: 2.5  |  tier 64: 1.1  |  tier 4K: 0.44
    public static float WanderScore(long v)
        => WanderAmplitude * Mathf.Exp(-WanderDecay * P(v));

    // CAMERA SCORE — logarithmic per-tile contribution to global camera influence.
    public static float CameraScore(long v) => Mathf.Log(1f + P(v));

    // TARGET ORTHO SIZE — tanh-smoothed response to the max camera score in the arena.
    public static float TargetOrthoSize(float globalCameraScore)
        => OrthoBase + OrthoRange * Tanh(globalCameraScore * OrthoSensitivity);

    // PRESSURE — tile's area footprint (size²). Feeds OccupancyRatio.
    public static float PressureScore(long v) { float s = PhysicalSize(v); return s * s; }

    // TEMPORAL PRESSURE — per-tile spawn-rate contribution: size × weight, so large heavy tiles
    // slow the arena more than linearly. Distinct from GlobalDensity (Σ PhysicalSize).
    public static float TemporalPressureScore(long v) => PhysicalSize(v) * WeightScore(v);

    // TARGET SPAWN INTERVAL — seconds between NPC spawns driven by global temporal pressure.
    // empty arena: 0.5s  |  full arena: ~8.5s
    public static float TargetSpawnInterval(float globalTemporalPressure)
        => SpawnIntervalBase + SpawnIntervalScale * Tanh(globalTemporalPressure * SpawnIntervalSensitivity);

    // PRESTIGE — power curve; drives glow/aura rendering in late game.
    public static float PrestigeScore(long v)
        => Mathf.Pow(Mathf.Clamp01(P(v) / PrestigePivot), PrestigePower);

    // FX — root curve: rises quickly at low P, plateaus late.
    public static float FXScore(long v)
        => Mathf.Pow(Mathf.Clamp01(P(v) / FXPivot), 0.5f);

    // WEIGHT — inertia/smoothness factor; large tiles feel heavier and more authoritative.
    // tier 2: 1.07  |  tier 512: 1.63  |  tier 1M: 1.93
    public static float WeightScore(long v) => 1f + Tanh(P(v) / 15f);

    // DOMINANCE — visual dominance power curve; late-game emphasis on prestige over size.
    // ~0 at tier 2  |  0.25 at tier 64  |  1.0 at tier 2^15
    public static float DominanceScore(long v)
        => Mathf.Pow(Mathf.Clamp01(P(v) / DominancePivot), DominancePower);

    // AUDIO — soft exponential amplification, plateaus near 1.0.
    // tier 2: 0.33  |  tier 64: 0.73  |  tier 4K: 0.93
    public static float AudioScore(long v)
        => 1f - Mathf.Exp(-0.2f * P(v));

    // MERGE RESISTANCE — progressive damping; high-tier tiles resist being eaten.
    // ~0 at tier 2  |  0.46 at tier 64  |  0.93 at tier 4K
    public static float MergeResistanceScore(long v) => Tanh(P(v) / 10f);

    // FREEDOM — inverse spatial pressure; large tiles have less maneuvering room.
    // 0.93 at tier 2  |  0.63 at tier 64  |  0.32 at tier 1M
    public static float FreedomScore(long v) => 1f - Tanh(P(v) / 20f);

    // INFLUENCE RADIUS — spatial footprint; used for avoidance and density fields.
    public static float InfluenceRadius(long v) => PhysicalSize(v) * InfluenceRadiusScale;

    // SPECIAL TILE SCALE — visual size for special tiles, evaluated on the shared PhysicalSize curve.
    public static float SpecialTileScale() => PhysicalSize(SpecialTileVisualTier);

    // ── Internal helpers ──────────────────────────────────────────────────────
    static float Tanh(float x) { float e2 = Mathf.Exp(2f * x); return (e2 - 1f) / (e2 + 1f); }
}
