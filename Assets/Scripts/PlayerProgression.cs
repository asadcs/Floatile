using UnityEngine;
using System;

public sealed class PlayerProgression : MonoBehaviour
{
    public long Tier { get; private set; } = 2;
    public long CurrentTier => Tier;

    long accumulatedPoints;
    long Threshold => Tier;

    public event Action<long> OnTierChanged;

    int _arenaId = -1;

    void Start()
    {
        if (ArenaState.Instance != null) _arenaId = ArenaState.Instance.Register(Tier);
    }

    void OnDestroy()
    {
        ArenaState.Instance?.Unregister(_arenaId);
    }

    public void AddPoints(long points)
    {
        accumulatedPoints += points;
        while (accumulatedPoints >= Threshold)
        {
            accumulatedPoints -= Threshold;
            DoubleTier();
        }
    }

    public void EvolveInstant()
    {
        accumulatedPoints = 0;
        DoubleTier();
    }

    public void ApplyPenalty()
    {
        accumulatedPoints = 0;
        Tier = Math.Max(2L, Tier / 2);
        ArenaState.Instance?.UpdateTier(_arenaId, Tier);
        OnTierChanged?.Invoke(Tier);
    }

    public void ApplySpecialEffect(SpecialTile.OperatorType op)
    {
        long next = ComputeSpecial(op);
        next = Math.Max(2L, next);
        if (next < 2) next = 2;
        Tier = next;
        accumulatedPoints = 0;
        ArenaState.Instance?.UpdateTier(_arenaId, Tier);
        OnTierChanged?.Invoke(Tier);
    }

    long ComputeSpecial(SpecialTile.OperatorType op)
    {
        switch (op)
        {
            case SpecialTile.OperatorType.Multiply2:
                return Tier * 2;
            case SpecialTile.OperatorType.Multiply3:
                return NextPow2(Tier * 3);
            case SpecialTile.OperatorType.Square:
            {
                long result = SquareSafe(Tier);
                return result;
            }
            case SpecialTile.OperatorType.Cube:
            {
                long result = CubeSafe(Tier);
                return result;
            }
            case SpecialTile.OperatorType.CubeRoot:
                return (long)Math.Round(Math.Pow(Tier, 1.0 / 3.0));
            case SpecialTile.OperatorType.SquareRoot:
                return (long)Math.Round(Math.Sqrt(Tier));
            case SpecialTile.OperatorType.DivideBy2:
                return Tier / 2;
            default:
                return Tier;
        }
    }

    static long SquareSafe(long v)
    {
        // Detect overflow: if v > sqrt(long.MaxValue) cap it
        const long limit = 3_037_000_499L;
        if (v > limit) return long.MaxValue / 2;
        return v * v;
    }

    static long CubeSafe(long v)
    {
        const long limit = 2_097_151L;
        if (v > limit) return long.MaxValue / 2;
        return v * v * v;
    }

    // Returns the smallest power of 2 >= value
    static long NextPow2(long value)
    {
        if (value <= 1) return 2;
        long p = 2;
        while (p < value) p <<= 1;
        return p;
    }

    void DoubleTier()
    {
        Tier = Tier >= long.MaxValue / 2 ? long.MaxValue : Tier * 2;
        ArenaState.Instance?.UpdateTier(_arenaId, Tier);
        OnTierChanged?.Invoke(Tier);
    }

    // Progress toward next tier-up [0..1], for UI progress bar fill
    public float Progress => Threshold > 0 ? (float)accumulatedPoints / Threshold : 0f;

    // Dev QA only — bypasses normal progression, sets tier directly.
    public void ForceSetTier(long tier)
    {
        Tier = Math.Max(2L, tier);
        accumulatedPoints = 0;
        ArenaState.Instance?.UpdateTier(_arenaId, Tier);
        OnTierChanged?.Invoke(Tier);
    }
}
