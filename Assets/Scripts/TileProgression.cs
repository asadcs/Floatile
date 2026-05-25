using UnityEngine;

public static class TileProgression
{
    // ── Tuning (all public static — override from a config component if needed) ─
    public static float SizeMin             = 0.20f;
    public static float SizeMax             = 1.50f;
    public static float SizePivot           = 12f;   // P where physical size is at midpoint

    public static float SpawnDecay          = 0.50f; // Gaussian spread in P-space around player

    public static float SpeedBase           = 0.50f;
    public static float SpeedRange          = 2.00f;
    public static float SpeedDecay          = 0.30f;

    public static float OrthoBase           = 7.0f;
    public static float OrthoRange          = 11.0f; // total zoom range: 7 → 18
    public static float OrthoSensitivity    = 0.5f;

    public static float PrestigePivot       = 20f;   // P at which PrestigeScore = 1.0
    public static float PrestigePower       = 2.0f;
    public static float FXPivot             = 30f;
    public static float InfluenceRadiusScale = 2.5f;

    // ── Core progression value ────────────────────────────────────────────────
    // P = log2(tier): tier 2→1, tier 4→2, tier 8→3, tier 1K→10, tier 1M→20 ...
    public static float P(long v) => v >= 2 ? Mathf.Log(v, 2f) : 0f;

    // ── Factor curves ─────────────────────────────────────────────────────────

    // SIZE — sigmoid (tanh), asymptotes smoothly to SizeMax. No hard cap.
    public static float PhysicalSize(long v)
    {
        float p = P(v);
        return SizeMin + (SizeMax - SizeMin) * Tanh(p / SizePivot);
    }

    // SPAWN WEIGHT — Gaussian in log-space, centered on player tier.
    // Tiles near player tier are most likely; drops off symmetrically.
    public static float SpawnWeight(long candidate, long player)
        => Mathf.Exp(-SpawnDecay * Mathf.Abs(P(candidate) - P(player)));

    // SPEED — exponential decay: tier-2 tiles dart (2.5 u/s), high-tier drift (≈0.5 u/s).
    public static float NpcSpeed(long v)
        => SpeedBase + SpeedRange * Mathf.Exp(-SpeedDecay * P(v));

    // CAMERA SCORE — logarithmic per-tile contribution to global camera influence.
    public static float CameraScore(long v) => Mathf.Log(1f + P(v));

    // TARGET ORTHO SIZE — tanh-smoothed response to the max camera score in the arena.
    public static float TargetOrthoSize(float globalCameraScore)
        => OrthoBase + OrthoRange * Tanh(globalCameraScore * OrthoSensitivity);

    // PRESSURE — tile's area footprint contribution (size²). Feeds OccupancyRatio.
    public static float PressureScore(long v) { float s = PhysicalSize(v); return s * s; }

    // PRESTIGE — power curve. Wired now; drives glow/aura rendering when added.
    public static float PrestigeScore(long v)
        => Mathf.Pow(Mathf.Clamp01(P(v) / PrestigePivot), PrestigePower);

    // FX — root curve: rises quickly at low P, plateaus late.
    public static float FXScore(long v)
        => Mathf.Pow(Mathf.Clamp01(P(v) / FXPivot), 0.5f);

    // INFLUENCE RADIUS — spatial footprint for future density field.
    public static float InfluenceRadius(long v) => PhysicalSize(v) * InfluenceRadiusScale;

    // ── Internal helpers ──────────────────────────────────────────────────────
    static float Tanh(float x) { float e2 = Mathf.Exp(2f * x); return (e2 - 1f) / (e2 + 1f); }
}
