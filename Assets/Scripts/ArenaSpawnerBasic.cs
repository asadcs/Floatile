using System.Collections;
using UnityEngine;

public sealed class ArenaSpawnerBasic : MonoBehaviour
{
    [SerializeField] private NPCFlowBasic npcPrefab;
    [SerializeField] private Sprite tileSprite;
    [SerializeField] private int startingNpcCount = 5;
    [SerializeField] private float respawnDelay = 0.5f;
    [SerializeField] private float leftSpawnPadding = 13.5f;
    [SerializeField] private Vector2 spawnYRange = new(5f, 55f);

    private readonly int[] tiers = { 2, 4, 8, 16, 32 };
    // Colors mapped to tiers 2→4→8→16→32 from user palette
    private readonly Color[] colors =
    {
        new Color(0.996f, 0.847f, 0.878f), // tier 2  #FED8E0 Cotton Pink
        new Color(1.000f, 0.573f, 0.702f), // tier 4  #FF92B3 Bubblegum Pink
        new Color(1.000f, 0.467f, 0.467f), // tier 8  #FF7777 Coral Candy
        new Color(0.996f, 0.690f, 0.075f), // tier 16 #FEB000 Mango Gold
        new Color(0.996f, 0.769f, 0.573f), // tier 32 #FEC492 Peach Cream
    };

    private int spawnCount = 0; // tracks total ever spawned for slot-based Y

    private void Start()
    {
        for (int i = 0; i < startingNpcCount; i++)
        {
            SpawnNpc();
        }
    }

    private void SpawnNpc()
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning("ArenaSpawnerBasic needs an NPC prefab assigned.");
            return;
        }

        float spawnX = GetLeftSpawnX();

        // Slot-based Y: divide range into equal bands, pick random point within band
        float rangeSize = spawnYRange.y - spawnYRange.x;
        float slotSize  = rangeSize / startingNpcCount;
        int   slot      = spawnCount % startingNpcCount;
        float slotMin   = spawnYRange.x + slot * slotSize + 0.5f;
        float slotMax   = slotMin + slotSize - 1f;
        float spawnY    = Random.Range(slotMin, slotMax);
        spawnCount++;

        NPCFlowBasic npc = Instantiate(npcPrefab, new Vector3(spawnX, spawnY, 0f), Quaternion.identity);
        npc.Destroyed += OnNpcDestroyed;

        int index = Random.Range(0, tiers.Length);
        TileVisual visual = npc.GetComponent<TileVisual>();

        if (visual != null)
        {
            visual.SetVisual(tileSprite, colors[index], tiers[index].ToString());
            visual.ApplyTierScale(tiers[index]);
        }
    }

    private float GetLeftSpawnX()
    {
        Camera mainCamera = Camera.main;
        return mainCamera == null ? -41f : mainCamera.transform.position.x - leftSpawnPadding;
    }

    private void OnNpcDestroyed(NPCFlowBasic npc)
    {
        npc.Destroyed -= OnNpcDestroyed;
        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnNpc();
    }
}
