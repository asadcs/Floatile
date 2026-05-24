using UnityEngine;

public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float followSpeed   = 6f;
    [SerializeField] private Vector2 arenaCenter = new(0f, 30f);
    [SerializeField] private Vector2 arenaSize   = new(80f, 60f);
    [SerializeField] private float orthoSize     = 7f;
    [SerializeField] private float aspect        = 16f / 9f;

    // Dead zone in world units — player moves freely within this box before camera follows
    [SerializeField] private float deadZoneX = 3f;
    [SerializeField] private float deadZoneY = 2f;

    private void Start()
    {
        if (player == null)
        {
            PlayerDrift drift = FindObjectOfType<PlayerDrift>();
            if (drift != null) player = drift.transform;
        }
        if (player != null)
            transform.position = ClampedCamPos(player.position);
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector2 p      = player.position;
        Vector2 c      = transform.position;
        Vector2 target = c;

        // Only pull camera when player exits the dead zone rectangle
        float dx = p.x - c.x;
        float dy = p.y - c.y;

        if (dx >  deadZoneX) target.x = p.x - deadZoneX;
        if (dx < -deadZoneX) target.x = p.x + deadZoneX;
        if (dy >  deadZoneY) target.y = p.y - deadZoneY;
        if (dy < -deadZoneY) target.y = p.y + deadZoneY;

        Vector3 next = ClampedCamPos(new Vector3(target.x, target.y, 0f));
        transform.position = Vector3.Lerp(transform.position, next, followSpeed * Time.deltaTime);
    }

    private Vector3 ClampedCamPos(Vector3 pos)
    {
        float hw   = orthoSize * aspect;
        float hh   = orthoSize;
        float minX = arenaCenter.x - arenaSize.x * 0.5f + hw;
        float maxX = arenaCenter.x + arenaSize.x * 0.5f - hw;
        float minY = arenaCenter.y - arenaSize.y * 0.5f + hh;
        float maxY = arenaCenter.y + arenaSize.y * 0.5f - hh;

        return new Vector3(
            Mathf.Clamp(pos.x, minX, maxX),
            Mathf.Clamp(pos.y, minY, maxY),
            -10f);
    }

    public void SetPlayer(Transform t) => player = t;
}
