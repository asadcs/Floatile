using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using TMPro;

public static class FloatileSceneSetup
{
    // Sky: matches Floatile mockup
    static readonly Color SKY_COLOR    = new Color(0.773f, 0.910f, 0.973f); // #C5E8F7
    // Hero: soft red (Coral Candy)
    static readonly Color HERO_COLOR   = new Color(1f, 0.467f, 0.467f);     // #FF7777
    // NPC colors matching tier table
    static readonly Color[] NPC_COLORS =
    {
        new Color(0.69f, 0.83f, 0.95f), // tier 2  #B0D4F1
        new Color(0.42f, 0.75f, 0.42f), // tier 4  #6BBF6B
        new Color(0.30f, 0.72f, 0.72f), // tier 8  #4DB8B8
        new Color(0.88f, 0.36f, 0.36f), // tier 16 #E05C5C
        new Color(0.94f, 0.57f, 0.23f), // tier 32 #F0923A
    };

    // ── Sprint 1 Setup ────────────────────────────────────────────────────────

    const string SCENE_PATH = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Floatile/Setup Sprint 2 Scene (Full Reset)")]
    public static void SetupSprint2() => SetupSprint1();

    [MenuItem("Floatile/Setup Sprint 1 Scene (Full Reset)")]
    public static void SetupSprint1()
    {
        // Explicitly open the target scene so saves always hit the right file
        var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        Sprite tileSprite = LoadTileSprite();
        Sprite bgSprite   = LoadBgSprite();

        SetupCamera();
        ClearSprint1Objects();
        CreateBackground(bgSprite);
        SetupBloom();
        CreateMusic();
        CreateWalls();
        CreateGameManager();
        GameObject player        = CreatePlayerSprint1(tileSprite);
        GameObject npcPrefab     = CreateNpcDriftPrefab(tileSprite);
        GameObject specialPrefab = CreateSpecialTilePrefab(tileSprite);
        CreateArenaSpawnerSprint1(npcPrefab, specialPrefab, tileSprite);
        CreateUIManager(player);

        if (Camera.main != null) EditorUtility.SetDirty(Camera.main.gameObject);

        // Explicit save by path — reliable in both interactive and batch contexts
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log(saved
            ? "Floatile Sprint 2 fixed-screen scene configured and saved. Press Play."
            : "WARNING: Scene save returned false — save manually via File → Save.");
        Selection.activeGameObject = player;
    }

    static void ClearSprint1Objects()
    {
        string[] names = { "Wall_Left","Wall_Right","Wall_Bottom","Wall_Top",
                           "Player","NPC_Prefab","ArenaSpawner",
                           "ArenaBackground","GlobalVolume","MusicManager",
                           "GameManager","UIManager" };
        foreach (string n in names)
        {
            GameObject old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }
    }

    static void CreateGameManager()
    {
        GameObject go = new("GameManager");
        go.AddComponent<GameManager>();
        EditorUtility.SetDirty(go);
    }

    static GameObject CreatePlayerSprint1(Sprite sprite)
    {
        GameObject p = new("Player");
        p.tag = "Player";
        p.transform.position = Vector3.zero;

        var sr          = p.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.color        = TierColorTable.ForTier(2);
        sr.sortingOrder = 1;

        var rb                      = p.AddComponent<Rigidbody2D>();
        rb.bodyType                 = RigidbodyType2D.Kinematic;
        rb.gravityScale             = 0f;
        rb.constraints              = RigidbodyConstraints2D.FreezeRotation;
        rb.useFullKinematicContacts = true;

        var col       = p.AddComponent<BoxCollider2D>();
        col.size      = Vector2.one;
        col.isTrigger = true;

        var tv = p.AddComponent<TileVisual>();
        SetTileVisual(tv, sprite, TierColorTable.ForTier(2), "2");
        p.transform.localScale = Vector3.one * TileVisual.TierScale(2);

        p.AddComponent<PlayerDrift>();
        p.AddComponent<PlayerProgression>();
        p.AddComponent<EatSystem>();

        EditorUtility.SetDirty(p);
        Debug.Log($"[Sprint1 Setup] Player created — isTrigger=true, PlayerProgression+EatSystem added.");
        return p;
    }

