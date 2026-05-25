using UnityEngine;
using System.Collections.Generic;

public sealed class ArenaState : MonoBehaviour
{
    public static ArenaState Instance { get; private set; }

    // ── Global fields (read by camera, spawner, audio, etc.) ─────────────────
    public float GlobalCameraScore { get; private set; }
    public float GlobalPressure    { get; private set; }
    public float GlobalPrestige    { get; private set; }
    public float OccupancyRatio    { get; private set; }  // 0..1

    // ── Arena bounds (updated by CameraFollow each frame) ────────────────────
    public static float HalfW { get; private set; } = 12.5f;
    public static float HalfH { get; private set; } = 7.0f;
    public static float MinX => -HalfW;
    public static float MaxX =>  HalfW;
    public static float MinY => -HalfH;
    public static float MaxY =>  HalfH;

    readonly Dictionary<int, long> tiles = new();
    int nextId;

    void Awake() { Instance = this; }

    // Tiles call Register on Start, Unregister on OnDestroy, UpdateTier on tier change.
    public int  Register(long tier)           { int id = nextId++; tiles[id] = tier; return id; }
    public void Unregister(int id)            => tiles.Remove(id);
    public void UpdateTier(int id, long tier) { if (tiles.ContainsKey(id)) tiles[id] = tier; }

    public static void SetBounds(float halfW, float halfH) { HalfW = halfW; HalfH = halfH; }

    void LateUpdate()
    {
        float area = HalfW * HalfH * 4f;
        float pressure = 0f, camScore = 0f, prestige = 0f;

        foreach (long t in tiles.Values)
        {
            pressure += TileProgression.PressureScore(t);
            camScore  = Mathf.Max(camScore, TileProgression.CameraScore(t));
            prestige += TileProgression.PrestigeScore(t);
        }

        GlobalPressure    = pressure;
        GlobalCameraScore = camScore;
        GlobalPrestige    = prestige;
        OccupancyRatio    = Mathf.Clamp01(pressure / Mathf.Max(1f, area));
    }
}
