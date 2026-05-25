using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class TileVisual : MonoBehaviour
{
    [SerializeField] private Sprite tileSprite;
    [SerializeField] private Color  tileColor  = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private string tierLabel  = "2";


    private SpriteRenderer spriteRenderer;
    private TextMesh       label;
    private TextMesh       labelShadow;
    private SpriteRenderer shadowRenderer;

    static readonly Color LABEL_COLOR  = new Color(1f, 0.97f, 0.93f, 1f);   // warm cream white
    static readonly Color SHADOW_COLOR = new Color(0f, 0f, 0f, 0.25f);       // soft dark shadow

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureLabels();
        ConfigureSprite();
        ConfigureLabels();
        ConfigureTileDropShadow();
        if (long.TryParse(tierLabel, out long t) && t >= 2)
            transform.localScale = Vector3.one * TierScale(t);
    }

    public void SetVisual(Sprite sprite, Color color, string text)
    {
        tileSprite = sprite;
        tileColor  = color;
        tierLabel  = text;
        if (spriteRenderer == null) return;
        ConfigureSprite();
        ConfigureLabels();
        ConfigureTileDropShadow();
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    private void EnsureLabels()
    {
        labelShadow = GetOrCreateTextMesh("LabelShadow");
        label       = GetOrCreateTextMesh("TierLabel");
    }

    private TextMesh GetOrCreateTextMesh(string childName)
    {
        Transform t = transform.Find(childName);
        if (t != null)
        {
            var existing = t.GetComponent<TextMesh>();
            if (existing != null) return existing;
        }
        GameObject obj = new(childName);
        obj.transform.SetParent(transform, false);
        return obj.AddComponent<TextMesh>();
    }

    private void ConfigureLabels()
    {
        float cs = CharSize(tierLabel.Length);

        ApplyTextMesh(labelShadow, tierLabel, SHADOW_COLOR, cs,
                      new Vector3(0.025f, -0.03f, -0.05f), sortOrder: 2);

        ApplyTextMesh(label, tierLabel, LABEL_COLOR, cs,
                      new Vector3(0f, 0f, -0.1f), sortOrder: 3);
    }

    // Fills ~60% of tile. fontSize=32 controls quality only; characterSize is the physical world size.
    static float CharSize(int digits)
    {
        if (digits <= 1) return 0.32f;
        if (digits <= 2) return 0.26f;
        if (digits <= 3) return 0.20f;
        if (digits <= 4) return 0.16f;
        return 0.12f;
    }

    private static void ApplyTextMesh(TextMesh tm, string text, Color color,
                                      float charSize, Vector3 localPos, int sortOrder)
    {
        tm.text          = text;
        tm.color         = color;
        tm.fontSize      = 32;           // controls mesh quality, not physical size
        tm.characterSize = charSize;
        tm.fontStyle     = FontStyle.Bold;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;

        tm.transform.localPosition = localPos;
        tm.transform.localScale    = Vector3.one;
        tm.GetComponent<Renderer>().sortingOrder = sortOrder;
    }

    // ── Sprite ────────────────────────────────────────────────────────────────

    private void ConfigureSprite()
    {
        if (tileSprite != null) spriteRenderer.sprite = tileSprite;
        if (tileColor.a > 0.01f) spriteRenderer.color = tileColor;
        spriteRenderer.sortingOrder = 1;

        if (shadowRenderer != null && spriteRenderer.sprite != null)
            shadowRenderer.sprite = spriteRenderer.sprite;
    }

    private void ConfigureTileDropShadow()
    {
        Transform existing   = transform.Find("DropShadow");
        GameObject shadowObj = existing != null ? existing.gameObject : new GameObject("DropShadow");
        shadowObj.transform.SetParent(transform, false);
        shadowObj.transform.localPosition = new Vector3(0.07f, -0.07f, 0.02f);

        var existingSr = shadowObj.GetComponent<SpriteRenderer>();
        shadowRenderer = (existingSr != null) ? existingSr : shadowObj.AddComponent<SpriteRenderer>();

        if (spriteRenderer.sprite != null) shadowRenderer.sprite = spriteRenderer.sprite;
        shadowRenderer.color        = new Color(0f, 0f, 0f, 0.22f);
        shadowRenderer.sortingOrder = 0;
    }

    // ── Tier scaling ──────────────────────────────────────────────────────────

    public static float TierScale(long tier) => TileProgression.PhysicalSize(tier);

    public void ApplyTierScale(long tier) =>
        transform.localScale = Vector3.one * TierScale(tier);

    // Full tier update: color + label + scale in one call
    public void SetTier(long tier)
    {
        Color  col   = TierColorTable.ForTier(tier);
        string text  = FormatNumber(tier);
        tierLabel    = text;
        tileColor    = col;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureLabels();
        ConfigureSprite();
        ConfigureLabels();
        transform.localScale = Vector3.one * TierScale(tier);
    }

    // ── Number formatting ─────────────────────────────────────────────────────

    public static string FormatNumber(long value)
    {
        if (value < 1_000L)                 return value.ToString();
        if (value < 1_000_000L)             return Suffix(value, 1_000L,                 "K");
        if (value < 1_000_000_000L)         return Suffix(value, 1_000_000L,             "M");
        if (value < 1_000_000_000_000L)     return Suffix(value, 1_000_000_000L,         "B");
        if (value < 1_000_000_000_000_000L) return Suffix(value, 1_000_000_000_000L,     "T");
        return                                     Suffix(value, 1_000_000_000_000_000L, "Qa");
    }

    static string Suffix(long value, long divisor, string suffix)
    {
        long num = (long)System.Math.Round((double)value / divisor);
        return num + suffix;
    }
}
