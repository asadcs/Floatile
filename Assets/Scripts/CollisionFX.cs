using UnityEngine;
using System.Collections;

// Visual-only feedback layer. Subscribes to EatSystem events and plays
// scale animations + hit flash. No gameplay logic here.
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
        _eat.OnEat            -= HandleEat;
        _eat.OnEvolve         -= HandleEvolve;
        _eat.OnPenalty        -= HandlePenalty;
        _eat.OnSpecialCollect -= HandleSpecial;
    }

    void HandleEat(long enemyTier)
    {
        Animate(PunchRoutine(0.22f, 0.13f));
        SpawnSparks(transform.position, TierColorTable.ForTier(enemyTier), 8);
        ScreenShake.Instance?.Shake(0.06f, 0.08f);
    }

    void HandleEvolve(long newTier)
    {
        Animate(SpringRoutine(0.50f, 0.28f));
        SpawnSparks(transform.position, TierColorTable.ForTier(newTier), 18);
        ScreenShake.Instance?.Shake(0.13f, 0.20f);
    }

    void HandleSpecial(SpecialTile _)
    {
        Animate(PunchRoutine(0.38f, 0.20f));
        SpawnSparks(transform.position, new Color(1f, 0.92f, 0.2f), 14); // gold burst
        ScreenShake.Instance?.Shake(0.10f, 0.14f);
    }

    void HandlePenalty(long _)
    {
        Animate(SquashRoutine(0.26f));
        StartCoroutine(FlashRoutine(Color.white, 0.18f));
        SpawnSparks(transform.position, new Color(1f, 0.25f, 0.1f), 24); // hot red-orange
        ScreenShake.Instance?.Shake(0.22f, 0.35f);
    }

    // Cancel any running scale animation and start a new one from a clean base.
    void Animate(IEnumerator routine)
    {
        if (_scale != null) { StopCoroutine(_scale); RestoreScale(); }
        _scale = StartCoroutine(routine);
    }

    // Restore the exact scale TileProgression expects for the current tier.
    void RestoreScale()
    {
        if (_prog != null)
            transform.localScale = Vector3.one * TileProgression.PhysicalSize(_prog.CurrentTier);
    }

    Vector3 BaseScale() =>
        _prog != null
            ? Vector3.one * TileProgression.PhysicalSize(_prog.CurrentTier)
            : transform.localScale;

    // ── Animations ────────────────────────────────────────────────────────────

    // Quick pop: scale up then back. Used for eat and special.
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

    // Celebratory spring: up → slight undershoot → settle. Used for evolve.
    IEnumerator SpringRoutine(float strength, float duration)
    {
        Vector3 normal = BaseScale();
        Vector3 peak   = normal * (1f + strength);
        Vector3 dip    = normal * 0.94f;
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

    // Impact squash: widen + flatten → spring back. Used for penalty.
    IEnumerator SquashRoutine(float duration)
    {
        Vector3 normal = BaseScale();
        float   s      = normal.x;
        Vector3 squash = new(s * 1.40f, s * 0.72f, s);
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

    // Brief white flash — reads live color so it works across all tier color changes.
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

    static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, t);

    static void SpawnSparks(Vector3 pos, Color color, int count)
    {
        var go = new GameObject("[Sparks]");
        go.transform.position = pos;

        var ps  = go.AddComponent<ParticleSystem>();
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sortingOrder = 10;

        var main             = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(3f, 9f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor      = color;
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape        = ps.shape;
        shape.shapeType  = ParticleSystemShapeType.Circle;
        shape.radius     = 0.15f;

        Destroy(go, 1.5f);
    }
}
