using UnityEngine;
using System.Collections;

// Visual-only feedback layer. Subscribes to EatSystem events and plays
// scale animations + shockwave ring + sparks. No gameplay logic here.
[RequireComponent(typeof(EatSystem))]
public sealed class CollisionFX : MonoBehaviour
{
    PlayerProgression _prog;
    EatSystem         _eat;
    SpriteRenderer    _sr;
    Coroutine         _scale;

    void Awake()
    {
        _prog = GetComponent<PlayerProgression>();
        _eat  = GetComponent<EatSystem>();
        _sr   = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        _eat.OnEat            += HandleEat;
        _eat.OnEvolve         += HandleEvolve;
        _eat.OnPenalty        += HandlePenalty;
        _eat.OnSpecialCollect += HandleSpecial;
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        _eat.OnEat            -= HandleEat;
        _eat.OnEvolve         -= HandleEvolve;
        _eat.OnPenalty        -= HandlePenalty;
        _eat.OnSpecialCollect -= HandleSpecial;
    }

    void HandleEat(long enemyTier)
    {
        Animate(PunchRoutine(0.28f, 0.14f));
        Color c = TierColorTable.ForTier(enemyTier);
        Vector3 npcPos = _eat != null ? (Vector3)_eat.LastEatPos : transform.position;
        SpawnSparks(npcPos, c, 12, SparkShape.Directional);
        StartCoroutine(ShockwaveRoutine(0.22f, 1.8f, c, 0.6f));
        ScreenShake.Instance?.Shake(0.07f, 0.10f);
    }

    void HandleEvolve(long newTier)
    {
        Animate(SpringRoutine(0.55f, 0.32f));
        Color c = TierColorTable.ForTier(newTier);
        SpawnSparks(transform.position, c, 26, SparkShape.Ring);
        StartCoroutine(ShockwaveRoutine(0.40f, 2.5f, c, 0.8f));
        ScreenShake.Instance?.Shake(0.15f, 0.22f);
    }

    void HandleSpecial(SpecialTile _)
    {
        Animate(PunchRoutine(0.42f, 0.22f));
        Color gold = new Color(1f, 0.88f, 0.15f);
        SpawnSparks(transform.position, gold, 20, SparkShape.Arc);
        StartCoroutine(ShockwaveRoutine(0.30f, 2.0f, gold, 0.7f));
        ScreenShake.Instance?.Shake(0.11f, 0.16f);
    }

    void HandlePenalty(long _)
    {
        Animate(SquashRoutine(0.28f));
        StartCoroutine(FlashRoutine(Color.white, 0.18f));
        Color red = new Color(1f, 0.18f, 0.08f);
        SpawnSparks(transform.position, red, 32, SparkShape.Explosion);
        StartCoroutine(ShockwaveRoutine(0.28f, 2.2f, red, 0.9f));
        ScreenShake.Instance?.Shake(0.25f, 0.40f);
    }

    // ── Consumed tile death pop ───────────────────────────────────────────────
    // Called by EatSystem instead of bare Destroy(). The tile briefly punches
    // out then vanishes, giving each eat a physical "pop" feel.

    public static void PopThenDestroy(GameObject go, MonoBehaviour runner)
    {
        if (go == null) return;
        runner.StartCoroutine(PopRoutine(go));
    }

    // 2-frame white flash then scale-pop then destroy.
    public static void FlashPopDestroy(GameObject go, MonoBehaviour runner)
    {
        if (go == null) return;
        runner.StartCoroutine(FlashPopRoutine(go));
    }

