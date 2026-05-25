using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class ArenaSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private GameObject specialTilePrefab;
    [SerializeField] private Sprite     tileSprite;

    const float MIN_X = -12.5f, MAX_X = 12.5f;
    const float MIN_Y =   -7f,  MAX_Y =  7f;

    const int   MAX_WHITE        = 1;
    const int   MAX_BLACK        = 1;
    const float SPECIAL_RESPAWN  = 60f;
    const int   TARGET_NPC_COUNT = 7;

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
        while (npcs.Count < TARGET_NPC_COUNT)
            SpawnEdgeNPC();
    }

    // ── NPC spawning ──────────────────────────────────────────────────────────

    // Initial fill: scatter across the arena interior so the game starts populated
    void SpawnInitialNPCs()
    {
        for (int i = 0; i < TARGET_NPC_COUNT; i++)
            SpawnInteriorNPC();
    }

    // Used for initial fill — places NPC randomly inside the arena
    void SpawnInteriorNPC()
    {
        if (npcPrefab == null) return;

        Vector2 pos = new(
            Random.Range(MIN_X + 1f, MAX_X - 1f),
            Random.Range(MIN_Y + 1f, MAX_Y - 1f));

        SpawnNPC(pos);
    }

    // Used for respawns — enters from the left edge
    void SpawnEdgeNPC()
    {
        if (npcPrefab == null) return;

        Vector2 pos = new(MIN_X, Random.Range(MIN_Y + 1f, MAX_Y - 1f));
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

    long PickSpawnTier()
    {
        long p = playerProg != null ? playerProg.CurrentTier : 2L;
        float roll = Random.value;
        if (roll < 0.65f) return System.Math.Max(2L, p / 2);
        if (roll < 0.75f) return p;
        if (roll < 0.95f) return p * 2;
        return p * 4;
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
            Random.Range(MIN_X + 2f, MAX_X - 2f),
            Random.Range(MIN_Y + 2f, MAX_Y - 2f));

        GameObject obj = Instantiate(specialTilePrefab, pos, Quaternion.identity);
        var st = obj.GetComponent<SpecialTile>();
        st?.Init(op, kind, tileSprite);

        Destroy(obj, 30f);
    }
}
