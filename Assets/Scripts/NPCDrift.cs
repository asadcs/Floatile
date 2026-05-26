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
            visual?.SetTier(tier);
            if (ArenaState.Instance != null) ArenaState.Instance.UpdateTier(_arenaId, tier);
        }
    }

    // WANDER amplitude is P-dependent — see TileProgression.WanderScore()

    Rigidbody2D rb;
    TileVisual  visual;
    Transform   playerTransform;
    float       baseSpeed;
    float       vertDir;
    float       wanderTimer;
    int         _arenaId = -1;

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        visual = GetComponent<TileVisual>();

        rb.bodyType     = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        vertDir     = Random.Range(-1f, 1f);
        wanderTimer = Random.Range(1.5f, 4f);

        baseSpeed = TileProgression.NpcSpeed(tier);
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

        float wander = TileProgression.WanderScore(tier);
        Vector2 vel = new(baseSpeed, vertDir * wander);

        // Proximity avoidance: flee if smaller than player and within influence radius
        if (playerTransform != null)
        {
            var prog = playerTransform.GetComponent<PlayerProgression>();
            if (prog != null && tier < prog.Tier)
            {
                float dist = Vector2.Distance(rb.position, (Vector2)playerTransform.position);
                if (dist < TileProgression.InfluenceRadius(tier))
                {
                    Vector2 away = (rb.position - (Vector2)playerTransform.position).normalized;
                    vel += away * baseSpeed * 0.8f;
                }
            }
        }

        // Vertical wall bounce
        float nextY = rb.position.y + vel.y * Time.fixedDeltaTime;
        if (nextY < ArenaState.MinY || nextY > ArenaState.MaxY)
        {
            vertDir = -vertDir;
            vel.y   = -vel.y;
        }

        rb.linearVelocity = vel;

        wanderTimer -= Time.fixedDeltaTime;
        if (wanderTimer <= 0f)
        {
            vertDir     = Random.Range(-1f, 1f);
            wanderTimer = Random.Range(1.5f, 4f);
        }
    }
}
