using UnityEngine;
using System.Text;

// Developer-only telemetry. Computes game-feel estimates from live arena state.
// Scores are evidence for QA review — they do not change gameplay.
public static class GameFeelTelemetry
{
    public struct Report
    {
        public float Calmness;
        public float Chaos;
        public float Readability;
        public float Flow;
        public float MotionHarmony;
        public float CameraComfort;
        public float PrestigeContrast;
        public float ArenaBeauty;
        public float ProgressionSmoothness;

        public string FormatScores()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CalmnessScore:              {Grade(Calmness)}  {Calmness:F2}");
            sb.AppendLine($"ChaosScore:                 {Grade(1f - Chaos)}  {Chaos:F2}");
            sb.AppendLine($"ReadabilityScore:           {Grade(Readability)}  {Readability:F2}");
            sb.AppendLine($"FlowScore:                  {Grade(Flow)}  {Flow:F2}");
            sb.AppendLine($"MotionHarmonyScore:         {Grade(MotionHarmony)}  {MotionHarmony:F2}");
            sb.AppendLine($"CameraComfortScore:         {Grade(CameraComfort)}  {CameraComfort:F2}");
            sb.AppendLine($"PrestigeContrastScore:      {Grade(PrestigeContrast)}  {PrestigeContrast:F2}");
            sb.AppendLine($"ArenaBeautyScore:           {Grade(ArenaBeauty)}  {ArenaBeauty:F2}");
            sb.AppendLine($"ProgressionSmoothnessScore: {Grade(ProgressionSmoothness)}  {ProgressionSmoothness:F2}");
            return sb.ToString();
        }

