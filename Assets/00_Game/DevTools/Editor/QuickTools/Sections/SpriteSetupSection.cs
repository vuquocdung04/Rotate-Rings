using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DevTools.QuickTools
{
	[System.Serializable]
	public class SpriteSetupSection
	{
		[TitleGroup("Sprite Setup", BoldTitle = true), PropertyOrder(0)]
		[HorizontalGroup("Sprite Setup/Row", 350)]
		[FolderPath(RequireExistingPath = true), LabelText("Folder"), LabelWidth(50)]
		public string spriteFolder;

		[HorizontalGroup("Sprite Setup/Row", Width = 170), PropertyOrder(1)]
		[Button("Scan", ButtonHeight = 22)]
		[GUIColor(0.4f, 0.9f, 0.5f)]
		void ScanSprites()
		{
			if (string.IsNullOrEmpty(spriteFolder) || !AssetDatabase.IsValidFolder(spriteFolder))
			{
				EditorUtility.DisplayDialog("Lỗi", "Folder không hợp lệ!", "OK");
				return;
			}

			spriteCandidates.Clear();

			foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { spriteFolder }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
				{
					continue;
				}

				if (importer.textureType == TextureImporterType.Sprite)
				{
					continue;
				}

				spriteCandidates.Add(new SpriteSetupItem
				{
					path = path,
					asset = AssetDatabase.LoadMainAssetAtPath(path),
					currentType = importer.textureType.ToString()
				});
			}

			if (spriteCandidates.Count == 0)
			{
				EditorUtility.DisplayDialog("Thông báo", "Không có texture nào cần đổi (tất cả đã là Sprite).", "OK");
			}
		}

		[TitleGroup("Sprite Setup"), PropertyOrder(2)]
		[ShowInInspector, HideLabel]
		[ShowIf("@spriteCandidates != null && spriteCandidates.Count > 0")]
		[TableList(IsReadOnly = true, ShowPaging = true, ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
		public List<SpriteSetupItem> spriteCandidates = new List<SpriteSetupItem>();

		[TitleGroup("Sprite Setup"), PropertyOrder(3)]
		[ShowIf("@spriteCandidates != null && spriteCandidates.Count > 0")]
		[Button("$ApplyLabel", ButtonHeight = 26)]
		[GUIColor(0.45f, 0.7f, 1f)]
		void ApplySprites()
		{
			int done = 0;
			int index = 0;

			try
			{
				foreach (var item in spriteCandidates)
				{
					EditorUtility.DisplayProgressBar("Đang set Sprite...", item.path, (float)index++ / spriteCandidates.Count);

					if (AssetImporter.GetAtPath(item.path) is not TextureImporter importer)
					{
						continue;
					}

					if (importer.textureType == TextureImporterType.Sprite)
					{
						continue;
					}

					importer.textureType = TextureImporterType.Sprite;
					importer.spriteImportMode = SpriteImportMode.Single;
					importer.SaveAndReimport();
					done++;
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			Debug.Log($"<color=cyan>[Quick Tools]</color> Đã set Sprite (2D and UI) / Single cho <b>{done}</b> texture trong '{spriteFolder}'.");
			spriteCandidates.Clear();
		}

		string ApplyLabel => $"Apply {spriteCandidates.Count} textures";

		[System.Serializable]
		public class SpriteSetupItem
		{
			[HideInTables]
			public string path;

			[HideInTables]
			public Object asset;

			[TableColumnWidth(110, false)]
			[ReadOnly, HideLabel]
			public string currentType;

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
		}
	}
}
