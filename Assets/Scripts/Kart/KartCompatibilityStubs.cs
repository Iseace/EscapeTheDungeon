using System;
using Fusion;
using UnityEngine;

// Temporary compatibility shims so the imported kart sample scripts compile
// without the full original project.

public class Powerup : ScriptableObject
{
    public virtual void Use(NetworkRunner runner, KartEntity kart) { }
}

public class KartCamera : MonoBehaviour
{
    public ParticleSystem speedLines;
}

public class KartAudio : MonoBehaviour
{
    public void PlayHorn() { }
}

public class GameUI : MonoBehaviour
{
    public void Init(KartEntity kart) { }
    public void ShowEndRaceScreen() { }
}

public interface ICollidable
{
    void Collide(KartEntity kart);
}

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    public GameUI hudPrefab;
    public GameObject nicknameCanvasPrefab;
    public Powerup[] powerups = Array.Empty<Powerup>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }
}

public class Checkpoint : MonoBehaviour
{
    public int index;
}

public class FinishLine : MonoBehaviour
{
    public bool debug;
}

public class GameTypeSettings
{
    public int lapCount = 3;
    public bool practiceMode;

    public bool IsPracticeMode()
    {
        return practiceMode;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static Track CurrentTrack;

    public static int GroundLayer => LayerMask.NameToLayer("Ground");
    public static int KartLayer => LayerMask.NameToLayer("Kart");

    public GameTypeSettings GameType = new GameTypeSettings();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }
}

public class Track : MonoBehaviour
{
    public static Track Current;

    public TickTimer StartRaceTimer;
    public AudioClip music;
    public Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
    public FinishLine finishLine;

    private void Awake()
    {
        Current = this;
        GameManager.CurrentTrack = this;
    }
}

public static class AudioManager
{
    public enum MixerTarget
    {
        SFX,
        Music
    }

    public static void Play(string key, MixerTarget target, Vector3 position = default) { }
    public static void PlayMusic(AudioClip clip) { }
}

public static class TickHelper
{
    public static float TickToSeconds(NetworkRunner runner, int tickDelta)
    {
        if (runner == null)
            return 0f;

        return tickDelta * runner.DeltaTime;
    }
}
