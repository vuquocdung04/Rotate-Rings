using UnityEngine;
using UnityEngine.UI;

public class LoadingController : MonoBehaviour
{
    private const float SceneReadyProgress = 0.9f;

    [SerializeField] private Image fill;
    [SerializeField, Min(0f)] private float loadingDuration = 2f;
    [SerializeField] private ThreadPriority loadPriority = ThreadPriority.BelowNormal;

    private void Start() => RunAsync().Forget();

    private async Awaitable RunAsync()
    {
        fill.fillAmount = 0f;

        SceneTransitionManager transition = SceneTransitionManager.Instance;
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = loadPriority;

        AsyncOperation operation;

        try
        {
            operation = transition.PreloadScene(SceneName.GAME_PLAY);
            if (operation == null) return;

            float elapsed = 0f;

            while (fill.fillAmount < 1f)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                elapsed += Time.unscaledDeltaTime;

                float timeProgress = loadingDuration > 0f ? elapsed / loadingDuration : 1f;
                float loadProgress = operation.progress / SceneReadyProgress;

                fill.fillAmount = Mathf.Max(fill.fillAmount, Mathf.Min(timeProgress, loadProgress, 1f));
            }
        }
        finally
        {
            Application.backgroundLoadingPriority = previousPriority;
        }

        transition.LoadScene(operation);
    }
}
