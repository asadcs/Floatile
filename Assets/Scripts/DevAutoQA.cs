#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class DevAutoQA : MonoBehaviour
{
    readonly struct SpawnSample
    {
        public readonly string Label;
        public readonly long HeroTier;

        public SpawnSample(string label, long heroTier)
        {
            Label = label;
            HeroTier = heroTier;
        }
    }

    static readonly SpawnSample[] SpawnSamples =
    {
        new("Initial H=2", 2L),
        new("Early H=16", 16L),
        new("Mid H=256", 256L),
        new("High H=8K", 8192L),
    };

    public static void Run()
    {
        var go = new GameObject("[DevAutoQA]");
        DontDestroyOnLoad(go);
        go.AddComponent<DevAutoQA>();
    }

    IEnumerator Start()
    {
        yield return null;
        yield return new WaitForSeconds(0.5f);

        var report = new StringBuilder();
        report.AppendLine("════════════════════════════════════════");
        report.AppendLine("FLOATILE AUTO QA RUN");
        report.AppendLine("════════════════════════════════════════");

        PlayerProgression player = FindFirstObjectByType<PlayerProgression>();
        ArenaSpawner spawner = FindFirstObjectByType<ArenaSpawner>();
        if (player == null || spawner == null)
        {
            report.AppendLine("FAIL: PlayerProgression or ArenaSpawner missing.");
            Finish(report, failed: true);
            yield break;
        }

        bool failed = false;
        foreach (var sample in SpawnSamples)
        {
            yield return RunSpawnScenario(sample, player, spawner, report, result => failed |= !result);
        }

        spawner.enabled = false;
        spawner.ClearAllNPCs();
        yield return null;

        yield return RunCollisionScenarios(player, spawner, report, result => failed |= !result);

        Finish(report, failed);
    }

    IEnumerator RunSpawnScenario(SpawnSample sample, PlayerProgression player, ArenaSpawner spawner, StringBuilder report, System.Action<bool> done)
    {
        player.ForceSetTier(sample.HeroTier);
        spawner.ClearAllNPCs();
        yield return new WaitForSeconds(0.2f);

        float end = Time.time + 8f;
        while (Time.time < end)
            yield return null;

        var counts = CountNpcs(sample.HeroTier);
        bool validPool = counts.Other == 0;
        bool hasEqual = counts.Equal > 0;
        bool hasFoodOrLowStart = sample.HeroTier == 2L || counts.Food > 0;
        bool dangerControlled = counts.Total == 0 || counts.Danger / (float)counts.Total <= 0.28f;
        bool pass = validPool && hasEqual && hasFoodOrLowStart && dangerControlled && counts.Total >= 8;

        report.AppendLine($"SPAWN {sample.Label}: {(pass ? "PASS" : "FAIL")}");
        report.AppendLine($"  total={counts.Total} food={counts.Food} equal={counts.Equal} danger={counts.Danger} other={counts.Other}");
        report.AppendLine($"  tiers={counts.FormatTiers()}");
        if (!validPool) report.AppendLine("  GAP: invalid tier outside food/H/Hx2 pool.");
        if (!hasEqual) report.AppendLine("  GAP: equal hero tier missing; progression can stall.");
        if (!hasFoodOrLowStart) report.AppendLine("  GAP: no smaller food values visible.");
        if (!dangerControlled) report.AppendLine("  GAP: danger fraction above 28% QA limit.");
        if (counts.Total < 8) report.AppendLine("  GAP: stream too sparse after sample window.");
        done(pass);
    }

    IEnumerator RunCollisionScenarios(PlayerProgression player, ArenaSpawner spawner, StringBuilder report, System.Action<bool> done)
    {
        player.ForceSetTier(16L);
        player.transform.position = Vector3.zero;
        spawner.SpawnNPCAtTier(16L);
        yield return null;

        NPCDrift npc = FindFirstObjectByType<NPCDrift>();
        if (npc == null)
        {
            report.AppendLine("COLLISION MICROTESTS: FAIL");
            report.AppendLine("  GAP: could not spawn NPC for collision test.");
            done(false);
            yield break;
        }

        npc.enabled = false;
        var eat = player.GetComponent<EatSystem>();
        var playerHitbox = player.GetComponent<TileHitbox>();
        var npcHitbox = npc.GetComponent<TileHitbox>();
        playerHitbox.Sync();
        npcHitbox.Sync();

        float halfSum = playerHitbox.Bounds.extents.x + npcHitbox.Bounds.extents.x;
        float y = player.transform.position.y;

        npc.transform.position = new Vector3(halfSum + 0.08f, y, 0f);
        yield return new WaitForFixedUpdate();
        bool nearMissSafe = player.CurrentTier == 16L && npc != null;

        npc.transform.position = new Vector3(halfSum - 0.01f, y, 0f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        bool edgeContactEvolves = player.CurrentTier == 32L || npc == null;

        spawner.ClearAllNPCs();
        player.ForceSetTier(16L);
        player.transform.position = Vector3.zero;
        spawner.SpawnNPCAtTier(32L);
        yield return null;
        npc = FindFirstObjectByType<NPCDrift>();
        if (npc != null)
        {
            npc.enabled = false;
            npc.transform.position = Vector3.zero;
        }
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        bool overlapPenalty = player.CurrentTier == 8L;

        bool pass = nearMissSafe && edgeContactEvolves && overlapPenalty;
        report.AppendLine($"COLLISION MICROTESTS: {(pass ? "PASS" : "FAIL")}");
        report.AppendLine($"  nearMissSafe={nearMissSafe}");
        report.AppendLine($"  edgeContactEvolves={edgeContactEvolves}");
        report.AppendLine($"  overlapPenalty={overlapPenalty}");
        if (!nearMissSafe) report.AppendLine("  GAP: near miss still triggered collision too early.");
        if (!edgeContactEvolves) report.AppendLine("  GAP: edge/near-edge contact did not trigger.");
        if (!overlapPenalty) report.AppendLine("  GAP: true overlap with bigger tile did not apply penalty.");
        done(pass);
    }

    static SpawnCounts CountNpcs(long heroTier)
    {
        var counts = new SpawnCounts(heroTier);
        foreach (var npc in FindObjectsByType<NPCDrift>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            counts.Add(npc.Tier);
        return counts;
    }

    static void Finish(StringBuilder report, bool failed)
    {
        report.AppendLine("════════════════════════════════════════");
        report.AppendLine(failed ? "FLOATILE AUTO QA RESULT: FAIL" : "FLOATILE AUTO QA RESULT: PASS");
        report.AppendLine("════════════════════════════════════════");
        Debug.Log(report.ToString());
        EditorApplication.Exit(failed ? 1 : 0);
    }

    struct SpawnCounts
    {
        readonly long heroTier;
        readonly SortedDictionary<long, int> tiers;

        public int Total;
        public int Food;
        public int Equal;
        public int Danger;
        public int Other;

        public SpawnCounts(long heroTier)
        {
            this.heroTier = heroTier;
            tiers = new SortedDictionary<long, int>();
            Total = Food = Equal = Danger = Other = 0;
        }

        public void Add(long tier)
        {
            Total++;
            tiers[tier] = tiers.TryGetValue(tier, out int current) ? current + 1 : 1;
            if (tier < heroTier) Food++;
            else if (tier == heroTier) Equal++;
            else if (tier == SafeDouble(heroTier)) Danger++;
            else Other++;
        }

        public string FormatTiers()
        {
            var sb = new StringBuilder();
            foreach (var kv in tiers)
                sb.Append($"{TileVisual.FormatNumber(kv.Key)}={kv.Value} ");
            return sb.ToString();
        }

        static long SafeDouble(long value) => value <= long.MaxValue / 2 ? value * 2L : long.MaxValue;
    }
}
#endif
