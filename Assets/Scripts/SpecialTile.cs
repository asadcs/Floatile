using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class SpecialTile : MonoBehaviour
{
    public enum OperatorType
    {
        Multiply2, Multiply3, Square, Cube,
        CubeRoot, SquareRoot, DivideBy2
    }

    public enum TileKind { White, Black }

    [SerializeField] private OperatorType operatorType;
    [SerializeField] private TileKind     kind;


    Rigidbody2D rb;
    Vector2     dir;

    static readonly Color WHITE_TILE = new(1f, 1f, 1f, 1f);
    static readonly Color BLACK_TILE = new(0.102f, 0.102f, 0.102f, 1f);
    static readonly Color TEXT_DARK  = new(0.1f, 0.1f, 0.1f, 1f);
    static readonly Color TEXT_LIGHT = new(0.95f, 0.95f, 0.95f, 1f);

    public OperatorType Operator => operatorType;

    // Called by ArenaSpawner after instantiation
    public void Init(OperatorType op, TileKind k, Sprite tileSprite)
    {
        operatorType = op;
        kind         = k;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite       = tileSprite;
            sr.color        = k == TileKind.White ? WHITE_TILE : BLACK_TILE;
            sr.sortingOrder = 1;
        }

        // Display operator symbol on tile face
        var tv = GetComponent<TileVisual>();
        if (tv != null)
        {
            Color labelCol = k == TileKind.White ? TEXT_DARK : TEXT_LIGHT;
            tv.SetVisual(tileSprite, k == TileKind.White ? WHITE_TILE : BLACK_TILE, SymbolFor(op));
        }

        transform.localScale = Vector3.one * TileProgression.SpecialTileScale();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.size      = Vector2.one * 0.9f;
        col.isTrigger = true;

        dir = UnityEngine.Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
    }

    void FixedUpdate()
    {
        Vector2 next = rb.position + dir * TileProgression.SpecialDriftSpeed * Time.fixedDeltaTime;

        if (next.x <= ArenaState.MinX || next.x >= ArenaState.MaxX) { dir.x = -dir.x; next.x = Mathf.Clamp(next.x, ArenaState.MinX, ArenaState.MaxX); }
        if (next.y <= ArenaState.MinY || next.y >= ArenaState.MaxY) { dir.y = -dir.y; next.y = Mathf.Clamp(next.y, ArenaState.MinY, ArenaState.MaxY); }

        rb.MovePosition(next);
    }

    public static string SymbolFor(OperatorType op) => op switch
    {
        OperatorType.Multiply2  => "x2",
        OperatorType.Multiply3  => "x3",
        OperatorType.Square     => "n^2",
        OperatorType.Cube       => "n^3",
        OperatorType.CubeRoot   => "3Vn",
        OperatorType.SquareRoot => "Vn",
        OperatorType.DivideBy2  => "/2",
        _ => "?"
    };

    // Picks which black-tile operator to use based on n = log2(tier)
    public static OperatorType BlackOpFor(long playerTier)
    {
        int n = (int)Mathf.Round(Mathf.Log(playerTier, 2f));
        if (n % 3 == 0) return OperatorType.CubeRoot;
        if (n % 2 == 0) return OperatorType.SquareRoot;
        return OperatorType.DivideBy2;
    }
}
