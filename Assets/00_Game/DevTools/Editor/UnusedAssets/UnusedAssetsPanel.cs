using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DevTools.UnusedAssets
{
	[System.Serializable]
	public class UnusedAssetsPanel : IDevToolPanel
	{
		public string Title => "Unused Assets";
		public int Order => 10;
		public SdfIconType Icon => SdfIconType.Trash;

		[Title("Cấu hình", bold: true)]
		[FolderPath(RequireExistingPath = true)]
		[LabelText("Folder"), LabelWidth(80)]
		public string targetFolder;

		[EnumToggleButtons, LabelText("Type"), LabelWidth(80)]
		public AssetTypeFilter assetType = AssetTypeFilter.Mesh;

		[ToggleLeft, LabelText("Subfolders")]
		public bool includeSubfolders = true;

		[HorizontalGroup("Actions"), PropertyOrder(5)]
		[Button("QUÉT", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.5f)]
		public void Scan()
		{
			if (string.IsNullOrEmpty(targetFolder) || !AssetDatabase.IsValidFolder(targetFolder))
			{
				EditorUtility.DisplayDialog("Lỗi", "Folder không hợp lệ!", "OK");
				return;
			}

			unusedAssets.Clear();

			string[] guids = AssetDatabase.FindAssets(GetFilter(assetType), new[] { targetFolder });
			var candidates = new HashSet<string>();
			foreach (var g in guids)
			{
				string p = AssetDatabase.GUIDToAssetPath(g);
				if (!includeSubfolders)
				{
					string dir = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
					if (dir != targetFolder) continue;
				}

				if (AssetDatabase.IsValidFolder(p)) continue;
				candidates.Add(p);
			}

			if (candidates.Count == 0)
			{
				EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy asset nào.", "OK");
				return;
			}

			var referenced = new HashSet<string>();
			var allPaths = AssetDatabase.GetAllAssetPaths()
				.Where(p => p.StartsWith("Assets/") || p.StartsWith("Packages/"))
				.Where(p => !candidates.Contains(p))
				.ToArray();

			try
			{
				int total = allPaths.Length;
				for (int i = 0; i < total; i++)
				{
					if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(
						"Đang quét tham chiếu...", $"{i}/{total}", (float)i / total))
					{
						return;
					}

					var deps = AssetDatabase.GetDependencies(allPaths[i], false);
					foreach (var d in deps)
						if (candidates.Contains(d)) referenced.Add(d);
				}
			}
			finally { EditorUtility.ClearProgressBar(); }

			long totalBytes = 0;
			foreach (var path in candidates.OrderBy(x => x))
			{
				if (referenced.Contains(path)) continue;

				var info = new System.IO.FileInfo(path);
				long bytes = info.Exists ? info.Length : 0;
				totalBytes += bytes;

				unusedAssets.Add(new UnusedAssetInfo
				{
					asset = AssetDatabase.LoadMainAssetAtPath(path),
					path = path,
					size = FormatBytes(bytes)
				});
			}

			totalSize = FormatBytes(totalBytes);

			Debug.Log($"<color=cyan>[Unused Assets]</color> Tìm thấy " +
			          $"<b>{unusedAssets.Count}/{candidates.Count}</b> assets không sử dụng. " +
			          $"Tiết kiệm được: <b>{totalSize}</b>");
		}

		[HorizontalGroup("Actions"), PropertyOrder(5)]
		[Button("Xóa tất cả", ButtonSizes.Large), GUIColor(1f, 0.4f, 0.4f)]
		[ShowIf("@unusedAssets != null && unusedAssets.Count > 0")]
		public void DeleteAll()
		{
			if (!EditorUtility.DisplayDialog("Xác nhận xóa",
				$"Xóa {unusedAssets.Count} assets ({totalSize})?\nKhông thể hoàn tác!",
				"Xóa", "Hủy")) return;

			var paths = unusedAssets.Where(u => u.asset != null).Select(u => u.path).ToArray();
			AssetDatabase.DeleteAssets(paths, new List<string>());
			AssetDatabase.Refresh();

			Debug.Log($"<color=red>[Unused Assets]</color> Đã xóa {paths.Length} assets.");
			unusedAssets.Clear();
			totalSize = "0 B";
		}

		[HorizontalGroup("Actions", MaxWidth = 90), PropertyOrder(5)]
		[Button("Clear", ButtonSizes.Large), GUIColor(0.7f, 0.7f, 0.7f)]
		[ShowIf("@unusedAssets != null && unusedAssets.Count > 0")]
		public void Clear()
		{
			unusedAssets.Clear();
			totalSize = "0 B";
		}

		[Title("Kết quả", bold: true), PropertyOrder(6)]
		[ShowInInspector, ReadOnly, LabelText("Có thể xóa"), LabelWidth(80)]
		public string totalSize = "0 B";

		[PropertyOrder(7)]
		[ShowInInspector, HideLabel, Searchable]
		[TableList(IsReadOnly = true, ShowPaging = true, ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
		public List<UnusedAssetInfo> unusedAssets = new List<UnusedAssetInfo>();

		public enum AssetTypeFilter
		{
			Mesh, Texture, Material, AudioClip,
			Prefab, Animation, ScriptableObject, All
		}

		[System.Serializable]
		public class UnusedAssetInfo
		{
			[HideInTables]
			public Object asset;

			[HideInTables]
			public string path;

			[TableColumnWidth(90, false)]
			[ReadOnly, HideLabel]
			public string size;

			[OnInspectorGUI, PropertyOrder(8)]
			void Asset()
			{
				GUILayout.BeginHorizontal();
				var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
				var tex = DevToolUi.Thumbnail(asset);
				if (tex != null)
				{
					GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
				}

				GUILayout.Label(DevToolUi.RichPath(path), DevToolUi.PathStyle);
				GUILayout.EndHorizontal();
			}

			[Button("Ping"), TableColumnWidth(55, false), PropertyOrder(9)]
			public void Ping()
			{
				if (asset != null)
				{
					EditorUtility.FocusProjectWindow();
					Selection.activeObject = asset;
					EditorGUIUtility.PingObject(asset);
				}
			}
		}

		string GetFilter(AssetTypeFilter t) => t switch
		{
			AssetTypeFilter.Mesh => "t:Mesh",
			AssetTypeFilter.Texture => "t:Texture",
			AssetTypeFilter.Material => "t:Material",
			AssetTypeFilter.AudioClip => "t:AudioClip",
			AssetTypeFilter.Prefab => "t:Prefab",
			AssetTypeFilter.Animation => "t:AnimationClip",
			AssetTypeFilter.ScriptableObject => "t:ScriptableObject",
			_ => ""
		};

		string FormatBytes(long bytes)
		{
			string[] s = { "B", "KB", "MB", "GB" };
			double size = bytes;
			int i = 0;
			while (size >= 1024 && i < s.Length - 1) { size /= 1024; i++; }
			return $"{size:0.##} {s[i]}";
		}
	}
}
