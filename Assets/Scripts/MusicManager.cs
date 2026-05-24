using UnityEngine;

public sealed class MusicManager : MonoBehaviour
{
    [SerializeField] private AudioClip bgMusic;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.35f;

    private AudioSource src;

    private void Awake()
    {
        // Survive scene reloads
        DontDestroyOnLoad(gameObject);

        src = gameObject.AddComponent<AudioSource>();
        src.clip        = bgMusic;
        src.loop        = true;
        src.volume      = volume;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D audio
    }

    private void Start()
    {
        if (bgMusic != null) src.Play();
    }
}
