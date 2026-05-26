using UnityEngine;
using System.Collections;

public sealed class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    Coroutine _running;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void Shake(float duration = 0.15f, float magnitude = 0.2f)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Vector3 origin = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float str = magnitude * (1f - elapsed / duration); // fade out over time
            transform.position = origin + new Vector3(
                Random.Range(-1f, 1f) * str,
                Random.Range(-1f, 1f) * str,
                0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = origin;
        _running = null;
    }
}
