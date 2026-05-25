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
            tier = value;
            visual?.SetTier(tier);
            ApplySpeed();
        }
    }

    const float MIN_Y  =  0f;
    const float MAX_Y  = 60f;
    const float MAX_X  = 40f;
    const float WANDER = 0.3f;   // max vertical drift per second

    Rigidbody2D   rb;
    TileVisual    visual;
    Transform     playerTransform;
    float         baseSpeed;
    float         vertDir;
    float         wanderTimer;

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        visual = GetComponent<TileVisual>();

        rb.bodyType    = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        vertDir      = Random.Range(-1f, 1f);
        wanderTimer  = Random.Range(1.5f, 4f);

        visual.SetTier(tier);
        ApplySpeed();
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    void FixedUpdate()
    {
        // Destroy when tile exits the right edge — ArenaSpawner will respawn
        if (rb.position.x > MAX_X + 3f)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 vel = new(baseSpeed, vertDir * WANDER);

        // Proximity avoidance: flee if smaller than player and too close
        if (playerTransform != null)
        {
            var prog = playerTransform.GetComponent<PlayerProgression>();
            if (prog != null && tier < prog.Tier)
            {
                float dist = Vector2.Distance(rb.position, (Vector2)playerTransform.position);
                if (dist < 3f)
                {
                    Vector2 away = (rb.position - (Vector2)playerTransform.position).normalized;
                    vel += away * baseSpeed * 0.8f;
                }
            }
        }

        // Vertical wall bounce
        float nextY = rb.position.y + vel.y * Time.fixedDeltaTime;
        if (nextY < MIN_Y || nextY > MAX_Y)
        {
            vertDir = -vertDir;
            vel.y   = -vel.y;
        }

        rb.linearVelocity = vel;

        // Periodically change vertical wander direction
        wanderTimer -= Time.fixedDeltaTime;
        if (wanderTimer <= 0f)
        {
            vertDir     = Random.Range(-1f, 1f);
            wanderTimer = Random.Range(1.5f, 4f);
        }
    }

    void ApplySpeed()
    {
        if (tier <= 4)       baseSpeed = 1.5f;
        else if (tier <= 32) baseSpeed = 2.0f;
        else                 baseSpeed = 2.5f;
    }
}
