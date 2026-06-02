using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TileHitbox : MonoBehaviour
{
    BoxCollider2D box;
    SpriteRenderer spriteRenderer;

    public BoxCollider2D Collider => box;
    public Bounds Bounds => box != null ? box.bounds : new Bounds(transform.position, Vector3.zero);

    void Awake() => Sync();
    void OnValidate() => Sync();

    public void Sync()
    {
        if (box == null) box = GetComponent<BoxCollider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (box == null || spriteRenderer == null) return;

        box.isTrigger = true;
        box.offset = Vector2.zero;

        Sprite sprite = spriteRenderer.sprite;
        box.size = sprite != null
            ? new Vector2(sprite.bounds.size.x, sprite.bounds.size.y) * TileProgression.HitboxVisualFraction
            : Vector2.one;
    }

    public bool Touches(TileHitbox other, float skin = 0f)
    {
        if (other == null) return false;
        return Touches(Bounds, other.Bounds, skin);
    }

    public static bool Touches(Bounds a, Bounds b, float skin = 0f)
    {
        return a.min.x <= b.max.x + skin &&
               a.max.x >= b.min.x - skin &&
               a.min.y <= b.max.y + skin &&
               a.max.y >= b.min.y - skin;
    }

    void OnDrawGizmos()
    {
        if (!TileProgression.DebugHitboxes) return;
        if (box == null) box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
    }
}
