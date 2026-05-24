using UnityEngine;

public sealed class PlayerDrift : MonoBehaviour
{
    [SerializeField] private float speed = 20f;

    private const float MIN_X = -40f;
    private const float MAX_X =  40f;
    private const float MIN_Y =   0f;
    private const float MAX_Y =  60f;

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

        // Solid collider so NPCs bounce off the player
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = false;

        Debug.Log($"[PlayerDrift.Awake] kinematic RB ready. isTrigger={col?.isTrigger}");
    }

    private void Start()
    {
        rb.position = new Vector2(0f, 30f);
        mainCam     = Camera.main;
    }

    private void FixedUpdate()
    {
        Vector2 dir = KeyboardDir();
        if (dir.sqrMagnitude < 0.01f)
            dir = MouseDir();

        if (dir.sqrMagnitude > 0.01f)
        {
            Vector2 next = rb.position + dir.normalized * speed * Time.fixedDeltaTime;
            next.x = Mathf.Clamp(next.x, MIN_X, MAX_X);
            next.y = Mathf.Clamp(next.y, MIN_Y, MAX_Y);
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
