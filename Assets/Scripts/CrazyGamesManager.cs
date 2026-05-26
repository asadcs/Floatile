using UnityEngine;
using System;
// To activate live SDK calls:
//   1. Import CrazyGames SDK package into this Unity project.
//   2. Add CRAZYGAMES to Project Settings → Player → Scripting Define Symbols (WebGL target).
// Without that flag this file compiles and runs as a no-op stub everywhere.
#if CRAZYGAMES
using CrazyGames;
#endif

public sealed class CrazyGamesManager : MonoBehaviour
{
    public static CrazyGamesManager Instance { get; private set; }

    // Ad break fires every N evolves, but never more often than MIN_INTERVAL seconds.
    const int   AD_EVERY_N_EVOLVES = 5;
    const float AD_MIN_INTERVAL    = 90f;

    int   _evolveCount;
    float _lastAdTime = -999f;
    bool  _adPlaying;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if CRAZYGAMES
        CrazySDK.Init();
        Debug.Log("[CrazyGames] SDK initialised.");
#else
        Debug.Log("[CrazyGames] Running without SDK (CRAZYGAMES not defined).");
#endif
    }

    void Start()
    {
        // Subscribe to evolve events without coupling EatSystem to this class.
        var eat = FindFirstObjectByType<EatSystem>();
        if (eat != null) eat.OnEvolve += HandleEvolve;
    }

    // ── Public API (called by GameManager / UIManager) ────────────────────────

    public void NotifyGameplayStart()
    {
#if CRAZYGAMES
        CrazySDK.Instance?.gameplayStart();
#endif
    }

    public void NotifyGameplayStop()
    {
#if CRAZYGAMES
        CrazySDK.Instance?.gameplayStop();
#endif
    }

    // Request a midgame ad break. Game pauses during the ad; onComplete fires after.
    public void RequestAdBreak(Action onComplete = null)
    {
        if (_adPlaying)
        {
            onComplete?.Invoke();
            return;
        }
        if (Time.unscaledTime - _lastAdTime < AD_MIN_INTERVAL)
        {
            onComplete?.Invoke();
            return;
        }

        _adPlaying  = true;
        _lastAdTime = Time.unscaledTime;

        NotifyGameplayStop();
        Time.timeScale      = 0f;
        AudioListener.pause = true;

#if CRAZYGAMES
        CrazyAds.Instance.beginAdBreak(() => AdFinished(onComplete));
#else
        AdFinished(onComplete); // stub: no ad, resume immediately
#endif
    }

    // ── Tab / focus ───────────────────────────────────────────────────────────

    void OnApplicationFocus(bool hasFocus)
    {
        if (_adPlaying) return; // don't fight ad-break state

#if !UNITY_EDITOR
        // In WebGL builds: mute and stop gameplay signal when tab hidden.
        AudioListener.pause = !hasFocus;
        if (hasFocus) NotifyGameplayStart();
        else          NotifyGameplayStop();
#endif
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void HandleEvolve(long newTier)
    {
        _evolveCount++;

        // Celebrate every evolve with CrazyGames happy-time sparkles.
        HappyTime();

        // Midgame ad break on the Nth evolve (throttled by AD_MIN_INTERVAL).
        if (_evolveCount % AD_EVERY_N_EVOLVES == 0)
            RequestAdBreak();
    }

    void HappyTime()
    {
#if CRAZYGAMES
        CrazySDK.Instance?.happyTime();
#endif
    }

    void AdFinished(Action onComplete)
    {
        _adPlaying          = false;
        Time.timeScale      = 1f;
        AudioListener.pause = false;
        NotifyGameplayStart();
        onComplete?.Invoke();
        Debug.Log("[CrazyGames] Ad break complete.");
    }
}
