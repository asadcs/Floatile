using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TileVisual))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(TileHitbox))]
public sealed class NPCDrift : MonoBehaviour
{
    [SerializeField] private long tier = 2;

    bool isMonster;
    bool currentlyMovingLeft;
    bool spawnedFromRight;     // immutable after SetDirection — determines which side destroys this tile
    Transform playerTransform;

    static readonly Color MONSTER_COLOR = new(0.102f, 0.102f, 0.102f, 1f);

    public long Tier
    {
        get => tier;
        set
        {
            tier = System.Math.Max(2L, value);
            baseSpeed = TileProgression.NpcSpeed(tier);
            wander = TileProgression.WanderScore(tier);
            if (isMonster) visual?.SetTierForced(tier, MONSTER_COLOR);
            else           visual?.SetTier(tier);
            RefreshCollider();
            ArenaState.Instance?.UpdateTier(_arenaId, tier);
        }
    }

    public bool IsMonster   => isMonster;
    public bool MovingLeft  => currentlyMovingLeft;

    public void SetMonster(bool monster)
    {
        isMonster = monster;
        if (monster) visual?.SetTierForced(tier, MONSTER_COLOR);
        else         visual?.SetTier(tier);
        RefreshCollider();
    }

    public void SetDirection(bool rightToLeft)
    {
        currentlyMovingLeft = rightToLeft;
        spawnedFromRight    = rightToLeft;
    }

    Rigidbody2D rb;
    TileVisual  visual;
    TileHitbox  hitbox;
    float baseSpeed;
    float wander;
    float verticalDrift;
    float driftTimer;
    int _arenaId = -1;

    readonly Collider2D[] overlapBuffer = new Collider2D[24];

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        visual = GetComponent<TileVisual>();
        hitbox = GetComponent<TileHitbox>();

        rb.bodyType               = RigidbodyType2D.Kinematic;
        rb.gravityScale           = 0f;
        rb.constraints            = RigidbodyConstraints2D.FreezeRotation;
        rb.useFullKinematicContacts = true;

        RefreshCollider();

