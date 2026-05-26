using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { Starting, Playing, Paused }
    public State CurrentState { get; private set; } = State.Starting;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartGame()
    {
        CurrentState     = State.Playing;
        Time.timeScale   = 1f;
        CrazyGamesManager.Instance?.NotifyGameplayStart();
    }

    public void Pause()
    {
        if (CurrentState != State.Playing) return;
        CurrentState   = State.Paused;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (CurrentState != State.Paused) return;
        CurrentState   = State.Playing;
        Time.timeScale = 1f;
    }

    public bool IsPlaying => CurrentState == State.Playing;
}
