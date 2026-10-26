using UnityEngine;

namespace Core.GameUI.Util
{
    public class SafeArea : MonoBehaviour
    {
        private void Start()
        {
            ResetSafeArea();
        }

        private void OnRectTransformDimensionsChange()
        {
            ResetSafeArea();
        }

        private void ResetSafeArea()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            Rect safeArea = Screen.safeArea;
            Vector2 minAnchor = safeArea.position;
            Vector2 maxAnchor = minAnchor + safeArea.size;
            
            minAnchor.x /= Screen.width;
            minAnchor.y /= Screen.height;
            maxAnchor.x /= Screen.width;
            maxAnchor.y /= Screen.height;

            rectTransform.anchorMin = minAnchor;
            rectTransform.anchorMax = maxAnchor;
        }
    }
}