        verticalDrift = Random.Range(-1f, 1f);
        driftTimer    = Random.Range(0.8f, 2.2f);
        baseSpeed     = TileProgression.NpcSpeed(tier);
        wander        = TileProgression.WanderScore(tier);
        visual.SetTier(tier);
    }

    void Start()
    {
        _arenaId = ArenaState.Instance != null ? ArenaState.Instance.Register(tier) : -1;
        var p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    void OnDestroy() => ArenaState.Instance?.Unregister(_arenaId);

    void FixedUpdate()
    {
        // Random wander tick
        driftTimer -= Time.fixedDeltaTime;
        if (driftTimer <= 0f)
        {
            verticalDrift = Mathf.Lerp(verticalDrift, Random.Range(-1f, 1f), 0.75f);
            driftTimer    = Random.Range(0.8f, 2.4f);
        }

        // Head-on avoidance overrides wander when another NPC is approaching
        ApplyHeadonAvoidance();

        float xDir = currentlyMovingLeft ? -1f : 1f;
        Vector2 vel = new(baseSpeed * xDir, verticalDrift * wander * 0.55f);

        // Player interaction: monster chases, food evades
        if (playerTransform != null)
        {
            long playerTier = playerTransform.GetComponent<PlayerProgression>()?.Tier ?? 2L;
            Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
            float   dist     = toPlayer.magnitude;

            if (isMonster && tier > playerTier)
            {
                vel += toPlayer.normalized * baseSpeed * 0.5f;
            }
            else if (!isMonster && dist < TileProgression.FoodFleeDetectRange)
            {
                verticalDrift = rb.position.y > playerTransform.position.y ? 1f : -1f;
                driftTimer    = Random.Range(0.5f, 1.2f);
            }
        }

        Vector2 next = rb.position + vel * Time.fixedDeltaTime;
        next.y = Mathf.Clamp(next.y, ArenaState.MinY + HalfSize(), ArenaState.MaxY - HalfSize());

        // Bounce at the opposite wall; vanish at the spawn-side wall
        if (!spawnedFromRight)
        {
            // Spawned from left → bounce at right wall → vanish at left off-screen
            if (next.x >= ArenaState.MaxX) { next.x = ArenaState.MaxX; currentlyMovingLeft = true; }
            if (next.x < ArenaState.MinX - 2f) { Destroy(gameObject); return; }
        }
        else
        {
            // Spawned from right → bounce at left wall → vanish at right off-screen
            if (next.x <= ArenaState.MinX) { next.x = ArenaState.MinX; currentlyMovingLeft = false; }
            if (next.x > ArenaState.MaxX + 2f) { Destroy(gameObject); return; }
        }

        next = ResolveAabbOverlaps(next);
        rb.MovePosition(next);
    }

    // Steer vertically away from any NPC that is approaching head-on in the x-axis.
    void ApplyHeadonAvoidance()
    {
        float avoidRange = HalfSize() * 2f + 2.0f;
        float myXDir     = currentlyMovingLeft ? -1f : 1f;

        int n = Physics2D.OverlapCircleNonAlloc(rb.position, avoidRange, overlapBuffer);
        for (int i = 0; i < n; i++)
        {
            var other = overlapBuffer[i];
            if (other == null || other.gameObject == gameObject) continue;
            var otherNPC = other.GetComponent<NPCDrift>();
            if (otherNPC == null) continue;

            float theirXDir = otherNPC.MovingLeft ? -1f : 1f;
            // Only react to head-on (moving in opposite x directions)
            if (myXDir * theirXDir >= 0f) continue;

            // Only react if the other tile is ahead of us in our travel direction
            float toOtherX = other.transform.position.x - rb.position.x;
            if (myXDir * toOtherX <= 0f) continue;

            // Steer to the side that moves away from the other tile's y position
            float toOtherY = other.transform.position.y - rb.position.y;
            verticalDrift = toOtherY < 0f ? 1f : -1f;
            driftTimer    = Random.Range(0.6f, 1.5f);
            break;
        }
    }

    void RefreshCollider() => hitbox?.Sync();

    float HalfSize() => hitbox != null ? hitbox.Bounds.extents.y : TileProgression.PhysicalSize(tier) * 0.5f;

    Vector2 ResolveAabbOverlaps(Vector2 pos)
    {
        Vector2 size = hitbox != null ? hitbox.Bounds.size : Vector2.one * TileProgression.PhysicalSize(tier);
        int n = Physics2D.OverlapBoxNonAlloc(pos, size + Vector2.one * 0.02f, 0f, overlapBuffer);

        Vector2 correction = Vector2.zero;
        Bounds  mine       = new(pos, size);
        for (int i = 0; i < n; i++)
        {
            var other = overlapBuffer[i];
            if (other == null || other.gameObject == gameObject) continue;
            if (other.GetComponent<NPCDrift>() == null) continue;

            var otherHitbox = other.GetComponent<TileHitbox>();
            if (otherHitbox == null) continue;
            Bounds theirs = otherHitbox.Bounds;
            if (!TileHitbox.Touches(mine, theirs)) continue;

            float pushX = Mathf.Min(mine.max.x - theirs.min.x, theirs.max.x - mine.min.x);
            float pushY = Mathf.Min(mine.max.y - theirs.min.y, theirs.max.y - mine.min.y);
            if (pushX < pushY)
            {
                float dir = mine.center.x < theirs.center.x ? -1f : 1f;
                correction.x += dir * pushX;
            }
            else
            {
                float dir = mine.center.y < theirs.center.y ? -1f : 1f;
                correction.y += dir * pushY;
            }
        }
        return pos + correction * 0.55f;
    }

    void OnDrawGizmos()
    {
        if (!TileProgression.DebugHitboxes) return;
        var b = GetComponent<BoxCollider2D>();
        if (b == null) return;
        Gizmos.color = isMonster ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
    }
}
