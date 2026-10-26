using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace DevTools.QuickTools
{
	[System.Serializable]
	public class SpriteAtlasSection
	{
		[TitleGroup("Sprite Atlas", BoldTitle = true), PropertyOrder(0)]
		[HorizontalGroup("Sprite Atlas/Row", 350)]
		[AssetsOnly, LabelText("Atlas"), LabelWidth(50)]
		public SpriteAtlas atlas;

		[HorizontalGroup("Sprite Atlas/Row", Width = 170), PropertyOrder(1)]
		[Button("Disable Compression", ButtonHeight = 22)]
		[GUIColor(0.45f, 0.7f, 1f)]
		void DisableSourceCompression()
		{
			if (atlas == null)
			{
				EditorUtility.DisplayDialog("Lỗi", "Kéo SpriteAtlas vào trước!", "OK");
				return;
			}

			var texturePaths = new HashSet<string>();
			var packables = UnityEditor.U2D.SpriteAtlasExtensions.GetPackables(atlas);
			foreach (var obj in packables)
			{
				string p = AssetDatabase.GetAssetPath(obj);
				if (string.IsNullOrEmpty(p))
				{
					continue;
				}

				if (AssetDatabase.IsValidFolder(p))
				{
					foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { p }))
					{
						texturePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
					}
				}
				else
				{
					texturePaths.Add(p);
				}
			}

			if (texturePaths.Count == 0)
			{
				EditorUtility.DisplayDialog("Thông báo", "Atlas không có packable nào.", "OK");
				return;
			}

			if (!EditorUtility.DisplayDialog("Xác nhận",
				$"Set Compression = None cho {texturePaths.Count} texture trong atlas '{atlas.name}'?",
				"OK", "Hủy"))
			{
				return;
			}

			changedTextures.Clear();
			int index = 0;

			try
			{
				foreach (var path in texturePaths)
				{
					EditorUtility.DisplayProgressBar("Đang xử lý...", path, (float)index++ / texturePaths.Count);

					if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
					{
						continue;
					}

					bool changed = false;

					if (importer.textureCompression != TextureImporterCompression.Uncompressed)
					{
						importer.textureCompression = TextureImporterCompression.Uncompressed;
						changed = true;
					}

					if (importer.crunchedCompression)
					{
						importer.crunchedCompression = false;
						changed = true;
					}

					foreach (var platform in new[] { "Android", "iPhone", "Standalone", "WebGL" })
					{
						var settings = importer.GetPlatformTextureSettings(platform);
						if (settings.overridden)
						{
							settings.overridden = false;
							importer.SetPlatformTextureSettings(settings);
							changed = true;
						}
					}

					if (changed)
					{
						importer.SaveAndReimport();
						changedTextures.Add(path);
					}
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			Debug.Log($"<color=cyan>[Quick Tools]</color> Đã tắt compression cho <b>{changedTextures.Count}/{texturePaths.Count}</b> texture trong atlas '{atlas.name}'.");
		}

		[TitleGroup("Sprite Atlas"), PropertyOrder(2)]
		[ShowInInspector, LabelText("Đã đổi")]
		[ShowIf("@changedTextures != null && changedTextures.Count > 0")]
		[ListDrawerSettings(ShowFoldout = true, NumberOfItemsPerPage = 15, IsReadOnly = true)]
		public List<string> changedTextures = new List<string>();
	}
}
