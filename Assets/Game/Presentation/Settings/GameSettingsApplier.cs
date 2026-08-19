using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSettingsApplier : MonoBehaviour
{
    public static GameSettingsApplier Instance { get; private set; }

    [SerializeField] private int defaultMaxFps = 60;   // set this in the Inspector for now

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyFrameRate(LoadMaxFps());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => ApplyFrameRate(LoadMaxFps());

    int LoadMaxFps() => PlayerPrefs.GetInt("MaxFps", defaultMaxFps); // falls back to Inspector value

    void ApplyFrameRate(int fps)
    {
        QualitySettings.vSyncCount = 0;        // REQUIRED — else targetFrameRate is ignored
        Application.targetFrameRate = fps;     // -1 = uncapped
    }

    // call this from the settings menu later:
    public void SetMaxFps(int fps)
    {
        PlayerPrefs.SetInt("MaxFps", fps);
        PlayerPrefs.Save();
        ApplyFrameRate(fps);
    }

    void OnDestroy() { if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded; }
}