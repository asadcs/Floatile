using UnityEngine;

public sealed class PlayerDrift : MonoBehaviour
{
    [SerializeField] private float speed = 20f;

    // Set by UIManager D-pad buttons; accumulated while buttons are held
    public Vector2 ExternalInput { get; set; }


    private Rigidbody2D rb;
    private Camera      mainCam;

    private void Awake()
    {
        gameObject.tag = "Player";

        // Kinematic RB: player drives movement, physics handles collisions
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Trigger so OnTriggerEnter2D fires for eat/evolve/penalty
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;

        Debug.Log($"[PlayerDrift.Awake] kinematic RB ready. isTrigger={col?.isTrigger}");
    }

    private void Start()
    {
        rb.position = Vector2.zero;
        mainCam     = Camera.main;
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