    static GameObject CreateNpcDriftPrefab(Sprite sprite)
    {
        string prefabPath = "Assets/Prefabs/NPC_Drift.prefab";
        if (!Directory.Exists(Application.dataPath + "/Prefabs"))
            Directory.CreateDirectory(Application.dataPath + "/Prefabs");

        GameObject npc = new("NPC_Drift");

        var sr = npc.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color  = TierColorTable.ForTier(2);
        sr.sortingOrder = 1;

        var rb = npc.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        var col       = npc.AddComponent<BoxCollider2D>();
        col.size      = Vector2.one;
        col.isTrigger = false;

        npc.AddComponent<TileVisual>();
        npc.AddComponent<NPCDrift>();

        AssetDatabase.DeleteAsset(prefabPath);
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(npc, prefabPath);
        Object.DestroyImmediate(npc);
        AssetDatabase.Refresh();
        Debug.Log("NPC_Drift prefab saved: " + prefabPath);
        return prefabAsset;
    }

    static GameObject CreateSpecialTilePrefab(Sprite sprite)
    {
        string prefabPath = "Assets/Prefabs/SpecialTile.prefab";

        GameObject st = new("SpecialTile");

        var sr = st.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 2;

        st.AddComponent<Rigidbody2D>();
        var col = st.AddComponent<BoxCollider2D>();
        col.size      = Vector2.one * 0.9f;
        col.isTrigger = true;

        st.AddComponent<TileVisual>();
        st.AddComponent<SpecialTile>();

        AssetDatabase.DeleteAsset(prefabPath);
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(st, prefabPath);
        Object.DestroyImmediate(st);
        AssetDatabase.Refresh();
        Debug.Log("SpecialTile prefab saved: " + prefabPath);
        return prefabAsset;
    }