    static IEnumerator FlashPopRoutine(GameObject go)
    {
        if (go == null) yield break;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) { sr.color = Color.white; yield return null; yield return null; }
        if (go == null) yield break;
        Vector3 start = go.transform.localScale;
        float t = 0f;
        while (t < 0.08f)
        {
            if (go == null) yield break;
            go.transform.localScale = Vector3.LerpUnclamped(start, start * 1.30f, Smooth(t / 0.08f));
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    // Vlambeer hitstop: pause world time for `duration` unscaled seconds.
    // Brain uses the gap to register the impact without consciously noticing it.
    public static IEnumerator HitFreeze(float duration)
    {
        Time.timeScale = 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Time.timeScale = 1f;
    }

    static IEnumerator PopRoutine(GameObject go)
    {
        if (go == null) yield break;
        Vector3 start = go.transform.localScale;
        Vector3 peak  = start * 1.30f;
        float   t     = 0f;
        while (t < 0.08f)
        {
            if (go == null) yield break;
            go.transform.localScale = Vector3.LerpUnclamped(start, peak, Smooth(t / 0.08f));
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    // ── Scale animations ──────────────────────────────────────────────────────

    void Animate(IEnumerator routine)
    {
        if (_scale != null) { StopCoroutine(_scale); RestoreScale(); }
        _scale = StartCoroutine(routine);
    }

    void RestoreScale()
    {
        if (_prog != null)
            transform.localScale = Vector3.one * TileProgression.PhysicalSize(_prog.CurrentTier);
    }

    Vector3 BaseScale() =>
        _prog != null
            ? Vector3.one * TileProgression.PhysicalSize(_prog.CurrentTier)
            : transform.localScale;

    IEnumerator PunchRoutine(float strength, float duration)
    {
        Vector3 normal = BaseScale();
        Vector3 peak   = normal * (1f + strength);
        float   half   = duration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(normal, peak, Smooth(t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(peak, normal, Smooth(t / half));
            yield return null;
        }
        transform.localScale = normal;
        _scale = null;
    }

    IEnumerator SpringRoutine(float strength, float duration)
    {
        Vector3 normal = BaseScale();
        Vector3 peak   = normal * (1f + strength);
        Vector3 dip    = normal * 0.93f;
        float   t3     = duration / 3f;

        for (float t = 0f; t < t3; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(normal, peak, Smooth(t / t3));
            yield return null;
        }
        for (float t = 0f; t < t3; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(peak, dip, Smooth(t / t3));
            yield return null;
        }
        for (float t = 0f; t < t3; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(dip, normal, Smooth(t / t3));
            yield return null;
        }
        transform.localScale = normal;
        _scale = null;
    }

    IEnumerator SquashRoutine(float duration)
    {
        Vector3 normal = BaseScale();
        float   s      = normal.x;
        Vector3 squash = new(s * 1.45f, s * 0.68f, s);
        float   half   = duration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(normal, squash, Smooth(t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(squash, normal, Smooth(t / half));
            yield return null;
        }
        transform.localScale = normal;
        _scale = null;
    }

    IEnumerator FlashRoutine(Color target, float duration)
    {
        if (_sr == null) yield break;
        Color from = _sr.color;
        float half = duration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            _sr.color = Color.Lerp(from, target, Smooth(t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            _sr.color = Color.Lerp(target, from, Smooth(t / half));
            yield return null;
        }
        _sr.color = from;
    }

    // Expanding tile-shaped ring that fades out.
    IEnumerator ShockwaveRoutine(float duration, float endScaleMult, Color color, float startAlpha)
    {
        if (_sr?.sprite == null) yield break;

        var go  = new GameObject("[Shockwave]");
        var sr  = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _sr.sprite;
        sr.sortingOrder = _sr.sortingOrder - 1;

        Color c = color;
        c.a = startAlpha;
        sr.color = c;

        Vector3 startScale = transform.localScale;
        Vector3 endScale   = startScale * endScaleMult;
        go.transform.position   = transform.position;
        go.transform.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float p = elapsed / duration;
            go.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, Smooth(p));
            c.a = startAlpha * (1f - p);
            sr.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }

    static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, t);

    // ── Spark shapes ──────────────────────────────────────────────────────────

    enum SparkShape { Circle, Directional, Ring, Explosion, Arc }

    static void SpawnSparks(Vector3 pos, Color color, int count, SparkShape shape)
    {
        var go  = new GameObject("[Sparks]");
        go.transform.position = pos;

        var ps  = go.AddComponent<ParticleSystem>();
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sortingOrder = 12;

        var main             = ps.main;
        main.loop            = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = color;
        main.gravityModifier = shape == SparkShape.Arc ? 0.45f : 0.20f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shapeModule = ps.shape;

        switch (shape)
        {
            case SparkShape.Directional:
                // Small focused burst — eat feels like impact
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.45f);
                main.startSpeed    = new ParticleSystem.MinMaxCurve(5f, 10f);
                main.startSize     = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
                shapeModule.shapeType = ParticleSystemShapeType.Circle;
                shapeModule.radius    = 0.08f;
                break;

            case SparkShape.Ring:
                // Wide ring burst — evolve feels expansive
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
                main.startSpeed    = new ParticleSystem.MinMaxCurve(6f, 14f);
                main.startSize     = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                shapeModule.shapeType = ParticleSystemShapeType.Circle;
                shapeModule.radius    = 0.20f;
                break;

            case SparkShape.Explosion:
                // Large radius, high speed — penalty feels punishing
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.65f);
                main.startSpeed    = new ParticleSystem.MinMaxCurve(8f, 18f);
                main.startSize     = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
                main.gravityModifier = 0.30f;
                shapeModule.shapeType = ParticleSystemShapeType.Circle;
                shapeModule.radius    = 0.25f;
                break;

            case SparkShape.Arc:
                // Upward arc shower — special tile collect feels like treasure
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.80f);
                main.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 10f);
                main.startSize     = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
                shapeModule.shapeType = ParticleSystemShapeType.Cone;
                shapeModule.angle     = 50f;
                shapeModule.radius    = 0.10f;
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                break;

            default:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.65f);
                main.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 12f);
                main.startSize     = new ParticleSystem.MinMaxCurve(0.08f, 0.26f);
                shapeModule.shapeType = ParticleSystemShapeType.Circle;
                shapeModule.radius    = 0.12f;
                break;
        }

        Destroy(go, 1.5f);
    }
}
