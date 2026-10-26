using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-1000)]
public class GameManager : Singleton<GameManager>
{
    public const string RESOURCE_PATH = "GameManager";

    [SerializeField, FormerlySerializedAs("fxManager")] private SceneTransitionManager sceneTransitionManager;
    [SerializeField] private AudioManager audioManager;
    public HeartManager heartManager;
    public RewardManager rewardManager;
    public ToastManager toastManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameManager prefab = Resources.Load<GameManager>(RESOURCE_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[GameManager] Khong tim thay prefab Resources/{RESOURCE_PATH}.");
            return;
        }

        GameManager manager = Instantiate(prefab);
        manager.name = prefab.name;
        DontDestroyOnLoad(manager.gameObject);
    }

    protected override void OnAwake()
    {
        Application.targetFrameRate = GameConfigSO.Instance.TargetFrameRate;

        if (IsAssigned(sceneTransitionManager, nameof(sceneTransitionManager))) sceneTransitionManager.Init();
        if (IsAssigned(audioManager, nameof(audioManager))) audioManager.Init();
        if (IsAssigned(toastManager, nameof(toastManager))) toastManager.Init(audioManager);
        if (IsAssigned(heartManager, nameof(heartManager))) heartManager.Init(toastManager);

        if (IsAssigned(rewardManager, nameof(rewardManager)))
        {
            rewardManager.Init();
            RewardBindings.Bind(rewardManager);
        }
    }

    private bool IsAssigned(Object target, string fieldName)
    {
        if (target != null) return true;

        Debug.LogError($"[GameManager] {fieldName} chua duoc gan trong prefab.", this);
        return false;
    }
}
