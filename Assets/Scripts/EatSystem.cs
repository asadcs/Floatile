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


    public Vector2 LastEatPos { get; private set; }

    bool invincible;
    PlayerProgression progression;
    TileVisual        visual;
    CircleCollider2D  selfCircle;

    void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        visual      = GetComponent<TileVisual>();
        selfCircle  = GetComponent<CircleCollider2D>();

        progression.OnTierChanged += RefreshVisual;
    }

    void OnDestroy()
    {
        progression.OnTierChanged -= RefreshVisual;
    }

    void RefreshVisual(long tier) => visual?.SetTier(tier);

    void FixedUpdate()
    {
        if (selfCircle == null) selfCircle = GetComponent<CircleCollider2D>();
        if (selfCircle == null) return;

        foreach (var other in FindObjectsByType<CircleCollider2D>(FindObjectsInactive.Exclude))
        {
            if (other == null || other.gameObject == gameObject) continue;
            if (!IsGameplayTarget(other)) continue;
            if (TryHandleContact(other)) return;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleContact(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryHandleContact(other);
    }

    bool TryHandleContact(Collider2D other)
    {
        if (!IsVisualContact(other)) return false;

        // Special tile collision
        var special = other.GetComponent<SpecialTile>();
        if (special != null)
        {
            progression.ApplySpecialEffect(special.Operator);
            OnSpecialCollect?.Invoke(special);
            CollisionFX.FlashPopDestroy(other.gameObject, this);
            return true;
        }

        // NPC tile collision
        var npc = other.GetComponent<NPCDrift>();
        if (npc == null) return false;

        long playerTier = progression.Tier;
        long enemyTier  = npc.Tier;

        if (playerTier > enemyTier)
        {
            LastEatPos = other.transform.position;
            progression.AddPoints(enemyTier);
            OnEat?.Invoke(enemyTier);
            StartCoroutine(CollisionFX.HitFreeze(0.016f));
            CollisionFX.FlashPopDestroy(other.gameObject, this);
            return true;
        }
        else if (playerTier == enemyTier)
        {
            progression.EvolveInstant();
            OnEvolve?.Invoke(progression.Tier);
            StartCoroutine(CollisionFX.HitFreeze(0.05f));
            CollisionFX.FlashPopDestroy(other.gameObject, this);
            return true;
        }
        else
        {
            if (invincible) return true;
            progression.ApplyPenalty();
            OnPenalty?.Invoke(progression.Tier);
            StartCoroutine(InvincibilityWindow());
            return true;
        }
    }

    static bool IsGameplayTarget(Collider2D other)
        => other.GetComponent<NPCDrift>() != null || other.GetComponent<SpecialTile>() != null;

    bool IsVisualContact(Collider2D other)
    {
        if (!IsGameplayTarget(other)) return false;
        if (other.transform.position.z != transform.position.z)
            other.transform.position = new Vector3(other.transform.position.x, other.transform.position.y, transform.position.z);

        var otherCircle = other as CircleCollider2D;
        if (selfCircle == null || otherCircle == null) return false;

        float selfR = selfCircle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        float otherR = otherCircle.radius * Mathf.Max(other.transform.lossyScale.x, other.transform.lossyScale.y);
        float dist = Vector2.Distance(transform.position, other.transform.position);

        return dist <= selfR + otherR + 0.001f;
    }

    IEnumerator InvincibilityWindow()
    {
        invincible = true;
        yield return new WaitForSeconds(TileProgression.InvincibilityDuration);
        invincible = false;
    }
}
