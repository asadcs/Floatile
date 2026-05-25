using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public sealed class UIManager : MonoBehaviour
{
    // Tier bar: 11 cells representing tiers 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048
    static readonly long[] TIER_BAR_VALUES = { 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };

    // ── Refs wired by FloatileSceneSetup ──────────────────────────────────────
    [SerializeField] private PlayerProgression playerProg;
    [SerializeField] private PlayerDrift       playerDrift;
    [SerializeField] private Transform         playerTransform;
    [SerializeField] private Camera            mainCam;

    // ── Runtime UI objects (created here) ────────────────────────────────────
    Canvas        rootCanvas;
    Image[]       tierCells;
    GameObject    instructionText;
    GameObject    youLabel;
    GameObject    dpad;
    Image[]       dotMarkers;   // mini-map dots

    // Mini-map dots pool size
    const int MINIMAP_DOTS = 20;

    // D-pad touch input exposed to PlayerDrift
    Vector2 dpadInput;
    bool    hasMovedOnce;
    bool    hasEatenOnce;

    void Awake()
    {
        BuildCanvas();
        BuildTierBar();
        BuildInstructionText();
        BuildYouLabel();
        BuildDpad();
        BuildMinimap();
        BuildButtons();
    }

    EatSystem eatSystem;
    Vector2   spawnPos;
    bool      positionRecorded;

    void Start()
    {
        if (playerProg != null)
            playerProg.OnTierChanged += OnTierChanged;

        if (playerTransform != null)
        {
            eatSystem = playerTransform.GetComponent<EatSystem>();
            if (eatSystem != null)
            {
                eatSystem.OnEat    += OnAnyEat;
                eatSystem.OnEvolve += OnAnyEat;
            }
            spawnPos         = playerTransform.position;
            positionRecorded = true;
        }
    }

    void OnDestroy()
    {
        if (playerProg != null)
            playerProg.OnTierChanged -= OnTierChanged;

        if (eatSystem != null)
        {
            eatSystem.OnEat    -= OnAnyEat;
            eatSystem.OnEvolve -= OnAnyEat;
        }
    }

    void OnAnyEat(long _) => NotifyFirstEat();

    // Called by PlayerDrift on first move
    public void NotifyFirstMove()
    {
        if (hasMovedOnce) return;
        hasMovedOnce = true;
        if (youLabel != null) youLabel.SetActive(true);
    }

    // Called by EatSystem on first eat
    public void NotifyFirstEat()
    {
        if (hasEatenOnce) return;
        hasEatenOnce = true;
        if (instructionText != null)
            StartCoroutine(FadeOut(instructionText));
    }

    // D-pad input read by PlayerDrift
    public Vector2 DpadInput => dpadInput;

    // ── Tier bar ──────────────────────────────────────────────────────────────

    void BuildTierBar()
    {
        GameObject bar = new("TierBar");
        bar.transform.SetParent(rootCanvas.transform, false);

        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        rt.sizeDelta        = new Vector2(550f, 44f);

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing           = 4f;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment    = TextAnchor.MiddleCenter;

        tierCells = new Image[TIER_BAR_VALUES.Length];
        for (int i = 0; i < TIER_BAR_VALUES.Length; i++)
        {
            GameObject cell = new($"TierCell_{TIER_BAR_VALUES[i]}");
            cell.transform.SetParent(bar.transform, false);

            var cellRt = cell.AddComponent<RectTransform>();
            cellRt.sizeDelta = new Vector2(44f, 44f);

            var img = cell.AddComponent<Image>();
            img.color = TierColorTable.ForTier(TIER_BAR_VALUES[i]);
            tierCells[i] = img;

            // Tier number label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(cell.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;

            var txt = labelGo.AddComponent<Text>();
            txt.text      = TileVisual.FormatNumber(TIER_BAR_VALUES[i]);
            txt.fontSize  = 11;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        RefreshTierBar(2L);
    }

    void OnTierChanged(long tier) => RefreshTierBar(tier);

    void RefreshTierBar(long tier)
    {
        if (tierCells == null) return;
        for (int i = 0; i < tierCells.Length; i++)
        {
            bool active = TIER_BAR_VALUES[i] == tier;
            float s = active ? 1.2f : 1f;
            tierCells[i].transform.localScale = Vector3.one * s;

            // White outline on active cell
            var outline = tierCells[i].GetComponent<Outline>();
            if (active && outline == null)
            {
                outline = tierCells[i].gameObject.AddComponent<Outline>();
                outline.effectColor    = Color.white;
                outline.effectDistance = new Vector2(2f, -2f);
            }
            else if (!active && outline != null)
            {
                Destroy(outline);
            }
        }
    }

    // ── Instruction text ──────────────────────────────────────────────────────

    void BuildInstructionText()
    {
        instructionText = new("InstructionText");
        instructionText.transform.SetParent(rootCanvas.transform, false);

        var rt = instructionText.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -70f);
        rt.sizeDelta        = new Vector2(400f, 60f);

        var txt = instructionText.AddComponent<Text>();
        txt.text      = "EAT SMALLER TILES\nFIND SAME NUMBER TO EVOLVE!";
        txt.fontSize  = 18;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = new Color(1f, 1f, 1f, 0.85f);
        txt.alignment = TextAnchor.UpperLeft;

        var shadow = instructionText.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    // ── YOU label (on player in world space → converted to screen) ────────────

    void BuildYouLabel()
    {
        youLabel = new("YouLabel");
        youLabel.transform.SetParent(rootCanvas.transform, false);
        youLabel.SetActive(false);   // shown on first move

        var rt = youLabel.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60f, 24f);
        rt.pivot     = new Vector2(0.5f, 0f);

        var txt = youLabel.AddComponent<Text>();
        txt.text      = "YOU";
        txt.fontSize  = 14;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        var shadow = youLabel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    // ── D-pad ─────────────────────────────────────────────────────────────────

    void BuildDpad()
    {
        dpad = new("Dpad");
        dpad.transform.SetParent(rootCanvas.transform, false);

        var rt = dpad.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(30f, 30f);
        rt.sizeDelta        = new Vector2(120f, 120f);

        MakeDpadButton(dpad, "Up",    new Vector2(40f, 80f),  Vector2.up);
        MakeDpadButton(dpad, "Down",  new Vector2(40f, 0f),   Vector2.down);
        MakeDpadButton(dpad, "Left",  new Vector2(0f, 40f),   Vector2.left);
        MakeDpadButton(dpad, "Right", new Vector2(80f, 40f),  Vector2.right);
    }

    void MakeDpadButton(GameObject parent, string label, Vector2 pos, Vector2 dir)
    {
        GameObject btn = new($"Dpad_{label}");
        btn.transform.SetParent(parent.transform, false);

        var rt = btn.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(38f, 38f);

        var img = btn.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.35f);

        var btnComp = btn.AddComponent<Button>();

        // Hold-to-move via EventTrigger
        var et = btn.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var downEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
        downEntry.callback.AddListener(_ => dpadInput += dir);
        et.triggers.Add(downEntry);

        var upEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
        upEntry.callback.AddListener(_ => dpadInput -= dir);
        et.triggers.Add(upEntry);

        // Arrow text label
        var txtGo = new GameObject("Arrow");
        txtGo.transform.SetParent(btn.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.text      = label == "Up" ? "▲" : label == "Down" ? "▼" : label == "Left" ? "◀" : "▶";
        txt.fontSize  = 20;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    // ── Mini-map ──────────────────────────────────────────────────────────────

    void BuildMinimap()
    {
        GameObject map = new("Minimap");
        map.transform.SetParent(rootCanvas.transform, false);

        var rt = map.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-10f, 10f);
        rt.sizeDelta        = new Vector2(80f, 60f);

        // Background
        var bg = map.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.3f);

        dotMarkers = new Image[MINIMAP_DOTS];
        for (int i = 0; i < MINIMAP_DOTS; i++)
        {
            GameObject dot = new($"Dot_{i}");
            dot.transform.SetParent(map.transform, false);
            var dotRt = dot.AddComponent<RectTransform>();
            dotRt.sizeDelta = new Vector2(4f, 4f);
            var img = dot.AddComponent<Image>();
            img.color = Color.white;
            dot.SetActive(false);
            dotMarkers[i] = img;
        }
    }

    void UpdateMinimap()
    {
        if (dotMarkers == null) return;
        var allNPCs = FindObjectsByType<NPCDrift>(FindObjectsSortMode.None);
        for (int i = 0; i < dotMarkers.Length; i++)
        {
            if (i >= allNPCs.Length) { dotMarkers[i].gameObject.SetActive(false); continue; }
            dotMarkers[i].gameObject.SetActive(true);
            // World pos → minimap pos (80×60 arena → 80×60 px minimap)
            Vector2 wpos = allNPCs[i].transform.position;
            float mx = (wpos.x + 40f) / 80f * 80f;
            float my = wpos.y / 60f * 60f;
            ((RectTransform)dotMarkers[i].transform).anchoredPosition = new Vector2(mx, my);
            dotMarkers[i].color = TierColorTable.ForTier(allNPCs[i].Tier);
        }
    }

    // ── Top buttons ───────────────────────────────────────────────────────────

    void BuildButtons()
    {
        // Pause button top-right
        MakeTopButton("Pause", new Vector2(-10f, -10f), new Vector2(1f, 1f), "II",
            () => GameManager.Instance?.Pause());
    }

    void MakeTopButton(string name, Vector2 pos, Vector2 anchor, string symbol, System.Action onClick)
    {
        GameObject btn = new(name);
        btn.transform.SetParent(rootCanvas.transform, false);

        var rt = btn.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot     = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(44f, 44f);

        var img = btn.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.4f);

        var btnComp = btn.AddComponent<Button>();
        btnComp.onClick.AddListener(() => onClick());

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(btn.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.text      = symbol;
        txt.fontSize  = 18;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    // ── Canvas ────────────────────────────────────────────────────────────────

    void BuildCanvas()
    {
        GameObject cgo = new("UICanvas");
        cgo.transform.SetParent(transform, false);

        rootCanvas = cgo.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 10;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;

        cgo.AddComponent<GraphicRaycaster>();

        // EventSystem required for button clicks
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    // ── Per-frame updates ─────────────────────────────────────────────────────

    void LateUpdate()
    {
        // Push D-pad state to PlayerDrift every frame
        if (playerDrift != null)
            playerDrift.ExternalInput = dpadInput;

        if (!hasMovedOnce && positionRecorded && playerTransform != null)
        {
            if (Vector2.Distance(playerTransform.position, spawnPos) > 0.3f)
                NotifyFirstMove();
        }
        UpdateYouLabelPos();
        UpdateMinimap();
    }

    void UpdateYouLabelPos()
    {
        if (youLabel == null || !youLabel.activeSelf) return;
        if (playerTransform == null || mainCam == null) return;

        Vector3 screenPos = mainCam.WorldToScreenPoint(playerTransform.position + Vector3.up * 0.8f);
        if (screenPos.z < 0) { youLabel.SetActive(false); return; }

        var rt = (RectTransform)youLabel.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)rootCanvas.transform,
            screenPos, null, out Vector2 local);
        rt.anchoredPosition = local;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    IEnumerator FadeOut(GameObject target)
    {
        var texts = target.GetComponentsInChildren<Text>();
        float elapsed = 0f;
        float duration = 1.5f;
        while (elapsed < duration)
        {
            float a = Mathf.Lerp(0.85f, 0f, elapsed / duration);
            foreach (var t in texts) { var c = t.color; c.a = a; t.color = c; }
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.SetActive(false);
    }
}
