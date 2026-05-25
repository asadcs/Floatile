using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class DynamicWall : MonoBehaviour
{
    public enum Side { Left, Right, Top, Bottom }
    [SerializeField] public Side side;

    BoxCollider2D col;

    void Awake() => col = GetComponent<BoxCollider2D>();

    void LateUpdate()
    {
        float hw = ArenaState.HalfW;
        float hh = ArenaState.HalfH;

        switch (side)
        {
            case Side.Left:
                transform.position = new Vector3(-hw, 0f, 0f);
                col.size = new Vector2(0.1f, hh * 2f);
                break;
            case Side.Right:
                transform.position = new Vector3(hw, 0f, 0f);
                col.size = new Vector2(0.1f, hh * 2f);
                break;
            case Side.Bottom:
                transform.position = new Vector3(0f, -hh, 0f);
                col.size = new Vector2(hw * 2f, 0.1f);
                break;
            case Side.Top:
                transform.position = new Vector3(0f, hh, 0f);
                col.size = new Vector2(hw * 2f, 0.1f);
                break;
        }
    }
}
