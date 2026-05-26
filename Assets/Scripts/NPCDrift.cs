using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TileVisual))]
public sealed class NPCDrift : MonoBehaviour
{
    [SerializeField] private long tier = 2;

    public long Tier
    {
        get => tier;
        set
        {
            tier      = value;
            baseSpeed = TileProgression.NpcSpeed(tier);
            wander    = TileProgression.WanderScore(tier);
            visual?.SetTier(tier);
            if (ArenaState.Instance != null) ArenaState.Instance.UpdateTier(_arenaId, tier);
        }
    }

    Rigidbody2D      rb;
    TileVisual       visual;
    CircleCollider2D col;
    Transform        playerTransform;
    float            baseSpeed;
    float            wander;
    float            vertDir;
    float            wanderTimer;
    int              _arenaId = -1;

    readonly Collider2D[] _overlapBuffer = new Collider2D[20];

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        visual = GetComponent<TileVisual>();
        col    = GetComponent<CircleCollider2D>();

        rb.bodyType                 = RigidbodyType2D.Kinematic;
        rb.gravityScale             = 0f;
        rb.constraints              = RigidbodyConstraints2D.FreezeRotation;
        rb.useFullKinematicContacts = true;

        vertDir     = Random.Range(-1f, 1f);
        wanderTimer = Random.Range(1.5f, 4f);

        baseSpeed = TileProgression.NpcSpeed(tier);
        wander    = TileProgression.WanderScore(tier);
        visual.SetTier(tier);
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;
        if (ArenaState.Instance != null) _arenaId = ArenaState.Instance.Register(tier);
    }

    void OnDestroy() => ArenaState.Instance?.Unregister(_arenaId);

    void FixedUpdate()
    {
        if (rb.position.x > ArenaState.MaxX)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 vel = new(baseSpeed, vertDir * wander);

        // Prey flees; predator chases. Determined by P-unit gap relative to player.
        if (playerTransform != null)
        {
            var prog = playerTransform.GetComponent<PlayerProgression>();
            if (prog != null && tier < prog.Tier)
            {
                // Flee: this NPC is smaller than the player — run away within avoidance range.
                float dist       = Vector2.Distance(rb.position, (Vector2)playerTransform.position);
                float avoidRange = Mathf.Max(TileProgression.InfluenceRadius(tier), TileProgression.AvoidanceMinRadius);
                if (dist < avoidRange)
                {
                    Vector2 away = (rb.position - (Vector2)playerTransform.position).normalized;
                    vel += away * baseSpeed * 0.8f;
                }
            }
            else if (prog != null &&
                     TileProgression.P(tier) >= TileProgression.P(prog.Tier) + TileProgression.PredatorMinDeltaP)
            {
                // Chase: this NPC is >= PredatorMinDeltaP P-units bigger — drift toward player.
                float dist       = Vector2.Distance(rb.position, (Vector2)playerTransform.position);
                float chaseRange = TileProgression.InfluenceRadius(tier) * TileProgression.ChaseDetectFactor;
                if (dist < chaseRange)
                {
                    Vector2 toward = ((Vector2)playerTransform.position - rb.position).normalized;
                    vel += toward * baseSpeed * TileProgression.ChaseSpeedFactor;
                }
            }
        }

        Vector2 next = rb.position + vel * Time.fixedDeltaTime;

        // Vertical wall bounce
        if (next.y < ArenaState.MinY || next.y > ArenaState.MaxY)
        {
            vertDir = -vertDir;
            vel.y   = -vel.y;
            next.y  = Mathf.Clamp(next.y, ArenaState.MinY, ArenaState.MaxY);
        }

        // Hard positional separation — overrides all velocity-based movement.
        next = ResolveOverlaps(next);

        rb.MovePosition(next);

        wanderTimer -= Time.fixedDeltaTime;
        if (wanderTimer <= 0f)
        {
            vertDir     = Random.Range(-1f, 1f);
            wanderTimer = Random.Range(1.5f, 4f);
        }
    }

    // Push `pos` out of any overlapping tile colliders. Runs after velocity so it
    // acts as a hard constraint — tiles can never end a frame inside each other.
    // Parallel correction: accumulate ALL push vectors before applying so 3+ tile
    // clusters resolve correctly (sequential would corrupt later iterations).
    Vector2 ResolveOverlaps(Vector2 pos)
    {
        float myR = col != null
            ? col.radius * transform.lossyScale.x
            : TileProgression.PhysicalSize(tier) * 0.5f;

        float queryR = myR + TileProgression.SizeMax + 0.2f;
        int   n      = Physics2D.OverlapCircleNonAlloc(pos, queryR, _overlapBuffer);

        Vector2 correction = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            var other = _overlapBuffer[i];
            if (other == null || other.gameObject == gameObject) continue;
            bool isNPC    = other.GetComponent<NPCDrift>() != null;
            bool isPlayer = other.GetComponent<PlayerProgression>() != null;
            if (!isNPC && !isPlayer) continue;

            var   otherCircle = other as CircleCollider2D;
            float otherR      = otherCircle != null
                ? otherCircle.radius * other.transform.lossyScale.x
                : TileProgression.PhysicalSize(tier) * 0.5f;

            Vector2 delta   = pos - (Vector2)other.transform.position;
            float   minDist = myR + otherR;
            float   dist    = delta.magnitude;

            if (dist < minDist)
            {
                Vector2 dir = dist > 0.001f ? delta / dist : RandomDir();
                correction += dir * (minDist - dist);
            }
        }
        return pos + correction;
    }

    static Vector2 RandomDir()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }
}
