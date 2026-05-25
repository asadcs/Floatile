using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private GameObject specialTilePrefab;
    [SerializeField] private Sprite     tileSprite;

    [SerializeField] private int maxNPCs = 10;
    [SerializeField] private int minNPCs = 3;

    const int   MAX_WHITE       = 1;
    const int   MAX_BLACK       = 1;
    const float SPECIAL_RESPAWN = 60f;

    List<GameObject> npcs = new();

    PlayerProgression playerProg;

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerProg = player.GetComponent<PlayerProgression>();

        SpawnInitialNPCs();
        SpawnSpecialTiles();
    }

    void Update()
    {
        npcs.RemoveAll(n => n == null);
        int target = TargetNPCCount();
        while (npcs.Count < target)
            SpawnEdgeNPC();
    }

    // NPC target count shrinks as arena fills — occupancy-pressure self-balancing
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
        Vector2 pos = new(
            Random.Range(ArenaState.MinX + 1f, ArenaState.MaxX - 1f),
            Random.Range(ArenaState.MinY + 1f, ArenaState.MaxY - 1f));
        SpawnNPC(pos);
    }

    void SpawnEdgeNPC()
    {
        if (npcPrefab == null) return;
        Vector2 pos = new(ArenaState.MinX, Random.Range(ArenaState.MinY + 1f, ArenaState.MaxY - 1f));
        SpawnNPC(pos);
    }

    void SpawnNPC(Vector2 pos)
    {
        GameObject obj = Instantiate(npcPrefab, pos, Quaternion.identity);
        var npc = obj.GetComponent<NPCDrift>();
        if (npc != null)
            npc.Tier = PickSpawnTier();
        npcs.Add(obj);
    }

    // Absolute availability: tier 2 always most common, each step × exp(-AvailabilityDecay) rarer.
    // Samples from P=1 up to P_player+2 so small tiles always exist alongside bigger ones.
    long PickSpawnTier()
    {
        long player = playerProg != null ? playerProg.CurrentTier : 2L;
        int  maxP   = Mathf.Max(4, Mathf.RoundToInt(TileProgression.P(player)) + 2);

        var candidates = new List<(long tier, float w)>(maxP);
        for (int p = 1; p <= maxP; p++)
        {
            long  t = 1L << p;
            float w = Mathf.Exp(-TileProgression.AvailabilityDecay * p);
            candidates.Add((t, w));
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
        st?.Init(op, kind, tileSprite);

        Destroy(obj, 30f);
    }
}
