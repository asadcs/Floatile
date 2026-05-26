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
        _eat.OnEat            -= HandleEat;
        _eat.OnEvolve         -= HandleEvolve;
        _eat.OnPenalty        -= HandlePenalty;
        _eat.OnSpecialCollect -= HandleSpecial;
    }

    void HandleEat(long enemyTier)
    {
        Animate(PunchRoutine(0.28f, 0.14f));
        Color c = TierColorTable.ForTier(enemyTier);
        SpawnSparks(transform.position, c, 12);
        StartCoroutine(ShockwaveRoutine(0.22f, 1.8f, c, 0.6f));
        ScreenShake.Instance?.Shake(0.07f, 0.10f);
    }

    void HandleEvolve(long newTier)
    {
        Animate(SpringRoutine(0.55f, 0.32f));
        Color c = TierColorTable.ForTier(newTier);
        SpawnSparks(transform.position, c, 26);
        StartCoroutine(ShockwaveRoutine(0.40f, 2.5f, c, 0.8f));
        ScreenShake.Instance?.Shake(0.15f, 0.22f);
    }

    void HandleSpecial(SpecialTile _)
    {
        Animate(PunchRoutine(0.42f, 0.22f));
        Color gold = new Color(1f, 0.88f, 0.15f);
        SpawnSparks(transform.position, gold, 20);
        StartCoroutine(ShockwaveRoutine(0.30f, 2.0f, gold, 0.7f));
        ScreenShake.Instance?.Shake(0.11f, 0.16f);
    }

    void HandlePenalty(long _)
    {
        Animate(SquashRoutine(0.28f));
        StartCoroutine(FlashRoutine(Color.white, 0.18f));
        Color red = new Color(1f, 0.18f, 0.08f);
        SpawnSparks(transform.position, red, 32);
        StartCoroutine(ShockwaveRoutine(0.28f, 2.2f, red, 0.9f));
        ScreenShake.Instance?.Shake(0.25f, 0.40f);
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

    // Expanding tile-shaped ring that fades out — much more impactful than sparks alone.
    // Copies the player's current sprite and scale, then expands to endScaleMult × size.
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

    static void SpawnSparks(Vector3 pos, Color color, int count)
    {
        var go  = new GameObject("[Sparks]");
        go.transform.position = pos;

        var ps  = go.AddComponent<ParticleSystem>();
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sortingOrder = 12;

        var main             = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.30f, 0.65f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(4f, 12f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.26f);
        main.startColor      = color;
        main.gravityModifier = 0.20f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape       = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.12f;

        Destroy(go, 1.5f);
    }
}
