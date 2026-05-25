using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private GameObject specialTilePrefab;
    [SerializeField] private Sprite     tileSprite;

    const float MIN_X = -40f, MAX_X = 40f;
    const float MIN_Y =   0f, MAX_Y = 60f;

    // Special tile limits
    const int   MAX_WHITE         = 2;
    const int   MAX_BLACK         = 1;
    const float SPECIAL_RESPAWN   = 8f;

    // NPC count by player tier bracket
    const int COUNT_LOW    = 14;   // tier < 32
    const int COUNT_MID    = 10;   // 32 <= tier < 256
    const int COUNT_HIGH   = 7;    // tier >= 256

    const float SAFE_SPAWN_DURATION = 30f;

    List<GameObject> npcs    = new();
    int whiteCount, blackCount;
    float elapsed;

    PlayerProgression playerProg;
    Camera            mainCam;

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) playerProg = player.GetComponent<PlayerProgression>();
        mainCam = Camera.main;

        SpawnInitialNPCs();
        SpawnSpecialTiles();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        int target = TargetNPCCount();

        // Remove destroyed NPCs from list
        npcs.RemoveAll(n => n == null);

        // Spawn replacements until we hit target
        while (npcs.Count < target)
            SpawnOneNPC();
    }

    // ── NPC spawning ──────────────────────────────────────────────────────────

    void SpawnInitialNPCs()
    {
        int count = TargetNPCCount();
        for (int i = 0; i < count; i++)
            SpawnOneNPC();
    }

    void SpawnOneNPC()
    {
        if (npcPrefab == null || mainCam == null) return;

        long tier = PickNPCTier();
        Vector2 pos = SpawnPosition();

        GameObject obj = Instantiate(npcPrefab, pos, Quaternion.identity);
        var npc = obj.GetComponent<NPCDrift>();
        if (npc != null) npc.Tier = tier;

        npcs.Add(obj);
    }

    long PickNPCTier()
    {
        long playerTier = playerProg != null ? playerProg.Tier : 2L;
        bool safe = elapsed < SAFE_SPAWN_DURATION;

        long minTier = safe ? 2L : System.Math.Max(2L, playerTier / 4);
        long maxTier = safe ? 4L : playerTier * 4;

        // Pick a random power-of-2 between min and max
        int minExp = Mathf.Max(1, (int)Mathf.Log(minTier, 2f));
        int maxExp = (int)Mathf.Log(maxTier, 2f);
        if (maxExp < minExp) maxExp = minExp;

        int exp  = Random.Range(minExp, maxExp + 1);
        return 1L << exp;
    }

    Vector2 SpawnPosition()
    {
        // Spawn just off the LEFT edge of the camera view
        float camLeft = mainCam != null
            ? mainCam.transform.position.x - mainCam.orthographicSize * mainCam.aspect - 1f
            : MIN_X;
        camLeft = Mathf.Clamp(camLeft, MIN_X, MAX_X);
        float y = Random.Range(MIN_Y + 2f, MAX_Y - 2f);
        return new Vector2(camLeft, y);
    }

    int TargetNPCCount()
    {
        long tier = playerProg != null ? playerProg.Tier : 2L;
        if (tier < 32)  return COUNT_LOW;
        if (tier < 256) return COUNT_MID;
        return COUNT_HIGH;
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
            yield return new WaitForSeconds(SPECIAL_RESPAWN + Random.Range(0f, 3f));
        }
    }

    IEnumerator SpawnBlack()
    {
        while (true)
        {
            SpawnSpecial(SpecialTile.TileKind.Black);
            yield return new WaitForSeconds(SPECIAL_RESPAWN + Random.Range(0f, 3f));
        }
    }

    void SpawnSpecial(SpecialTile.TileKind kind)
    {
        if (specialTilePrefab == null) return;

        long playerTier = playerProg != null ? playerProg.Tier : 2L;

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
            op = SpecialTile.BlackOpFor(playerTier);
        }

        Vector2 pos = new(
            Random.Range(MIN_X + 5f, MAX_X - 5f),
            Random.Range(MIN_Y + 5f, MAX_Y - 5f));

        GameObject obj = Instantiate(specialTilePrefab, pos, Quaternion.identity);
        var st = obj.GetComponent<SpecialTile>();
        st?.Init(op, kind, tileSprite);

        // Destroy after 30s if not collected
        Destroy(obj, 30f);
    }
}
