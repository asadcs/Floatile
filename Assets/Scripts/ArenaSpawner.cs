using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private GameObject specialTilePrefab; // unused but kept for scene compatibility
    [SerializeField] private Sprite     tileSprite;        // unused but kept for scene compatibility
    [SerializeField] private int        targetNPCs = 18;
    [SerializeField] private int        minNPCs = 10;
    [SerializeField] private float      spawnInterval = 0.42f;

    const float SpawnPadding     = 0.35f;
    const float SafePlayerRadius = 2.8f;

    static readonly long[]  FOOD_TIERS   = { 2L, 4L, 8L, 16L, 32L };
    static readonly float[] FOOD_WEIGHTS = { 1f, 2f, 3f, 3f, 2f };

    readonly List<GameObject> npcs = new();

    PlayerProgression playerProg;
    long heroH = 2L;
    float gameTime;

    void Start()
    {
        Time.timeScale = 1f;
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerProg = player.GetComponent<PlayerProgression>();
            if (playerProg != null)
                playerProg.OnTierChanged += OnHeroTierChanged;
        }
        StartCoroutine(SpawnLoop());
    }

    void OnDestroy()
    {
        if (playerProg != null)
            playerProg.OnTierChanged -= OnHeroTierChanged;
    }

    void Update()
    {
        gameTime += Time.deltaTime;
    }

    void OnHeroTierChanged(long tier)
    {
        heroH = System.Math.Max(2L, tier);
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(0.25f);
        while (true)
        {
            npcs.RemoveAll(n => n == null);
            int target = TargetCount();
            int deficit = Mathf.Clamp(target - npcs.Count, 0, 3);
            for (int i = 0; i < deficit; i++)
                SpawnEdgeNPC();

            yield return new WaitForSeconds(CurrentSpawnInterval());
        }
    }

    int TargetCount()
    {
        float occ = ArenaState.Instance != null ? ArenaState.Instance.OccupancyRatio : 0f;
        return Mathf.RoundToInt(Mathf.Lerp(targetNPCs, minNPCs, Mathf.Clamp01(occ * 6f)));
    }

    float CurrentSpawnInterval()
    {
        float occ = ArenaState.Instance != null ? ArenaState.Instance.OccupancyRatio : 0f;
        float pressureSlowdown = Mathf.Lerp(1f, 1.45f, Mathf.Clamp01(occ * 5f));
        return spawnInterval * pressureSlowdown;
    }

    void SpawnEdgeNPC()
    {
        if (npcPrefab == null) return;
        npcs.RemoveAll(n => n == null);

        int food = 0, monsters = 0;
        foreach (var go in npcs)
        {
            var npc = go?.GetComponent<NPCDrift>();
            if (npc != null) { if (npc.IsMonster) monsters++; else food++; }
        }

        bool spawnMonster = monsters < food || (monsters == food && Random.value < 0.5f);

        if (spawnMonster)
        {
            long tier = PickMonsterTier();
            bool rtl  = Random.value < 0.5f;
            if (rtl ? TryFindRightSpawn(tier, out Vector2 rpos) : TryFindLeftSpawn(tier, out rpos))
                SpawnNPCAsMonster(rpos, tier, rtl);
        }
        else
        {
            long tier = PickFoodTier();
            if (TryFindLeftSpawn(tier, out Vector2 pos))
                SpawnNPCAsFood(pos, tier);
        }
    }

    long PickFoodTier()
    {
        if (playerProg != null) heroH = System.Math.Max(2L, playerProg.Tier);

        // Only spawn food tiers the player can safely eat (tile <= player tier).
        // This guarantees there is always something edible regardless of player level.
        float total = 0f;
        for (int i = 0; i < FOOD_TIERS.Length; i++)
            if (FOOD_TIERS[i] <= heroH) total += FOOD_WEIGHTS[i];

        if (total <= 0f) return 2L;

        float roll = Random.value * total, cum = 0f;
        for (int i = 0; i < FOOD_TIERS.Length; i++)
        {
            if (FOOD_TIERS[i] > heroH) continue;
            cum += FOOD_WEIGHTS[i];
            if (roll <= cum) return FOOD_TIERS[i];
        }
        return 2L;
    }

    long PickMonsterTier()
    {
        if (playerProg != null) heroH = System.Math.Max(2L, playerProg.Tier);
        float t     = Mathf.Clamp01(gameTime / TileProgression.MonsterEscalationTime);
        float scale = Mathf.Lerp(TileProgression.MonsterScaleInitial, TileProgression.MonsterScaleMin, t);
        long  raw   = (long)(heroH * scale);
        int   p     = Mathf.Max(1, Mathf.RoundToInt(TileProgression.P(System.Math.Max(2L, raw))));
        return 1L << p;
    }

    bool TryFindLeftSpawn(long tier, out Vector2 pos)
    {
        float size = TileProgression.PhysicalSize(tier);
        float half = size * 0.5f;
        for (int attempt = 0; attempt < 18; attempt++)
        {
            float x = ArenaState.MinX - half - Random.Range(0.25f, 1.2f);
            float y = Random.Range(ArenaState.MinY + half + SpawnPadding, ArenaState.MaxY - half - SpawnPadding);
            pos = new Vector2(x, y);
            if (IsSpawnClear(pos, size, true))
                return true;
        }
        pos = default;
        return false;
    }

    bool TryFindRightSpawn(long tier, out Vector2 pos)
    {
        float size = TileProgression.PhysicalSize(tier);
        float half = size * 0.5f;
        for (int attempt = 0; attempt < 18; attempt++)
        {
            float x = ArenaState.MaxX + half + Random.Range(0.25f, 1.2f);
            float y = Random.Range(ArenaState.MinY + half + SpawnPadding, ArenaState.MaxY - half - SpawnPadding);
            pos = new Vector2(x, y);
            if (IsSpawnClear(pos, size, true))
                return true;
        }
        pos = default;
        return false;
    }

    bool TryFindInteriorSpawn(long tier, out Vector2 pos)
    {
        float size = TileProgression.PhysicalSize(tier);
        float half = size * 0.5f;
        for (int attempt = 0; attempt < 18; attempt++)
        {
            float x = Random.Range(ArenaState.MinX + half + SpawnPadding, ArenaState.MaxX - half - SpawnPadding);
            float y = Random.Range(ArenaState.MinY + half + SpawnPadding, ArenaState.MaxY - half - SpawnPadding);
            pos = new Vector2(x, y);
            if (IsSpawnClear(pos, size, false))
                return true;
        }
        pos = default;
        return false;
    }

    bool IsSpawnClear(Vector2 pos, float size, bool allowOffLeft)
    {
        var half = Vector2.one * (size * 0.46f);
        var hits = Physics2D.OverlapBoxAll(pos, half * 2f, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;
            if (h.GetComponent<NPCDrift>() != null || h.GetComponent<SpecialTile>() != null || h.GetComponent<PlayerProgression>() != null)
                return false;
        }

        if (playerProg != null)
        {
            float dist = Vector2.Distance(pos, playerProg.transform.position);
            if (dist < SafePlayerRadius && !allowOffLeft)
                return false;
        }
        return true;
    }

    void SpawnNPCAsFood(Vector2 pos, long tier)
    {
        var obj = Instantiate(npcPrefab, pos, Quaternion.identity);
        var npc = obj.GetComponent<NPCDrift>();
        if (npc != null) { npc.Tier = tier; npc.SetMonster(false); npc.SetDirection(false); }
        npcs.Add(obj);
    }

    void SpawnNPCAsMonster(Vector2 pos, long tier, bool rightToLeft)
    {
        var obj = Instantiate(npcPrefab, pos, Quaternion.identity);
        var npc = obj.GetComponent<NPCDrift>();
        if (npc != null) { npc.Tier = tier; npc.SetMonster(true); npc.SetDirection(rightToLeft); }
        npcs.Add(obj);
    }

    public void SpawnNPCAtTier(long tier)
    {
        if (TryFindInteriorSpawn(tier, out Vector2 pos))
        {
            var obj = Instantiate(npcPrefab, pos, Quaternion.identity);
            var npc = obj.GetComponent<NPCDrift>();
            if (npc != null) npc.Tier = tier;
            npcs.Add(obj);
        }
    }

    public void ForceSpawnMonster()
    {
        long tier = PickMonsterTier();
        bool rtl  = Random.value < 0.5f;
        if (rtl ? TryFindRightSpawn(tier, out Vector2 pos) : TryFindLeftSpawn(tier, out pos))
            SpawnNPCAsMonster(pos, tier, rtl);
    }

    public void ClearAllNPCs()
    {
        foreach (var n in npcs)
            if (n != null) Destroy(n);
        npcs.Clear();
    }

    static long SafeDouble(long value) => value <= long.MaxValue / 2 ? value * 2L : long.MaxValue;
}
