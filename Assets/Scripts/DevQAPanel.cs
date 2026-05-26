#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Dev-only in-game QA panel. Toggle with F1. Never shipped in release builds.
// Provides tier-setting, tile spawning, arena clearing, special tile forcing, and telemetry dumps.
public sealed class DevQAPanel : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        new GameObject("[DevQA]").AddComponent<DevQAPanel>();
    }

    // Tier presets grouped by range for readable layout
    static readonly (string label, long tier)[][] TIER_ROWS =
    {
        new[] { ("2",    2L),    ("4",    4L),    ("8",    8L)    },
        new[] { ("1M",   1_000_000L),      ("500M", 500_000_000L),      ("999M", 999_000_000L)      },
        new[] { ("1B",   1_000_000_000L),  ("500B", 500_000_000_000L),  ("999B", 999_000_000_000L)  },
        new[] { ("1T",   1_000_000_000_000L), ("500T", 500_000_000_000_000L), ("999T", 999_000_000_000_000L) },
    };

    bool _visible;
    PlayerProgression _player;
    ArenaSpawner      _spawner;
    Camera            _cam;

    void Awake()   => DontDestroyOnLoad(gameObject);
    void Start()
    {
        _player  = FindFirstObjectByType<PlayerProgression>();
        _spawner = FindFirstObjectByType<ArenaSpawner>();
        _cam     = Camera.main;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) _visible = !_visible;
        if (Input.GetKeyDown(KeyCode.F2)) LogFactors();
        if (Input.GetKeyDown(KeyCode.F3)) LogScores();
    }

    void OnGUI()
    {
        if (!_visible) return;

        GUILayout.BeginArea(new Rect(10, 10, 240, Screen.height - 20), GUI.skin.box);

        GUILayout.Label("DEV QA PANEL  [F1 toggle]");
        DrawStatus();

        GUILayout.Space(4);
        GUILayout.Label("── SET HERO TIER ──");
        foreach (var row in TIER_ROWS)
        {
            GUILayout.BeginHorizontal();
            foreach (var (label, tier) in row)
                if (GUILayout.Button(label)) SetHeroTier(tier);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUILayout.Label("── SPAWN NPC ──");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Smaller")) SpawnRelative(-1);
        if (GUILayout.Button("Same"))    SpawnRelative(0);
        if (GUILayout.Button("Bigger"))  SpawnRelative(1);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Clear Arena")) _spawner?.ClearAllNPCs();

        GUILayout.Space(4);
        GUILayout.Label("── SPECIAL TILES ──");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Gold"))  _spawner?.ForceSpawnWhite();
        if (GUILayout.Button("Force Hell"))  _spawner?.ForceSpawnBlack();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("── REPORTS  [F2/F3] ──");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Factors")) LogFactors();
        if (GUILayout.Button("Scores"))  LogScores();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Run QA Scenario")) RunQAScenario();

        GUILayout.EndArea();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void DrawStatus()
    {
        if (_player == null) { GUILayout.Label("Player: NOT FOUND"); return; }
        long tier = _player.CurrentTier;
        var arena = ArenaState.Instance;
        GUILayout.Label($"Hero: {TileVisual.FormatNumber(tier)}  P={TileProgression.P(tier):F1}");
        if (arena != null)
        {
            GUILayout.Label($"Occ: {arena.OccupancyRatio * 100f:F0}%  Temp: {arena.GlobalTemporalPressure:F1}");
            GUILayout.Label($"Density: {arena.GlobalDensity:F2}  Prestige: {arena.GlobalPrestige:F2}");
        }
    }

    void SetHeroTier(long tier)
    {
        if (_player == null) { Debug.LogWarning("[DevQA] PlayerProgression not found"); return; }
        _player.ForceSetTier(tier);
        Debug.Log($"[DevQA] Hero tier set to {TileVisual.FormatNumber(tier)} (P={TileProgression.P(tier):F2})");
    }

    void SpawnRelative(int delta)
    {
        if (_spawner == null) { Debug.LogWarning("[DevQA] ArenaSpawner not found"); return; }
        long baseTier = _player != null ? _player.CurrentTier : 2L;
        long tier = delta == 0 ? baseTier
                  : delta < 0  ? System.Math.Max(2L, baseTier / 2)
                               : baseTier * 2;
        _spawner.SpawnNPCAtTier(tier);
        Debug.Log($"[DevQA] Spawned NPC tier {TileVisual.FormatNumber(tier)}");
    }

    void LogFactors()
    {
        long tier = _player != null ? _player.CurrentTier : 2L;
        Debug.Log(GameFeelTelemetry.DumpFactors(tier));
    }

    void LogScores()
    {
        long tier = _player != null ? _player.CurrentTier : 2L;
        var report = GameFeelTelemetry.Compute(tier, _cam ?? Camera.main);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"── Telemetry Score Report — {TileVisual.FormatNumber(tier)} ──");
        sb.Append(report.FormatScores());
        Debug.Log(sb.ToString());
    }

    void RunQAScenario()
    {
        long tier  = _player != null ? _player.CurrentTier : 2L;
        var  arena = ArenaState.Instance;
        var  report = GameFeelTelemetry.Compute(tier, _cam ?? Camera.main);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("════════════════════════════════════════");
        sb.AppendLine($"QA SCENARIO  hero={TileVisual.FormatNumber(tier)}  P={TileProgression.P(tier):F1}");
        sb.AppendLine("────────────────────────────────────────");
        sb.AppendLine(GameFeelTelemetry.DumpFactors(tier));
        sb.AppendLine("────────────────────────────────────────");
        sb.Append(report.FormatScores());
        if (arena != null)
        {
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine($"Occ={arena.OccupancyRatio*100f:F0}%  " +
                          $"Density={arena.GlobalDensity:F2}  " +
                          $"TemporalP={arena.GlobalTemporalPressure:F2}  " +
                          $"Prestige={arena.GlobalPrestige:F2}");
        }
        sb.AppendLine("════════════════════════════════════════");
        Debug.Log(sb.ToString());
    }
}

#endif