        static string Grade(float v)
        {
            if (v >= 0.70f) return "PASS";
            if (v >= 0.40f) return "WARN";
            return "FAIL";
        }
    }

    static readonly long[] CHECKPOINTS = { 2L, 8L, 64L, 1024L, 1_000_000L, 1_000_000_000_000L };

    public static Report Compute(long playerTier, Camera cam)
    {
        var arena = ArenaState.Instance;
        return new Report
        {
            Calmness             = ComputeCalmness(arena),
            Chaos                = ComputeChaos(arena),
            Readability          = ComputeReadability(arena, playerTier),
            Flow                 = ComputeFlow(arena),
            MotionHarmony        = ComputeMotionHarmony(playerTier),
            CameraComfort        = ComputeCameraComfort(cam, arena),
            PrestigeContrast     = ComputePrestigeContrast(playerTier),
            ArenaBeauty          = ComputeArenaBeauty(arena),
            ProgressionSmoothness = ComputeProgressionSmoothness(),
        };
    }

    // ── Score functions ───────────────────────────────────────────────────────

    static float ComputeCalmness(ArenaState arena)
    {
        if (arena == null) return 0f;
        // Peaks at OccupancyRatio ~0.35, degrades toward 0 and 1.
        float occFit  = 1f - Mathf.Clamp01(Mathf.Abs(arena.OccupancyRatio - 0.35f) * 3f);
        // Low temporal pressure = calm spawning pace.
        float pressFit = 1f - Mathf.Clamp01(arena.GlobalTemporalPressure / 15f);
        return (occFit + pressFit) * 0.5f;
    }

    static float ComputeChaos(ArenaState arena)
    {
        if (arena == null) return 0f;
        // Crowding + pressure excess → chaos.
        return Mathf.Clamp01(arena.OccupancyRatio * 1.6f + arena.GlobalTemporalPressure / 40f);
    }

    static float ComputeReadability(ArenaState arena, long playerTier)
    {
        if (arena == null) return 0f;
        // Breathing room (low occupancy) + player distinction (prestige).
        float spacing  = 1f - Mathf.Clamp01(arena.OccupancyRatio * 2.5f);
        float contrast = 0.7f + TileProgression.PrestigeScore(playerTier) * 0.3f;
        return Mathf.Clamp01(spacing * contrast);
    }

    static float ComputeFlow(ArenaState arena)
    {
        if (arena == null) return 0f;
        float occ = arena.OccupancyRatio;
        // Arena too empty (< 10%) or flooded (> 75%) = bad flow.
        if (occ < 0.10f) return occ * 5f;         // ramps 0→0.5 in the sparse zone
        if (occ > 0.75f) return (1f - occ) * 4f;  // ramps 0.5→0 in the flood zone
        return 1f - Mathf.Abs(occ - 0.40f) * 2.5f;
    }

    static float ComputeMotionHarmony(long playerTier)
    {
        // Tier 2 NPC faster than player-tier NPC = speed hierarchy correct.
        float npc2    = TileProgression.NpcSpeed(2L);
        float npcPl   = TileProgression.NpcSpeed(playerTier);
        float plSpeed = TileProgression.PlayerSpeed(playerTier);

        // Player should out-pace same-tier NPCs for control feel (>2 u/s advantage).
        float ctrlScore      = Mathf.Clamp01((plSpeed - npcPl) / 5f);
        // Small tiles clearly faster than player-tier NPCs.
        float hierarchyScore = Mathf.Clamp01((npc2 - npcPl) / 1.5f);
        // Wander decays: small tiles erratic, large tiles calm.
        float wand2   = TileProgression.WanderScore(2L);
        float wandPl  = TileProgression.WanderScore(playerTier);
        float wandScore = wand2 > wandPl ? 1f : 0f;

        return (ctrlScore + hierarchyScore + wandScore) / 3f;
    }

    static float ComputeCameraComfort(Camera cam, ArenaState arena)
    {
        if (cam == null || arena == null) return 0f;
        float current = cam.orthographicSize;
        float target  = TileProgression.TargetOrthoSize(arena.GlobalCameraScore);
        float diff    = Mathf.Abs(current - target);
        return 1f - Mathf.Clamp01(diff / 6f); // within 6 ortho units = comfortable
    }

    static float ComputePrestigeContrast(long playerTier)
    {
        float prestige  = TileProgression.PrestigeScore(playerTier);
        float dominance = TileProgression.DominanceScore(playerTier);
        float fxScore   = TileProgression.FXScore(playerTier);
        // Weighted: prestige + dominance + FX all contribute.
        return Mathf.Clamp01(prestige * 0.5f + dominance * 0.3f + fxScore * 0.2f);
    }

    static float ComputeArenaBeauty(ArenaState arena)
    {
        if (arena == null) return 0f;
        // Sweet spot: 25–55% occupancy. Degrades symmetrically outside that range.
        return Mathf.Clamp01(1f - Mathf.Abs(arena.OccupancyRatio - 0.40f) / 0.30f);
    }

    static float ComputeProgressionSmoothness()
    {
        int passes = 0, total = 0;
        for (int i = 1; i < CHECKPOINTS.Length; i++)
        {
            long prev = CHECKPOINTS[i - 1], cur = CHECKPOINTS[i];
            // Size increases toward SizeMax.
            total++; if (TileProgression.PhysicalSize(cur)         >= TileProgression.PhysicalSize(prev))         passes++;
            // NPC speed decreases.
            total++; if (TileProgression.NpcSpeed(cur)             <= TileProgression.NpcSpeed(prev))             passes++;
            // Availability decreases (binary pyramid).
            total++; if (TileProgression.AvailabilityScore(cur)    <= TileProgression.AvailabilityScore(prev))    passes++;
            // Prestige increases.
            total++; if (TileProgression.PrestigeScore(cur)        >= TileProgression.PrestigeScore(prev))        passes++;
            // Player speed decreases (gently).
            total++; if (TileProgression.PlayerSpeed(cur)          <= TileProgression.PlayerSpeed(prev))          passes++;
            // Wander decreases.
            total++; if (TileProgression.WanderScore(cur)          <= TileProgression.WanderScore(prev))          passes++;
            // FX increases.
            total++; if (TileProgression.FXScore(cur)              >= TileProgression.FXScore(prev))              passes++;
            // Dominance increases.
            total++; if (TileProgression.DominanceScore(cur)       >= TileProgression.DominanceScore(prev))       passes++;
            // Merge resistance increases.
            total++; if (TileProgression.MergeResistanceScore(cur) >= TileProgression.MergeResistanceScore(prev)) passes++;
            // Freedom decreases.
            total++; if (TileProgression.FreedomScore(cur)         <= TileProgression.FreedomScore(prev))         passes++;
        }
        return total > 0 ? (float)passes / total : 0f;
    }

    // ── Factor dump ───────────────────────────────────────────────────────────

    public static string DumpFactors(long tier)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"── Factor Dump — tier {TileVisual.FormatNumber(tier)} (P={TileProgression.P(tier):F2}) ──");
        sb.AppendLine($"PhysicalSize:        {TileProgression.PhysicalSize(tier):F4}");
        sb.AppendLine($"NpcSpeed:            {TileProgression.NpcSpeed(tier):F4}");
        sb.AppendLine($"PlayerSpeed:         {TileProgression.PlayerSpeed(tier):F4}");
        sb.AppendLine($"WanderScore:         {TileProgression.WanderScore(tier):F4}");
        sb.AppendLine($"AvailabilityScore:   {TileProgression.AvailabilityScore(tier):F6}");
        sb.AppendLine($"CameraScore:         {TileProgression.CameraScore(tier):F4}");
        sb.AppendLine($"PressureScore:       {TileProgression.PressureScore(tier):F4}");
        sb.AppendLine($"TemporalPressure:    {TileProgression.TemporalPressureScore(tier):F4}");
        sb.AppendLine($"PrestigeScore:       {TileProgression.PrestigeScore(tier):F4}");
        sb.AppendLine($"FXScore:             {TileProgression.FXScore(tier):F4}");
        sb.AppendLine($"WeightScore:         {TileProgression.WeightScore(tier):F4}");
        sb.AppendLine($"DominanceScore:      {TileProgression.DominanceScore(tier):F4}");
        sb.AppendLine($"AudioScore:          {TileProgression.AudioScore(tier):F4}");
        sb.AppendLine($"MergeResistance:     {TileProgression.MergeResistanceScore(tier):F4}");
        sb.AppendLine($"FreedomScore:        {TileProgression.FreedomScore(tier):F4}");
        sb.AppendLine($"InfluenceRadius:     {TileProgression.InfluenceRadius(tier):F4}");
        sb.AppendLine($"Label:               {TileVisual.FormatNumber(tier)}");
        var arena = ArenaState.Instance;
        if (arena != null)
        {
            sb.AppendLine($"── Arena Globals ──");
            sb.AppendLine($"OccupancyRatio:      {arena.OccupancyRatio:F4}");
            sb.AppendLine($"GlobalPressure:      {arena.GlobalPressure:F4}");
            sb.AppendLine($"GlobalTemporalPress: {arena.GlobalTemporalPressure:F4}");
            sb.AppendLine($"GlobalCameraScore:   {arena.GlobalCameraScore:F4}");
            sb.AppendLine($"GlobalPrestige:      {arena.GlobalPrestige:F4}");
            sb.AppendLine($"GlobalDominance:     {arena.GlobalDominance:F4}");
            sb.AppendLine($"GlobalDensity:       {arena.GlobalDensity:F4}");
            sb.AppendLine($"GlobalAudio:         {arena.GlobalAudioIntensity:F4}");
            sb.AppendLine($"TargetOrtho:         {TileProgression.TargetOrthoSize(arena.GlobalCameraScore):F4}");
            sb.AppendLine($"TargetSpawnInterval: {TileProgression.TargetSpawnInterval(arena.GlobalTemporalPressure):F4}s");
        }
        return sb.ToString();
    }
}
