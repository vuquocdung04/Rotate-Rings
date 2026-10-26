using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    private const float SceneReadyProgress = 0.9f;

    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private SquareTransition square;
    public float transitionDurationOut = 1f;
    public float transitionDurationIn = 1f;
    [SerializeField, Min(0f)] private float revealHoldTimeout = 10f;

    private bool isTransitioning;
    private bool revealPending;
    private int revealHolds;
    private Action revealCallbacks;

    public bool IsTransitioning => isTransitioning;
    public bool IsRevealPending => revealPending;

    public void Init()
    {
        Instance = this;

        if (square == null) square = FindAnyObjectByType<SquareTransition>(FindObjectsInactive.Include);
        square.Hide();
    }

    public void HoldReveal()
    {
        if (isTransitioning) revealHolds++;
    }

    public void NotifySceneReady()
    {
        if (revealHolds > 0) revealHolds--;
    }

    public async Awaitable NotifySceneReadyAsync()
    {
        NotifySceneReady();

        if (!isTransitioning) return;

        await AwaitableEx.WaitUntil(() => !isTransitioning, destroyCancellationToken);
    }

    public void WaitReveal(Action onReveal)
    {
        if (onReveal == null) return;

        if (!revealPending)
        {
            onReveal();
            return;
        }

        revealCallbacks += onReveal;
    }

    public void StopWaitReveal(Action onReveal) => revealCallbacks -= onReveal;

    public AsyncOperation PreloadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneTransition] Scene '{sceneName}' chưa có trong Build Profile.");
            return null;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;
        return operation;
    }

    public bool LoadScene(string sceneName, bool skipOutPhase = false)
    {
        if (IsBusy()) return false;

        AsyncOperation operation = PreloadScene(sceneName);
        if (operation == null) return false;

        TransitionAsync(operation, skipOutPhase).Forget();
        return true;
    }

    public bool LoadScene(AsyncOperation preloaded, bool skipOutPhase = false)
    {
        if (preloaded == null || IsBusy()) return false;

        TransitionAsync(preloaded, skipOutPhase).Forget();
        return true;
    }

    public bool ReloadScene(bool skipOutPhase = false) => LoadScene(SceneManager.GetActiveScene().name, skipOutPhase);

    public void PrepareCovered()
    {
        square.Prepare();
        square.SetCovered();
    }

    private bool IsBusy()
    {
        if (!isTransitioning) return false;

        Debug.LogWarning("[SceneTransition] Đang chuyển scene, bỏ qua lệnh LoadScene mới.");
        return true;
    }

    private async Awaitable TransitionAsync(AsyncOperation operation, bool skipOutPhase)
    {
        isTransitioning = true;
        revealPending = true;
        revealHolds = 0;

        try
        {
            square.Prepare();

            if (skipOutPhase)
                square.SetCovered();
            else
                await square.Cover(transitionDurationOut);

            while (operation.progress < SceneReadyProgress)
                await Awaitable.NextFrameAsync(destroyCancellationToken);

            operation.allowSceneActivation = true;

            while (!operation.isDone)
                await Awaitable.NextFrameAsync(destroyCancellationToken);

            square.SetCovered();

            await Awaitable.NextFrameAsync(destroyCancellationToken);
            await WaitRevealHoldsAsync();

            await square.Reveal(transitionDurationIn);
        }
        finally
        {
            if (square != null) square.Hide();

            isTransitioning = false;
            revealHolds = 0;
            FireReveal();
        }
    }

    private async Awaitable WaitRevealHoldsAsync()
    {
        float elapsed = 0f;

        while (revealHolds > 0)
        {
            if (revealHoldTimeout > 0f && elapsed >= revealHoldTimeout)
            {
                Debug.LogWarning($"[SceneTransition] Scene chưa gọi NotifySceneReady sau {revealHoldTimeout}s, tự mở màn.");
                return;
            }

            await Awaitable.NextFrameAsync(destroyCancellationToken);
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private void FireReveal()
    {
        if (!revealPending) return;

        revealPending = false;

        Action callbacks = revealCallbacks;
        revealCallbacks = null;
        callbacks?.Invoke();
    }
}