    static void CreateArenaSpawnerSprint1(GameObject npcPrefab, GameObject specialPrefab, Sprite sprite)
    {
        GameObject go = new("ArenaSpawner");
        var spawner = go.AddComponent<ArenaSpawner>();

        SerializedObject so = new(spawner);
        so.FindProperty("npcPrefab").objectReferenceValue      = npcPrefab;
        so.FindProperty("specialTilePrefab").objectReferenceValue = specialPrefab;
        so.FindProperty("tileSprite").objectReferenceValue     = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawner);
    }

    static void CreateUIManager(GameObject player)
    {
        GameObject go = new("UIManager");
        var ui = go.AddComponent<UIManager>();

        var prog  = player.GetComponent<PlayerProgression>();
        var drift = player.GetComponent<PlayerDrift>();

        SerializedObject so = new(ui);
        so.FindProperty("playerProg").objectReferenceValue      = prog;
        so.FindProperty("playerDrift").objectReferenceValue     = drift;
        so.FindProperty("playerTransform").objectReferenceValue = player.transform;
        so.FindProperty("mainCam").objectReferenceValue         = Camera.main;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
    }

    // ── Sprint 0 Setup ────────────────────────────────────────────────────────

    [MenuItem("Floatile/Setup Sprint 0 Scene (Full Reset)")]
    public static void SetupScene()
    {
        ImportTMPEssentials();

        Sprite tileSprite = LoadTileSprite();
        Sprite bgSprite   = LoadBgSprite();

        SetupCamera();
        ClearOldSceneObjects();
        CreateBackground(bgSprite);
        SetupBloom();
        CreateMusic();
        CreateWalls();
        GameObject player = CreatePlayer(tileSprite);
        GameObject npcPrefab = CreateNpcPrefab(tileSprite);
        CreateArenaSpawner(npcPrefab, tileSprite);
        WireCameraFollow(player);

        EditorUtility.SetDirty(Camera.main.gameObject);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Floatile Sprint 0 scene fully configured and saved. Press Play.");
        Selection.activeGameObject = player;
    }

    static void ImportTMPEssentials()
    {
        string tmpPath = Application.dataPath + "/TextMesh Pro";
        if (!Directory.Exists(tmpPath))
        {
            Debug.Log("Importing TMP Essentials...");
            AssetDatabase.ImportPackage(
                EditorApplication.applicationContentsPath +
                "/Resources/PackageManager/BuiltInPackages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage",
                false);
        }
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // Create a Main Camera if the scene doesn't have one
            GameObject camGo = new("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            Debug.Log("[Setup] No Main Camera found — created one.");
        }

        cam.orthographic     = true;
        cam.orthographicSize = 7f;
        cam.backgroundColor  = Color.black;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null) Object.DestroyImmediate(follow);
    }

    static void WireCameraFollow(GameObject player)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        CameraFollow cf = cam.GetComponent<CameraFollow>();
        if (cf != null) cf.SetPlayer(player.transform);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    static void ClearOldSceneObjects()
    {
        string[] names = { "Wall_Left","Wall_Right","Wall_Bottom","Wall_Top",
                           "Player","NPC_Prefab","ArenaSpawner",
                           "ArenaBackground","GlobalVolume","MusicManager" };
        foreach (string n in names)
        {
            GameObject old = GameObject.Find(n);
            if (old != null)
            {
                Debug.Log($"[Setup] Destroyed old '{n}' (id={old.GetInstanceID()})");
                Object.DestroyImmediate(old);
            }
            else
            {
                Debug.Log($"[Setup] No existing '{n}' found — skipping.");
            }
        }
    }

    // ── Music ─────────────────────────────────────────────────────────────────

    static void CreateMusic()
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bg_music.mp3");

        GameObject go = new("MusicManager");
        var mm = go.AddComponent<MusicManager>();

        SerializedObject so = new(mm);
        so.FindProperty("bgMusic").objectReferenceValue = clip;
        so.FindProperty("volume").floatValue = 0.35f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
        Debug.Log(clip != null ? "Music wired: bg_music.mp3" : "WARNING: bg_music.mp3 not found in Assets/Audio/");
    }

    // ── Background ────────────────────────────────────────────────────────────

    static void CreateBackground(Sprite bgSprite)
    {
        GameObject bg = new("ArenaBackground");

        bg.transform.position = new Vector3(0f, 0f, 1f);

        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite       = bgSprite;
        sr.sortingOrder = -10;

        if (bgSprite != null)
        {
            const float arenaW = 25.5f;
            const float arenaH = 14.5f;
            float texW  = bgSprite.bounds.size.x;
            float texH  = bgSprite.bounds.size.y;
            float scaleX = arenaW / texW;
            float scaleY = arenaH / texH;
            bg.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        EditorUtility.SetDirty(bg);
        Debug.Log("ArenaBackground placed in world space.");
    }

    static void SetupBloom()
    {
        GameObject volGO = new("GlobalVolume");
        var vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Save the profile asset so Unity can serialize it
        string profileDir  = "Assets/Settings";
        string profilePath = profileDir + "/FloatilePostProcess.asset";
        if (!Directory.Exists(Application.dataPath + "/Settings"))
            Directory.CreateDirectory(Application.dataPath + "/Settings");
        AssetDatabase.DeleteAsset(profilePath);
        AssetDatabase.CreateAsset(profile, profilePath);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.4f);
        bloom.threshold.Override(0.8f);
        bloom.scatter.Override(0.7f);

        vol.sharedProfile = profile;
        EditorUtility.SetDirty(volGO);
        AssetDatabase.SaveAssets();
        Debug.Log("URP Bloom volume created.");
    }

    // ── Walls ─────────────────────────────────────────────────────────────────

    static void CreateWalls()
    {
        MakeWall("Wall_Left",   new Vector3(-12.5f,  0f, 0f), new Vector2(0.1f, 14f));
        MakeWall("Wall_Right",  new Vector3( 12.5f,  0f, 0f), new Vector2(0.1f, 14f));
        MakeWall("Wall_Bottom", new Vector3(  0f,   -7f, 0f), new Vector2(25f,  0.1f));
        MakeWall("Wall_Top",    new Vector3(  0f,    7f, 0f), new Vector2(25f,  0.1f));
    }

    static void MakeWall(string name, Vector3 pos, Vector2 size)
    {
        GameObject w = new(name);
        w.transform.position = pos;
        var col = w.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
    }

    // ── Player ────────────────────────────────────────────────────────────────

    static GameObject CreatePlayer(Sprite sprite)
    {
        GameObject p = new("Player");
        p.tag = "Player";
        p.transform.position = new Vector3(0f, 30f, 0f);

        var sr  = p.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.color        = HERO_COLOR; // set directly on SR — survives Play mode entry
        sr.sortingOrder = 1;

        // Kinematic Rigidbody2D: player drives its own movement, still collides with NPCs
        var rb          = p.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        var col         = p.AddComponent<BoxCollider2D>();
        col.size        = Vector2.one * 0.9f;
        col.isTrigger   = false;

        var tv = p.AddComponent<TileVisual>();
        SetTileVisual(tv, sprite, HERO_COLOR, "2");

        p.transform.localScale = Vector3.one * TileVisual.TierScale(2);

        p.AddComponent<PlayerDrift>();
        p.AddComponent<HeroColor>();

        // ── Diagnostics ──
        var rbCheck  = p.GetComponent<Rigidbody2D>();
        var colCheck = p.GetComponent<BoxCollider2D>();
        Debug.Log($"[Setup] Player created id={p.GetInstanceID()} " +
                  $"tag={p.tag} " +
                  $"hasRB={rbCheck != null} " +
                  $"isTrigger={colCheck?.isTrigger} " +
                  $"scene={UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path}");

        return p;
    }

    static void SetTileVisual(TileVisual tv, Sprite sprite, Color color, string label)
    {
        SerializedObject so = new(tv);
        so.FindProperty("tileSprite").objectReferenceValue = sprite;
        so.FindProperty("tileColor").colorValue            = color;
        so.FindProperty("tierLabel").stringValue           = label;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── NPC Prefab ────────────────────────────────────────────────────────────

    static GameObject CreateNpcPrefab(Sprite sprite)
    {
        string prefabDir  = "Assets/Prefabs";
        string prefabPath = prefabDir + "/NPC.prefab";

        if (!Directory.Exists(Application.dataPath + "/Prefabs"))
            Directory.CreateDirectory(Application.dataPath + "/Prefabs");

        // Build the NPC object in scene temporarily
        GameObject npc = new("NPC");

        var sr = npc.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color  = NPC_COLORS[0];
        sr.sortingOrder = 1;

        var rb = npc.AddComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.linearDamping  = 0.5f;
        rb.constraints    = RigidbodyConstraints2D.FreezeRotation;

        var col  = npc.AddComponent<BoxCollider2D>();
        col.size = Vector2.one * 0.9f;

        var tv = npc.AddComponent<TileVisual>();
        SetTileVisual(tv, sprite, NPC_COLORS[0], "2");
        npc.AddComponent<NPCFlowBasic>();

        // Save as prefab then remove from scene
        AssetDatabase.DeleteAsset(prefabPath);
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(npc, prefabPath);
        Object.DestroyImmediate(npc);

        AssetDatabase.Refresh();
        Debug.Log("NPC prefab saved to " + prefabPath);
        return prefabAsset;
    }

    // ── Arena Spawner ─────────────────────────────────────────────────────────

    static void CreateArenaSpawner(GameObject npcPrefab, Sprite sprite)
    {
        GameObject go = new("ArenaSpawner");
        var spawner = go.AddComponent<ArenaSpawnerBasic>();

        // Load NPC prefab component from saved asset path (most reliable reference)
        var npcComp = AssetDatabase.LoadAssetAtPath<NPCFlowBasic>("Assets/Prefabs/NPC.prefab");

        SerializedObject so = new(spawner);
        so.FindProperty("npcPrefab").objectReferenceValue  = npcComp;
        so.FindProperty("tileSprite").objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawner);
        Debug.Log(npcComp != null
            ? "ArenaSpawner wired: NPC prefab assigned."
            : "WARNING: NPC prefab not found at Assets/Prefabs/NPC.prefab");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Sprite LoadTileSprite()
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tile.png");
        if (s == null)
            Debug.LogWarning("tile.png not found at Assets/Sprites/tile.png — assign sprites manually after setup.");
        return s;
    }

    static Sprite LoadBgSprite()
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/arena_bg.png");
        if (s == null)
            Debug.LogWarning("arena_bg.png not found — background will be black until you reimport.");
        return s;
    }
}
