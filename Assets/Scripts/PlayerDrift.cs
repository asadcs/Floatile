using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(TileHitbox))]
public sealed class PlayerDrift : MonoBehaviour
{
    // Set by UIManager D-pad buttons; accumulated while buttons are held
    public Vector2 ExternalInput { get; set; }

    float speed = 20f;  // updated by TileProgression.PlayerSpeed() when tier changes

    private Rigidbody2D rb;
    private Camera      mainCam;
    PlayerProgression   progression;

    private void Awake()
    {
        gameObject.tag = "Player";

        // Kinematic RB: player drives movement, physics handles collisions
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Trigger so OnTriggerEnter2D fires for eat/evolve/penalty.
        var hitbox = GetComponent<TileHitbox>();
        if (hitbox == null) hitbox = gameObject.AddComponent<TileHitbox>();
        hitbox.Sync();

        Debug.Log($"[PlayerDrift.Awake] kinematic RB ready. isTrigger={hitbox.Collider?.isTrigger}");
    }

    private void Start()
    {
        rb.position = Vector2.zero;
        mainCam     = Camera.main;

        progression = GetComponent<PlayerProgression>();
        if (progression != null)
        {
            speed = TileProgression.PlayerSpeed(progression.Tier);
            progression.OnTierChanged += t => speed = TileProgression.PlayerSpeed(t);
        }
    }

    private void FixedUpdate()
    {
        Vector2 dir = KeyboardDir();
        if (dir.sqrMagnitude < 0.01f)
            dir = MouseDir();
        if (dir.sqrMagnitude < 0.01f)
            dir = ExternalInput.normalized;

        if (dir.sqrMagnitude > 0.01f)
        {
            Vector2 next = rb.position + dir.normalized * speed * Time.fixedDeltaTime;
            next.x = Mathf.Clamp(next.x, ArenaState.VisibleMinX, ArenaState.VisibleMaxX);
            next.y = Mathf.Clamp(next.y, ArenaState.VisibleMinY, ArenaState.VisibleMaxY);
            rb.MovePosition(next);
        }
    }

    private static Vector2 KeyboardDir()
    {
        float x = 0f, y = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y -= 1f;
        return new Vector2(x, y);
    }

    private Vector2 MouseDir()
    {
        if (mainCam == null) { mainCam = Camera.main; return Vector2.zero; }
        if (!Input.GetMouseButton(0)) return Vector2.zero;
        Vector2 world   = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = world - rb.position;
        return toMouse.sqrMagnitude > 0.25f ? toMouse.normalized : Vector2.zero;
    }
}
