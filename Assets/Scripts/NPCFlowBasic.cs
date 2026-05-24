using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class NPCFlowBasic : MonoBehaviour
{
    public event Action<NPCFlowBasic> Destroyed;

    [SerializeField] private float speed = 2f;
    [SerializeField] private float verticalWander = 0.3f;
    [SerializeField] private float bottomY = 0f;
    [SerializeField] private float topY = 60f;
    [SerializeField] private float rightCameraPadding = 13.5f;

    private Rigidbody2D rb;
    private bool isQuitting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 0.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void OnEnable()
    {
        rb.linearVelocity = new Vector2(speed, UnityEngine.Random.Range(-verticalWander, verticalWander));
    }

    private void FixedUpdate()
    {
        Vector2 velocity = rb.linearVelocity;

        if (transform.position.y < bottomY && velocity.y < 0f)
        {
            velocity.y = Mathf.Abs(velocity.y);
        }
        else if (transform.position.y > topY && velocity.y > 0f)
        {
            velocity.y = -Mathf.Abs(velocity.y);
        }

        velocity.x = speed;
        rb.linearVelocity = velocity;

        Camera mainCamera = Camera.main;
        float exitX = mainCamera == null ? 41f : mainCamera.transform.position.x + rightCameraPadding;

        if (transform.position.x > exitX)
        {
            Destroy(gameObject);
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (!isQuitting)
        {
            Destroyed?.Invoke(this);
        }
    }
}
