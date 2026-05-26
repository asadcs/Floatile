using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private GameObject specialTilePrefab;
    [SerializeField] private Sprite     tileSprite;

    [SerializeField] private int maxNPCs = 15;
    [SerializeField] private int minNPCs = 5;

    const int   MAX_WHITE       = 1;
    const int   MAX_BLACK       = 1;
    const float SPECIAL_RESPAWN = 60f;

    List<GameObject> npcs = new();

    PlayerProgression playerProg;
    Sprite            goldenSprite;
    Sprite            hellSprite;

    // Resources.Load<Sprite> fails when texture is imported as Multiple sprites.
    // Loading as Texture2D + Sprite.Create always works regardless of import settings.
    static Sprite LoadSprite(string resourceName)
    {
        var tex = Resources.Load<Texture2D>(resourceName);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                             new Vector2(0.5f, 0.5f), 100f);
    }

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerProg = player.GetComponent<PlayerProgression>();

        goldenSprite = LoadSprite("golden_tile");
        hellSprite   = LoadSprite("hell_tile");

        SpawnInitialNPCs();
        SpawnSpecialTiles();
        StartCoroutine(SpawnLoop());
    }

    // Temporal spawn loop: interval driven by GlobalTemporalPressure — arena breathes naturally.
    // Spawns up to 2 tiles per tick when the deficit is large so eating never empties the arena.
    IEnumerator SpawnLoop()
    {
        while (true)
        {
            npcs.RemoveAll(n => n == null);
            int deficit = TargetNPCCount() - npcs.Count;
            int batch   = Mathf.Clamp(deficit, 0, 2);
            for (int i = 0; i < batch; i++)
                SpawnEdgeNPC();

            float pressure = ArenaState.Instance != null
                ? ArenaState.Instance.GlobalTemporalPressure : 0f;
            yield return new WaitForSeconds(TileProgression.TargetSpawnInterval(pressure));
        }
    }

    // NPC target count shrinks as arena fills — occupancy-pressure self-balancing.
    int TargetNPCCount()
    {
        float occ = ArenaState.Instance != null ? ArenaState.Instance.OccupancyRatio : 0f;
        return Mathf.RoundToInt(Mathf.Lerp(maxNPCs, minNPCs, occ));
    }

    // ── NPC spawning ──────────────────────────────────────────────────────────

    void SpawnInitialNPCs()
    {
        for (int i = 0; i < maxNPCs; i++)
            SpawnInteriorNPC();
    }

    void SpawnInteriorNPC()
    {
        if (npcPrefab == null) return;
        long  tier   = PickSpawnTier();
        float checkR = TileProgression.PhysicalSize(tier);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var pos = new Vector2(
                Random.Range(ArenaState.MinX + checkR + 0.5f, ArenaState.MaxX - checkR - 0.5f),
                Random.Range(ArenaState.MinY + checkR + 0.5f, ArenaState.MaxY - checkR - 0.5f));
            if (Physics2D.OverlapCircle(pos, checkR * 0.9f) == null)
            {
                SpawnNPC(pos, tier);
                return;
            }
        }
    }

    void SpawnEdgeNPC()
    {
        if (npcPrefab == null) return;
        long  tier   = PickSpawnTier();
        float checkR = TileProgression.PhysicalSize(tier);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float y = Random.Range(ArenaState.MinY + checkR + 0.5f, ArenaState.MaxY - checkR - 0.5f);
            var   pos = new Vector2(ArenaState.MinX + checkR, y);
            if (Physics2D.OverlapCircle(pos, checkR * 0.9f) == null)
            {
                SpawnNPC(pos, tier);
                return;
            }
        }
    }

    void SpawnNPC(Vector2 pos, long tier = 0)
    {
        if (tier == 0) tier = PickSpawnTier();
        GameObject obj = Instantiate(npcPrefab, pos, Quaternion.identity);
        var npc = obj.GetComponent<NPCDrift>();
        if (npc != null) npc.Tier = tier;
        npcs.Add(obj);
    }

    // ── Dev QA helpers (dev/editor only) ─────────────────────────────────────

    public void SpawnNPCAtTier(long tier)
    {
        if (npcPrefab == null) return;
        float checkR = TileProgression.PhysicalSize(tier);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var pos = new Vector2(
                Random.Range(ArenaState.MinX + checkR + 0.5f, ArenaState.MaxX - checkR - 0.5f),
                Random.Range(ArenaState.MinY + checkR + 0.5f, ArenaState.MaxY - checkR - 0.5f));
            if (Physics2D.OverlapCircle(pos, checkR * 0.9f) == null)
            {
                SpawnNPC(pos, tier);
                return;
            }
        }
    }

    public void ClearAllNPCs()
    {
        foreach (var n in npcs)
            if (n != null) Destroy(n);
        npcs.Clear();
    }

    public void ForceSpawnWhite() => SpawnSpecial(SpecialTile.TileKind.White);
    public void ForceSpawnBlack() => SpawnSpecial(SpecialTile.TileKind.Black);

    // Blended spawn distribution: max(SpawnWeight, AvailabilityScore).
    // SpawnWeight (relative) creates a tension zone around the player tier — prey below, threats above.
    // AvailabilityScore (absolute) floors small-tier weights so tiny prey always exists.
    // Pool extends ThreatRangeAboveP P-units above player so threats always appear at any tier.
    long PickSpawnTier()
    {
        long player  = playerProg != null ? playerProg.CurrentTier : 2L;
        int  playerP = Mathf.Max(1, Mathf.RoundToInt(TileProgression.P(player)));
        int  maxP    = playerP + TileProgression.ThreatRangeAboveP;

        var candidates = new List<(long tier, float w)>(maxP);
        for (int p = 1; p <= maxP; p++)
        {
            long  t    = 1L << p;
            float relW = TileProgression.SpawnWeight(t, player);
            float absW = TileProgression.AvailabilityScore(t);
            candidates.Add((t, Mathf.Max(relW, absW)));
        }

        float total = 0f;
        foreach (var c in candidates) total += c.w;

        float roll = Random.value * total, cum = 0f;
        foreach (var c in candidates)
        {
            cum += c.w;
            if (roll <= cum) return c.tier;
        }
        return candidates[0].tier;
    }

    // ── Special tile spawning ─────────────────────────────────────────────────

    void SpawnSpecialTiles()
    {
        for (int i = 0; i < MAX_WHITE; i++) StartCoroutine(SpawnWhite());
        for (int i = 0; i < MAX_BLACK; i++) StartCoroutine(SpawnBlack());
    }

    IEnumerator SpawnWhite()
    {
        while (true)
        {
            SpawnSpecial(SpecialTile.TileKind.White);
            yield return new WaitForSeconds(SPECIAL_RESPAWN + Random.Range(0f, 45f));
        }
    }

    IEnumerator SpawnBlack()
    {
        while (true)
        {
            SpawnSpecial(SpecialTile.TileKind.Black);
            yield return new WaitForSeconds(SPECIAL_RESPAWN + Random.Range(0f, 45f));
        }
    }

    void SpawnSpecial(SpecialTile.TileKind kind)
    {
        if (specialTilePrefab == null) return;

        long p = playerProg != null ? playerProg.CurrentTier : 2L;

        SpecialTile.OperatorType op;
        if (kind == SpecialTile.TileKind.White)
        {
            var whiteOps = new[] {
                SpecialTile.OperatorType.Multiply2,
                SpecialTile.OperatorType.Multiply3,
                SpecialTile.OperatorType.Square,
                SpecialTile.OperatorType.Cube
            };
            op = whiteOps[Random.Range(0, whiteOps.Length)];
        }
        else
        {
            op = SpecialTile.BlackOpFor(p);
        }

        Vector2 pos = new(
            Random.Range(ArenaState.MinX + 2f, ArenaState.MaxX - 2f),
            Random.Range(ArenaState.MinY + 2f, ArenaState.MaxY - 2f));

        GameObject obj = Instantiate(specialTilePrefab, pos, Quaternion.identity);
        var st = obj.GetComponent<SpecialTile>();
        Sprite art = kind == SpecialTile.TileKind.White ? goldenSprite : hellSprite;
        st?.Init(op, kind, tileSprite, p, art);
    }
}
