using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private GameObject specialTilePrefab;
    [SerializeField] private Sprite     tileSprite;
    [SerializeField] private int        maxNPCs = 15;
    [SerializeField] private int        minNPCs = 5;

    // ── Stage weight tables — slot order: [H/4, H/2, H, H×2, H×4] ───────────
    static readonly float[] EarlyWeights    = { 0.15f, 0.35f, 0.40f, 0.08f, 0.02f };
    static readonly float[] MidWeights      = { 0.20f, 0.30f, 0.30f, 0.15f, 0.05f };
    static readonly float[] LateWeights     = { 0.15f, 0.25f, 0.30f, 0.20f, 0.10f };
    static readonly float[] RecoveryWeights = { 0.40f, 0.40f, 0.15f, 0.04f, 0.01f };

    List<GameObject> npcs = new();
    PlayerProgression playerProg;
    Sprite            goldenSprite;
    Sprite            hellSprite;

    // ── Pool: 5 slots (H/4, H/2, H, H×2, H×4), each clamped ≥ 2 ────────────
    long             _heroH  = 2L;
    readonly long[]  _slots  = new long[5];

    // ── Spawn director state ──────────────────────────────────────────────────
    readonly long[] _recent       = new long[6]; // ring buffer, length = DirectorHRecentLimit
    int             _recentHead   = 0;
    int             _recentCount  = 0;
    int             _dangerStreak = 0;
    long            _lastTier     = 0L;
    int             _sameStreak   = 0;
    int             _recoveryLeft = 0;

    // ── Special tile cooldowns ────────────────────────────────────────────────
    float _goldenCooldown = 0f;
    float _hellCooldown   = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

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
        if (player != null)
        {
            playerProg = player.GetComponent<PlayerProgression>();
            if (playerProg != null)
                playerProg.OnTierChanged += OnHeroTierChanged;
        }

        goldenSprite = LoadSprite("golden_tile");
        hellSprite   = LoadSprite("hell_tile");

        RebuildPool();
        SpawnInitialNPCs();
        StartCoroutine(SpawnLoop());
    }

    void OnDestroy()
    {
        if (playerProg != null)
            playerProg.OnTierChanged -= OnHeroTierChanged;
    }

    void Update()
    {
        if (_goldenCooldown > 0f) _goldenCooldown -= Time.deltaTime;
        if (_hellCooldown   > 0f) _hellCooldown   -= Time.deltaTime;
    }

    void OnHeroTierChanged(long _) => RebuildPool();

    // ── Pool management ───────────────────────────────────────────────────────

    void RebuildPool()
    {
        long H    = playerProg != null ? playerProg.Tier : 2L;
        bool drop = H < _heroH;
        _heroH    = H;

        _slots[0] = Math.Max(2L, H >> 2);                                   // H/4
        _slots[1] = Math.Max(2L, H >> 1);                                   // H/2
        _slots[2] = H;                                                       // H
        _slots[3] = H <= (long.MaxValue >> 1) ? H << 1 : long.MaxValue;     // H×2
        _slots[4] = H <= (long.MaxValue >> 2) ? H << 2 : long.MaxValue;     // H×4

        _dangerStreak = 0;
        if (drop) _recoveryLeft = TileProgression.DirectorRecoverySpawns;
    }

    float[] CurrentWeights() =>
        _recoveryLeft > 0                        ? RecoveryWeights
        : _heroH <= TileProgression.EarlyGameMaxH ? EarlyWeights
        : _heroH >= TileProgression.LateGameMinH  ? LateWeights
        : MidWeights;

    // Roll a tier from the pool. Slots mapping to the same tier have weights summed.
    // exclude=0 means no exclusion (0 can never be a valid tier).
    long RollFromPool(float[] slotWeights, long exclude)
    {
        long[]  tiers   = new long[5];
        float[] weights = new float[5];
        int     n       = 0;

        for (int i = 0; i < 5; i++)
        {
            long t = _slots[i];
            if (t == exclude) continue;

            int found = -1;
            for (int j = 0; j < n; j++) if (tiers[j] == t) { found = j; break; }
            if (found >= 0) weights[found] += slotWeights[i];
            else            { tiers[n] = t; weights[n] = slotWeights[i]; n++; }
        }

        float total = 0f;
        for (int i = 0; i < n; i++) total += weights[i];
        if (total <= 0f) return _slots[2]; // fallback: H

        float roll = Random.value * total, cum = 0f;
        for (int i = 0; i < n; i++)
        {
            cum += weights[i];
            if (roll <= cum) return tiers[i];
        }
        return tiers[n - 1];
    }

    // ── Spawn director ────────────────────────────────────────────────────────

    long DirectorPickTier()
    {
        // Rule 4 + Rule 1 (highest priority): danger overload → force safe (H/2)
        if (_dangerStreak >= TileProgression.DirectorMaxDangerStreak ||
            DangerFraction() >= TileProgression.DirectorDangerLimit)
            return CommitSpawn(_slots[1]);

        // Rule 3: H absent from recent history → force H
        if (!HSeenRecently())
            return CommitSpawn(_slots[2]);

        // Rule 2: same-value streak → exclude that tier from roll
        long exclude = _sameStreak >= TileProgression.DirectorMaxSameStreak ? _lastTier : 0L;

        // Rule 5 folded into weights: recovery mode uses RecoveryWeights
        return CommitSpawn(RollFromPool(CurrentWeights(), exclude));
    }

    long CommitSpawn(long tier)
    {
        _recent[_recentHead] = tier;
        _recentHead          = (_recentHead + 1) % _recent.Length;
        if (_recentCount < _recent.Length) _recentCount++;

        _dangerStreak = tier >= _slots[3] ? _dangerStreak + 1 : 0;
        _sameStreak   = tier == _lastTier  ? _sameStreak   + 1 : 0;
        _lastTier     = tier;
        if (_recoveryLeft > 0) _recoveryLeft--;

        return tier;
    }

    bool HSeenRecently()
    {
        long H     = _slots[2];
        int  limit = Math.Min(_recentCount, TileProgression.DirectorHRecentLimit);
        for (int i = 0; i < limit; i++)
        {
            int idx = (_recentHead - 1 - i + _recent.Length) % _recent.Length;
            if (_recent[idx] == H) return true;
        }
        return false;
    }

    float DangerFraction()
    {
        if (npcs.Count == 0) return 0f;
        long dangerThreshold = _slots[3];
        int  valid = 0, danger = 0;
        foreach (var n in npcs)
        {
            if (n == null) continue;
            valid++;
            var npc = n.GetComponent<NPCDrift>();
            if (npc != null && npc.Tier >= dangerThreshold) danger++;
        }
        return valid > 0 ? (float)danger / valid : 0f;
    }

    // ── NPC spawn loop ────────────────────────────────────────────────────────

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

    void SpawnInitialNPCs()
    {
        for (int i = 0; i < maxNPCs; i++)
            SpawnInteriorNPC();
    }

    // Interior spawn: bypasses director (no history yet), uses stage weights directly.
    void SpawnInteriorNPC()
    {
        if (npcPrefab == null) return;
        long  tier   = RollFromPool(CurrentWeights(), 0L);
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

    // Edge spawn: runs special roll first, then director-chosen tier.
    void SpawnEdgeNPC()
    {
        if (npcPrefab == null) return;

        // Special tile roll — blocked by cooldown falls through to normal NPC
        float r = Random.value;
        if (r < TileProgression.SpecialHellChance && _hellCooldown <= 0f)
        {
            SpawnSpecial(SpecialTile.TileKind.Black);
            _hellCooldown = Random.Range(TileProgression.HellCooldownMin, TileProgression.HellCooldownMax);
            return;
        }
        if (r < TileProgression.SpecialHellChance + TileProgression.SpecialGoldenChance
            && _goldenCooldown <= 0f)
        {
            SpawnSpecial(SpecialTile.TileKind.White);
            _goldenCooldown = Random.Range(TileProgression.GoldenCooldownMin, TileProgression.GoldenCooldownMax);
            return;
        }

        long  tier   = DirectorPickTier();
        float checkR = TileProgression.PhysicalSize(tier);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float y   = Random.Range(ArenaState.MinY + checkR + 0.5f, ArenaState.MaxY - checkR - 0.5f);
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
        if (tier == 0) tier = DirectorPickTier();
        GameObject obj = Instantiate(npcPrefab, pos, Quaternion.identity);
        var npc = obj.GetComponent<NPCDrift>();
        if (npc != null) npc.Tier = tier;
        npcs.Add(obj);
    }

    // ── Special tile spawning ─────────────────────────────────────────────────

    void SpawnSpecial(SpecialTile.TileKind kind)
    {
        if (specialTilePrefab == null) return;
        long p  = playerProg != null ? playerProg.Tier : 2L;
        var  op = kind == SpecialTile.TileKind.White
            ? SpecialTile.OperatorType.Multiply2
            : SpecialTile.OperatorType.DivideBy2;

        Vector2 pos = new(
            Random.Range(ArenaState.MinX + 2f, ArenaState.MaxX - 2f),
            Random.Range(ArenaState.MinY + 2f, ArenaState.MaxY - 2f));

        GameObject obj = Instantiate(specialTilePrefab, pos, Quaternion.identity);
        var st  = obj.GetComponent<SpecialTile>();
        Sprite art = kind == SpecialTile.TileKind.White ? goldenSprite : hellSprite;
        st?.Init(op, kind, tileSprite, p, art);
    }

    // ── Dev QA helpers ────────────────────────────────────────────────────────

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
}
