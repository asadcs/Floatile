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

    void HandleEat(long _)    => Animate(PunchRoutine(0.22f, 0.13f));
    void HandleEvolve(long _) => Animate(SpringRoutine(0.50f, 0.28f));
    void HandleSpecial(SpecialTile _) => Animate(PunchRoutine(0.38f, 0.20f));

    void HandlePenalty(long _)
    {
        Animate(SquashRoutine(0.26f));
        StartCoroutine(FlashRoutine(Color.white, 0.18f));
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
}
