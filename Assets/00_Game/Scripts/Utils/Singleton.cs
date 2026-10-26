using UnityEngine;

/// <summary>
/// Singleton generic chuẩn: tự set Instance trong Awake.
/// Bật cờ <see cref="dontDestroyOnLoad"/> nếu muốn sống qua các scene.
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = false;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        OnAwake();
    }

    protected virtual void OnAwake() { }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

/// <summary>
/// Singleton khởi tạo thủ công (không tự chạy Awake).
/// Chủ sở hữu (vd GamePlayController) giữ reference rồi tự gọi
/// <see cref="InitInstance"/> và <see cref="Init"/> theo đúng thứ tự mong muốn.
/// </summary>
public abstract class InitSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    public void InitInstance() => Instance = this as T;

    public abstract void Init();

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
