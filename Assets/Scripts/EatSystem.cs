using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(PlayerProgression))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(TileHitbox))]
public sealed class EatSystem : MonoBehaviour
{
    public event Action<long>        OnEat;
    public event Action<long>        OnEvolve;
    public event Action<long>        OnPenalty;
    public event Action<SpecialTile> OnSpecialCollect;

    public Vector2 LastEatPos { get; private set; }

    const float ContactSkin = 0.015f;
    const float CollisionDebounce = 0.08f;

    bool invincible;
    float nextCollisionTime;
    PlayerProgression progression;
    TileVisual visual;
    TileHitbox selfHitbox;
    Vector2 previousCenter;

    readonly Collider2D[] overlapBuffer = new Collider2D[32];

    void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        visual = GetComponent<TileVisual>();
        selfHitbox = GetComponent<TileHitbox>();
        if (selfHitbox == null) selfHitbox = gameObject.AddComponent<TileHitbox>();
        RefreshCollider();
        previousCenter = selfHitbox.Bounds.center;

        progression.OnTierChanged += RefreshVisual;
    }

    void OnDestroy()
    {
        if (progression != null)
            progression.OnTierChanged -= RefreshVisual;
    }

    void RefreshVisual(long tier)
    {
        visual?.SetTier(tier);
        RefreshCollider();
    }

    void RefreshCollider()
    {
        selfHitbox?.Sync();
    }

    void FixedUpdate()
    {
        if (Time.time < nextCollisionTime) return;
        var target = FindNearestTrueOverlap();
        if (target != null)
            HandleContact(target);
        previousCenter = selfHitbox.Bounds.center;
    }

    Collider2D FindNearestTrueOverlap()
    {
        Bounds currentBounds = selfHitbox.Bounds;
        Bounds queryBounds = SweptBounds(currentBounds, previousCenter);
        int count = Physics2D.OverlapBoxNonAlloc(queryBounds.center, queryBounds.size, 0f, overlapBuffer);

        Collider2D best = null;
        bool bestIsCurrentOverlap = false;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var other = overlapBuffer[i];
            if (other == null || other.gameObject == gameObject) continue;
            if (!IsGameplayTarget(other)) continue;
            var otherHitbox = other.GetComponent<TileHitbox>();
            if (otherHitbox == null) continue;

            Bounds otherBounds = otherHitbox.Bounds;
            bool currentOverlap = TileHitbox.Touches(currentBounds, otherBounds, ContactSkin);
            bool sweptOverlap = currentOverlap || SweptAabbTouches(previousCenter, currentBounds, otherBounds);
            if (!sweptOverlap) continue;

            float d = ((Vector2)otherBounds.center - (Vector2)currentBounds.center).sqrMagnitude;
            if ((currentOverlap && !bestIsCurrentOverlap) ||
                (currentOverlap == bestIsCurrentOverlap && d < bestDist))
            {
                bestIsCurrentOverlap = currentOverlap;
                bestDist = d;
                best = other;
            }
        }
        return best;
    }

    static bool IsGameplayTarget(Collider2D other)
        => other.GetComponent<NPCDrift>() != null || other.GetComponent<SpecialTile>() != null;

    static Bounds SweptBounds(Bounds current, Vector2 previous)
    {
        Vector2 currentCenter = current.center;
        Vector2 min = Vector2.Min(previous, currentCenter) - (Vector2)current.extents;
        Vector2 max = Vector2.Max(previous, currentCenter) + (Vector2)current.extents;
        var bounds = new Bounds((min + max) * 0.5f, max - min);
        bounds.Expand(0.05f);
        return bounds;
    }

    static bool SweptAabbTouches(Vector2 previous, Bounds current, Bounds target)
    {
        Vector2 currentCenter = current.center;
        Vector2 delta = currentCenter - previous;
        if (delta.sqrMagnitude < 0.000001f) return false;

        Bounds expanded = target;
        expanded.Expand(current.size);
        return SegmentIntersectsAabb(previous, currentCenter, expanded);
    }

    static bool SegmentIntersectsAabb(Vector2 start, Vector2 end, Bounds box)
    {
        Vector2 delta = end - start;
        float tMin = 0f;
        float tMax = 1f;
        if (!ClipAxis(start.x, delta.x, box.min.x, box.max.x, ref tMin, ref tMax)) return false;
        if (!ClipAxis(start.y, delta.y, box.min.y, box.max.y, ref tMin, ref tMax)) return false;
        return true;
    }

    static bool ClipAxis(float start, float delta, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(delta) < 0.000001f)
            return start >= min && start <= max;

        float inv = 1f / delta;
        float t1 = (min - start) * inv;
        float t2 = (max - start) * inv;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        return tMin <= tMax;
    }

    void HandleContact(Collider2D other)
    {
        nextCollisionTime = Time.time + CollisionDebounce;

        var special = other.GetComponent<SpecialTile>();
        if (special != null)
        {
            progression.ApplySpecialEffect(special.Operator);
            OnSpecialCollect?.Invoke(special);
            CollisionFX.FlashPopDestroy(other.gameObject, this);
            return;
        }

        var npc = other.GetComponent<NPCDrift>();
        if (npc == null) return;

        long playerTier = progression.Tier;
        long enemyTier = npc.Tier;
        LastEatPos = other.transform.position;

        if (playerTier > enemyTier)
        {
            progression.AddPoints(enemyTier);
            OnEat?.Invoke(enemyTier);
            StartCoroutine(CollisionFX.HitFreeze(0.016f));
            CollisionFX.FlashPopDestroy(other.gameObject, this);
        }
        else if (playerTier == enemyTier)
        {
            progression.EvolveInstant();
            OnEvolve?.Invoke(progression.Tier);
            StartCoroutine(CollisionFX.HitFreeze(0.05f));
            CollisionFX.FlashPopDestroy(other.gameObject, this);
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

    void OnDrawGizmos()
    {
        if (!TileProgression.DebugHitboxes) return;
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
    }
}
