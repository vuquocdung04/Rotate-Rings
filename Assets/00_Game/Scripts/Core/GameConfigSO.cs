using UnityEngine;

[CreateAssetMenu(menuName = "Base/Game/Game Config", fileName = "GameConfigSO")]
public class GameConfigSO : ScriptableObject
{
    public const string RESOURCE_PATH = "GameConfigSO";

    private static GameConfigSO instance;

    public static GameConfigSO Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = Resources.Load<GameConfigSO>(RESOURCE_PATH);

            if (instance == null)
            {
                Debug.LogWarning($"[GameConfigSO] Khong tim thay Resources/{RESOURCE_PATH}, dung gia tri mac dinh.");
                instance = CreateInstance<GameConfigSO>();
            }

            return instance;
        }
    }

    [Header("Application")]
    [SerializeField] private int targetFrameRate = 60;

    [Header("Testing")]
    [SerializeField] private bool testing;
    [SerializeField] private int levelTest = 1;

    public int TargetFrameRate => Mathf.Max(30, targetFrameRate);

    public bool Testing
    {
        get
        {
#if UNITY_EDITOR
            return testing;
#else
            return false;
#endif
        }
    }

    public int LevelTest => Mathf.Max(1, levelTest);

    public int CurrentLevel => Testing ? LevelTest : UseProfile.Level;
}
