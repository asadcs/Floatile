#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FloatileStaticQAEditor
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var report = new StringBuilder();
        bool failed = false;
        report.AppendLine("════════════════════════════════════════");
        report.AppendLine("FLOATILE STATIC QA RUN");
        report.AppendLine("════════════════════════════════════════");

        failed |= !CheckPrefabs(report);
        failed |= !CheckFoodMonsterSpawner(report);
        failed |= !CheckNpcDirectionApi(report);
        failed |= !CheckAabbMath(report);

        report.AppendLine("════════════════════════════════════════");
        report.AppendLine(failed ? "FLOATILE STATIC QA RESULT: FAIL" : "FLOATILE STATIC QA RESULT: PASS");
        report.AppendLine("════════════════════════════════════════");
        Debug.Log(report.ToString());
        EditorApplication.Exit(failed ? 1 : 0);
    }

    static bool CheckPrefabs(StringBuilder report)
    {
        bool pass = true;
        pass &= CheckPrefab("Assets/Prefabs/NPC_Drift.prefab", report);
        pass &= CheckPrefab("Assets/Prefabs/SpecialTile.prefab", report);
        report.AppendLine($"PREFAB HITBOX CONTRACT: {(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    static bool CheckPrefab(string path, StringBuilder report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            report.AppendLine($"  FAIL: missing prefab {path}");
            return false;
        }

        bool hasBox = prefab.GetComponent<BoxCollider2D>() != null;
        bool hasHitbox = prefab.GetComponent<TileHitbox>() != null;
        bool hasCircle = prefab.GetComponent<CircleCollider2D>() != null;
        bool trigger = prefab.GetComponent<BoxCollider2D>()?.isTrigger == true;
        bool pass = hasBox && hasHitbox && !hasCircle && trigger;
        report.AppendLine($"  {path}: {(pass ? "PASS" : "FAIL")} box={hasBox} hitbox={hasHitbox} circle={hasCircle} trigger={trigger}");
        return pass;
    }

    static bool CheckFoodMonsterSpawner(StringBuilder report)
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerProgression>();
        var spawner = UnityEngine.Object.FindFirstObjectByType<ArenaSpawner>();
        if (player == null || spawner == null)
        {
            report.AppendLine("FOOD/MONSTER SPAWNER CONTRACT: FAIL missing player/spawner");
            return false;
        }

        Type spawnerType = typeof(ArenaSpawner);
        MethodInfo pickFood = spawnerType.GetMethod("PickFoodTier", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo pickMonster = spawnerType.GetMethod("PickMonsterTier", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo playerField = spawnerType.GetField("playerProg", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo heroField = spawnerType.GetField("heroH", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo gameTimeField = spawnerType.GetField("gameTime", BindingFlags.NonPublic | BindingFlags.Instance);

        if (pickFood == null || pickMonster == null || playerField == null || heroField == null || gameTimeField == null)
        {
            report.AppendLine("FOOD/MONSTER SPAWNER CONTRACT: FAIL reflection hooks missing");
            return false;
        }

        playerField.SetValue(spawner, player);
        bool allPass = true;

        long[] heroTiers = { 2L, 16L, 256L, 8192L };
        foreach (long hero in heroTiers)
        {
            player.ForceSetTier(hero);
            heroField.SetValue(spawner, hero);

            var foodCounts = new SortedDictionary<long, int>();
            bool foodInvalid = false;
            for (int i = 0; i < 240; i++)
            {
                long tier = (long)pickFood.Invoke(spawner, null);
                foodCounts[tier] = foodCounts.TryGetValue(tier, out int current) ? current + 1 : 1;
                if (!IsValidFoodForHero(hero, tier))
                    foodInvalid = true;
            }

            bool hasLowest = foodCounts.ContainsKey(2L);
            long highestExpected = Math.Min(hero, TileProgression.FoodTierMax);
            bool hasHighestWhenReachable = foodCounts.ContainsKey(highestExpected);
            bool foodPass = !foodInvalid && hasLowest && hasHighestWhenReachable;
            allPass &= foodPass;
            report.AppendLine($"FOOD PICKER H={TileVisual.FormatNumber(hero)}: {(foodPass ? "PASS" : "FAIL")} tiers={FormatCounts(foodCounts)}");
            if (foodInvalid) report.AppendLine("  GAP: food picker emitted value outside capped edible food set.");
            if (!hasHighestWhenReachable) report.AppendLine($"  GAP: highest expected food {TileVisual.FormatNumber(highestExpected)} did not appear.");
        }

        float[] elapsedSamples = { 0f, TileProgression.MonsterEscalationTime };
        foreach (long hero in heroTiers)
        {
            player.ForceSetTier(hero);
            heroField.SetValue(spawner, hero);

            foreach (float elapsed in elapsedSamples)
            {
                gameTimeField.SetValue(spawner, elapsed);
                long monster = (long)pickMonster.Invoke(spawner, null);
                float ratio = monster / (float)hero;
                float expected = elapsed <= 0f ? TileProgression.MonsterScaleInitial : TileProgression.MonsterScaleMin;
                long raw = (long)(hero * expected);
                int p = Mathf.Max(1, Mathf.RoundToInt(TileProgression.P(Math.Max(2L, raw))));
                long expectedTier = 1L << p;
                bool pass = monster == expectedTier && monster > hero;

                allPass &= pass;
                string phase = elapsed <= 0f ? "initial" : "late";
                report.AppendLine($"MONSTER PICKER H={TileVisual.FormatNumber(hero)} {phase}: {(pass ? "PASS" : "FAIL")} tier={TileVisual.FormatNumber(monster)} ratio={ratio:F2}");
                if (!pass) report.AppendLine($"  GAP: expected {TileVisual.FormatNumber(expectedTier)} and monster > hero.");
            }
        }

        return allPass;
    }

    static bool IsValidFoodForHero(long hero, long tier)
    {
        if (tier > hero || tier > TileProgression.FoodTierMax) return false;
        return tier == 2L || tier == 4L || tier == 8L || tier == 16L || tier == 32L;
    }

    static bool CheckNpcDirectionApi(StringBuilder report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPC_Drift.prefab");
        if (prefab == null)
        {
            report.AppendLine("NPC DIRECTION API: FAIL missing NPC prefab");
            return false;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            report.AppendLine("NPC DIRECTION API: FAIL could not instantiate prefab");
            return false;
        }

        var npc = instance.GetComponent<NPCDrift>();
        FieldInfo spawnedFromRight = typeof(NPCDrift).GetField("spawnedFromRight", BindingFlags.NonPublic | BindingFlags.Instance);
        bool pass = npc != null && spawnedFromRight != null;
        if (pass)
        {
            npc.Tier = 64L;
            npc.SetMonster(true);
            bool monsterFlag = npc.IsMonster;

            npc.SetDirection(false);
            bool leftSpawn = npc.MovingLeft == false && (bool)spawnedFromRight.GetValue(npc) == false;
            npc.SetDirection(true);
            bool rightSpawn = npc.MovingLeft == true && (bool)spawnedFromRight.GetValue(npc) == true;
            pass = monsterFlag && leftSpawn && rightSpawn;
            report.AppendLine($"NPC DIRECTION API: {(pass ? "PASS" : "FAIL")} monsterFlag={monsterFlag} leftSpawn={leftSpawn} rightSpawn={rightSpawn}");
        }
        else
        {
            report.AppendLine("NPC DIRECTION API: FAIL missing NPCDrift/spawnedFromRight");
        }

        UnityEngine.Object.DestroyImmediate(instance);
        return pass;
    }

    static bool CheckAabbMath(StringBuilder report)
    {
        Bounds hero = new(Vector3.zero, Vector3.one);
        Bounds enemy = new(new Vector3(1.08f, 0f, 0f), Vector3.one);
        bool nearMissSafe = !TileHitbox.Touches(hero, enemy);

        enemy.center = new Vector3(0.99f, 0f, 0f);
        bool edgeContact = TileHitbox.Touches(hero, enemy);

        MethodInfo swept = typeof(EatSystem).GetMethod("SweptAabbTouches", BindingFlags.NonPublic | BindingFlags.Static);
        bool sweptWorks = false;
        if (swept != null)
        {
            Bounds target = new(new Vector3(0f, 0f, 0f), Vector3.one);
            Bounds mover = new(new Vector3(1.4f, 0f, 0f), Vector3.one);
            sweptWorks = (bool)swept.Invoke(null, new object[] { new Vector2(-1.4f, 0f), mover, target });
        }

        bool pass = nearMissSafe && edgeContact && sweptWorks;
        report.AppendLine($"AABB COLLISION MATH: {(pass ? "PASS" : "FAIL")} nearMissSafe={nearMissSafe} edgeContact={edgeContact} swept={sweptWorks}");
        return pass;
    }

    static string FormatCounts(SortedDictionary<long, int> counts)
    {
        var sb = new StringBuilder();
        foreach (var kv in counts)
            sb.Append($"{TileVisual.FormatNumber(kv.Key)}={kv.Value} ");
        return sb.ToString();
    }
}
#endif
