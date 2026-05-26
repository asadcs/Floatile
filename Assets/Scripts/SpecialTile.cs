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
    Transform   _player;

    static readonly Color WHITE_TILE = new(1f, 1f, 1f, 1f);
    static readonly Color BLACK_TILE = new(0.102f, 0.102f, 0.102f, 1f);
    static readonly Color TEXT_DARK  = new(0.1f, 0.1f, 0.1f, 1f);
    static readonly Color TEXT_LIGHT = new(0.95f, 0.95f, 0.95f, 1f);

    public OperatorType Operator => operatorType;

    // Called by ArenaSpawner after instantiation.
    // playerTier: used to size the tile at 2× the player's current physical size.
    // customSprite: golden or hell artwork — rendered at true color (no tint).
    public void Init(OperatorType op, TileKind k, Sprite tileSprite, long playerTier, Sprite customSprite = null)
    {
        operatorType = op;
        kind         = k;

        Color bgHint  = k == TileKind.White ? WHITE_TILE : BLACK_TILE;
        bool  hasArt  = customSprite != null;
        Sprite visual = hasArt ? customSprite : tileSprite;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite       = visual;
            sr.color        = hasArt ? Color.white : bgHint;
            sr.sortingOrder = 1;
        }

        var tv = GetComponent<TileVisual>();
        if (tv != null)
            tv.SetVisual(visual, bgHint, SymbolFor(op));

        // Always 2× the player's current tile size so both kinds feel significant.
        float scale = 2f * TileProgression.PhysicalSize(playerTier);
        transform.localScale = Vector3.one * scale;

        Destroy(gameObject, k == TileKind.Black ? TileProgression.BlackTileLifespan : TileProgression.WhiteTileLifespan);
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

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) _player = p.transform;
    }

    void FixedUpdate()
    {
        if (_player != null)
        {
            if (kind == TileKind.Black)
            {
                // Shark: arc gradually toward player.
                Vector2 toward = ((Vector2)_player.position - rb.position).normalized;
                dir = Vector2.Lerp(dir, toward, TileProgression.BlackTileChaseStrength * Time.fixedDeltaTime).normalized;
            }
            else if (kind == TileKind.White)
            {
                // Golden tile: flee when player enters detection range — elusive, must be chased.
                float dist = Vector2.Distance(rb.position, (Vector2)_player.position);
                if (dist < TileProgression.WhiteTileDetectRange)
                {
                    Vector2 away = (rb.position - (Vector2)_player.position).normalized;
                    dir = Vector2.Lerp(dir, away, TileProgression.WhiteTileFleeStrength * Time.fixedDeltaTime).normalized;
                }
            }
        }

        float speed = kind == TileKind.Black ? TileProgression.BlackTileChaseSpeed
                    : kind == TileKind.White  ? TileProgression.WhiteTileFleeSpeed
                    : TileProgression.SpecialDriftSpeed;
        Vector2 next = rb.position + dir * speed * Time.fixedDeltaTime;

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
