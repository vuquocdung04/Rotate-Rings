using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class UIUtils
{
    public static void FitToBounds(this RectTransform holder)
    {
        if (holder == null) return;

        holder.localScale = Vector3.one;
        LayoutRebuilder.ForceRebuildLayoutImmediate(holder);

        float availableWidth = holder.rect.width;
        float availableHeight = holder.rect.height;

        float neededWidth = LayoutUtility.GetPreferredWidth(holder);
        float neededHeight = LayoutUtility.GetPreferredHeight(holder);

        float scaleX = neededWidth > availableWidth && neededWidth > 0f ? availableWidth / neededWidth : 1f;
        float scaleY = neededHeight > availableHeight && neededHeight > 0f ? availableHeight / neededHeight : 1f;

        float scale = Mathf.Min(scaleX, scaleY);
        holder.localScale = new Vector3(scale, scale, 1f);
    }

    public static void ClearSpawned<T>(this List<T> items) where T : Component
    {
        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            T item = items[i];
            if (item == null) continue;

            item.gameObject.SetActive(false);
            Object.Destroy(item.gameObject);
        }

        items.Clear();
    }
}
