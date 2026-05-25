using UnityEngine;

public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] float lerpSpeed   = 2f;   // camera zoom smoothing speed
    [SerializeField] float spawnMargin = 1.5f; // world units of extra space beyond visible edge

    void Start()
    {
        transform.position = new Vector3(0f, 0f, -10f);
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null || ArenaState.Instance == null) return;

        // Smooth logarithmic zoom driven by the largest tile's camera score
        float target = TileProgression.TargetOrthoSize(ArenaState.Instance.GlobalCameraScore);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, target, Time.deltaTime * lerpSpeed);

        // Publish updated bounds so spawner and drift scripts stay in sync
        float halfH = cam.orthographicSize + spawnMargin;
        float halfW = halfH * cam.aspect;
        ArenaState.SetBounds(halfW, halfH);
    }

    // Legacy hook — kept so FloatileSceneSetup.SetPlayer() compiles without error
    public void SetPlayer(Transform t) { }
}
