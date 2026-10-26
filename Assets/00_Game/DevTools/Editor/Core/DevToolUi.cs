using UnityEngine;

namespace DevTools
{
	public static class DevToolUi
	{
		static GUIStyle _pathStyle;

		public static GUIStyle PathStyle
		{
			get
			{
				if (_pathStyle == null)
				{
					_pathStyle = new GUIStyle(UnityEditor.EditorStyles.label) { richText = true };
				}

				return _pathStyle;
			}
		}

		public static string RichPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return string.Empty;
			}

			int slash = path.LastIndexOf('/');
			if (slash < 0)
			{
				return $"<b><color=#ffffff>{path}</color></b>";
			}

			string dir = path.Substring(0, slash + 1);
			string file = path.Substring(slash + 1);
			return $"<color=#8a8a8a>{dir}</color><b><color=#ffffff>{file}</color></b>";
		}

		public static Texture Thumbnail(Object obj)
		{
			if (obj == null)
			{
				return null;
			}

			Texture tex = UnityEditor.AssetPreview.GetAssetPreview(obj);
			if (tex == null)
			{
				tex = UnityEditor.AssetPreview.GetMiniThumbnail(obj);
			}

			return tex;
		}
	}
}
