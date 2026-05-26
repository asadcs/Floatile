using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(PlayerProgression))]
public sealed class EatSystem : MonoBehaviour
{
    public event Action<long>          OnEat;           // (enemyTier)
    public event Action<long>          OnEvolve;        // (newTier)
    public event Action<long>          OnPenalty;       // (newTier)
    public event Action<SpecialTile>   OnSpecialCollect;


    bool invincible;
    PlayerProgression progression;
    TileVisual        visual;

    void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        visual      = GetComponent<TileVisual>();

        progression.OnTierChanged += RefreshVisual;
    }

    void OnDestroy()
    {
        progression.OnTierChanged -= RefreshVisual;
    }

    void RefreshVisual(long tier) => visual?.SetTier(tier);

    void OnTriggerEnter2D(Collider2D other)
    {
        // Special tile collision
        var special = other.GetComponent<SpecialTile>();
        if (special != null)
        {
            progression.ApplySpecialEffect(special.Operator);
            OnSpecialCollect?.Invoke(special);
            Destroy(other.gameObject);
            return;
        }

        // NPC tile collision
        var npc = other.GetComponent<NPCDrift>();
        if (npc == null) return;

        long playerTier = progression.Tier;
        long enemyTier  = npc.Tier;

        if (playerTier > enemyTier)
        {
            progression.AddPoints(enemyTier);
            OnEat?.Invoke(enemyTier);
            Destroy(other.gameObject);
        }
        else if (playerTier == enemyTier)
        {
            progression.EvolveInstant();
            OnEvolve?.Invoke(progression.Tier);
            Destroy(other.gameObject);
        }
        else
        {
            if (invincible) return;
            progression.ApplyPenalty();
            OnPenalty?.Invoke(progression.Tier);
            StartCoroutine(InvincibilityWindow());
        }
    }

    IEnumerator InvincibilityWindow()
    {
        invincible = true;
        yield return new WaitForSeconds(TileProgression.InvincibilityDuration);
        invincible = false;
    }
}
