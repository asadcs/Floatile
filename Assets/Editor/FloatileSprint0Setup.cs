using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FloatileSprint0Setup
{
    private const string TilePath = "Assets/Sprites/tile.png";
    private const string ScenePath = "Assets/Scenes/FloatileSprint0.unity";
    private const string PrefabPath = "Assets/Prefabs/NPCBasic.prefab";

    public static void SetupScene()
    {
        ConfigureTileImporter();

        Sprite tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TilePath);
        if (tileSprite == null)
        {
            throw new System.InvalidOperationException("Tile sprite was not imported as a Sprite: " + TilePath);
        }

        AssetDatabase.CreateFolder("Assets", "Prefabs");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera camera = CreateCamera();
        GameObject player = CreatePlayer(tileSprite);
        camera.GetComponent<CameraFollow>().SetPlayer(player.transform);

        NPCFlowBasic npcPrefab = CreateNpcPrefab(tileSprite);
        CreateSpawner(tileSprite, npcPrefab);
        CreateArenaWalls();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ConfigureTileImporter()
    {
        AssetDatabase.ImportAsset(TilePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(TilePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 256f;
        importer.SaveAndReimport();
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 23f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.529f, 0.808f, 0.922f);
        camera.orthographic = true;
        camera.orthographicSize = 7f;

        cameraObject.AddComponent<CameraFollow>();
        return camera;
    }

    private static GameObject CreatePlayer(Sprite tileSprite)
    {
        GameObject player = new("Player");
        player.transform.position = new Vector3(0f, 23f, 0f);
        player.transform.localScale = Vector3.one * 1.25f;

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = tileSprite;
        renderer.color = new Color(0.96f, 0.82f, 0.20f);

        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        TileVisual visual = player.AddComponent<TileVisual>();
        SetPrivateField(visual, "tileSprite", tileSprite);
        SetPrivateField(visual, "tileColor", new Color(0.96f, 0.82f, 0.20f));
        SetPrivateField(visual, "tierLabel", "64");

        player.AddComponent<PlayerDrift>();
        return player;
    }

    private static NPCFlowBasic CreateNpcPrefab(Sprite tileSprite)
    {
        GameObject npc = new("NPCBasic");
        npc.transform.localScale = Vector3.one;

        SpriteRenderer renderer = npc.AddComponent<SpriteRenderer>();
        renderer.sprite = tileSprite;
        renderer.color = new Color(0.69f, 0.83f, 0.95f);

        Rigidbody2D rb = npc.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 0.5f;

        BoxCollider2D collider = npc.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        TileVisual visual = npc.AddComponent<TileVisual>();
        SetPrivateField(visual, "tileSprite", tileSprite);
        SetPrivateField(visual, "tileColor", new Color(0.69f, 0.83f, 0.95f));
        SetPrivateField(visual, "tierLabel", "2");

        NPCFlowBasic flow = npc.AddComponent<NPCFlowBasic>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(npc, PrefabPath);
        Object.DestroyImmediate(npc);
        return prefab.GetComponent<NPCFlowBasic>();
    }

    private static void CreateSpawner(Sprite tileSprite, NPCFlowBasic npcPrefab)
    {
        GameObject spawner = new("Arena Spawner");
        ArenaSpawnerBasic arenaSpawner = spawner.AddComponent<ArenaSpawnerBasic>();
        SetPrivateField(arenaSpawner, "npcPrefab", npcPrefab);
        SetPrivateField(arenaSpawner, "tileSprite", tileSprite);
    }

    private static void CreateArenaWalls()
    {
        CreateWall("Left Wall", new Vector2(-41f, 30f), new Vector2(1f, 62f));
        CreateWall("Right Wall", new Vector2(41f, 30f), new Vector2(1f, 62f));
        CreateWall("Bottom Wall", new Vector2(0f, -1f), new Vector2(82f, 1f));
        CreateWall("Top Wall", new Vector2(0f, 61f), new Vector2(82f, 1f));
    }

    private static void CreateWall(string name, Vector2 position, Vector2 size)
    {
        GameObject wall = new(name);
        wall.transform.position = position;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
    }

    private static void SetPrivateField<T>(Object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new System.MissingFieldException(target.GetType().Name, fieldName);
        }

        field.SetValue(target, value);
        EditorUtility.SetDirty(target);
    }
}
