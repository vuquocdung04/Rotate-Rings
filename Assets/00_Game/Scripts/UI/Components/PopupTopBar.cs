using DG.Tweening;
using EventDispatcher;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class PopupTopBar : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private bool bringToFront = true;

    private CanvasGroup group;
    private int openCount;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.SetCanvasState(false, 0f);
    }

    private void OnEnable()
    {
        this.RegisterListener(EventID.POPUP_OPENED, OnPopupOpened);
        this.RegisterListener(EventID.POPUP_CLOSED, OnPopupClosed);
    }

    private void OnDisable()
    {
        this.RemoveListener(EventID.POPUP_OPENED, OnPopupOpened);
        this.RemoveListener(EventID.POPUP_CLOSED, OnPopupClosed);
        group.DOKill();
    }

    private void OnPopupOpened(object _)
    {
        openCount++;
        if (bringToFront) transform.SetAsLastSibling();
        Refresh();
    }

    private void OnPopupClosed(object _)
    {
        openCount = Mathf.Max(0, openCount - 1);
        Refresh();
    }

    private void Refresh()
    {
        bool visible = openCount > 0;

        group.DOKill();
        group.SetCanvasState(visible);
        group.DOFade(visible ? 1f : 0f, fadeDuration).SetUpdate(true).SetLink(gameObject);
    }
}
