using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ModuleTools.Popup
{
	public class PopupCatalogWindow : OdinEditorWindow
	{
		[MenuItem("Modules/Popup Catalog")]
		static void Open()
		{
			var window = GetWindow<PopupCatalogWindow>();
			window.titleContent = new GUIContent("Popup Catalog");
			window.minSize = new Vector2(720, 480);
		}

		const string PopupCatalogFolder = "Assets/00_Game/Configs/UI/Resources";
		const string Split = "Split";
		const string Left = "Split/Left";
		const string Right = "Split/Right";
		const float LeftWidth = 300f;

		protected override void OnEnable()
		{
			base.OnEnable();

			if (popupCatalog == null) popupCatalog = LocatePopupCatalog();
			SyncPopup();
		}

		[HorizontalGroup(Split, LeftWidth, MarginRight = 10)]
		[VerticalGroup(Left), PropertyOrder(0)]
		[Title("Popup Catalog", bold: true)]
		[LabelText("Catalog"), LabelWidth(70)]
		[OnValueChanged("SyncPopup")]
		public PopupCatalogSO popupCatalog;

		[VerticalGroup(Left), PropertyOrder(1)]
		[InfoBox("Chưa có PopupCatalogSO. Bấm 'Tạo Catalog' để tạo trong Resources.", InfoMessageType.Warning,
			VisibleIf = "@popupCatalog == null")]
		[Button("Tạo Catalog", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.5f)]
		[ShowIf("@popupCatalog == null")]
		void CreatePopupCatalog()
		{
			Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), PopupCatalogFolder));
			AssetDatabase.Refresh();

			var asset = ScriptableObject.CreateInstance<PopupCatalogSO>();
			string path = AssetDatabase.GenerateUniqueAssetPath($"{PopupCatalogFolder}/{PopupCatalogSO.RESOURCE_PATH}.asset");
			AssetDatabase.CreateAsset(asset, path);
			AssetDatabase.SaveAssets();

			popupCatalog = asset;
			SyncPopup();
		}

		[VerticalGroup(Left), PropertyOrder(2)]
		[Button("Đồng bộ popup từ code", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
		[ShowIf("@popupCatalog != null")]
		void SyncPopup()
		{
			if (popupCatalog == null) return;

			List<string> names = TypeCache.GetTypesDerivedFrom<BaseBox>()
				.Where(t => !t.IsAbstract)
				.Select(t => t.Name)
				.ToList();

			popupCatalog.popups.RemoveAll(p => p == null || !names.Contains(p.typeName));

			foreach (string name in names)
			{
				if (popupCatalog.Find(name) == null)
					popupCatalog.popups.Add(new PopupEntry { typeName = name });
			}

			popupCatalog.popups.Sort((a, b) => string.CompareOrdinal(a.typeName, b.typeName));
			popupRows = popupCatalog.popups;

			EditorUtility.SetDirty(popupCatalog);
		}

		[VerticalGroup(Left), PropertyOrder(3)]
		[Title("Test popup", bold: true)]
		[InfoBox("Vào Play mode để test popup.", VisibleIf = "@!UnityEngine.Application.isPlaying")]
		[ShowInInspector, LabelText("Popup"), LabelWidth(70)]
		[ValueDropdown("TestPopupNames")]
		[ShowIf("@popupCatalog != null")]
		string testPopup;

		IEnumerable<string> TestPopupNames() =>
			popupCatalog != null ? popupCatalog.popups.Where(p => p != null).Select(p => p.typeName) : Enumerable.Empty<string>();

		[HorizontalGroup(Left + "/Test", Order = 4), PropertyOrder(4)]
		[Button("Show", ButtonSizes.Medium), GUIColor(0.4f, 0.9f, 0.5f)]
		[EnableIf("@UnityEngine.Application.isPlaying && !string.IsNullOrEmpty(testPopup)")]
		[ShowIf("@popupCatalog != null")]
		void TestShow()
		{
			System.Type type = ResolvePopupType(testPopup);
			if (type == null) return;

			EnsurePopupManager();
			ShowPopup(type).Forget();
		}

		[HorizontalGroup(Left + "/Test", Order = 4), PropertyOrder(5)]
		[Button("Close", ButtonSizes.Medium), GUIColor(1f, 0.5f, 0.5f)]
		[EnableIf("@UnityEngine.Application.isPlaying && !string.IsNullOrEmpty(testPopup)")]
		[ShowIf("@popupCatalog != null")]
		void TestClose()
		{
			System.Type type = ResolvePopupType(testPopup);
			if (type == null) return;

			BaseBox box = PopupManager.Peek(type);
			if (box == null)
			{
				Debug.LogWarning($"[PopupCatalog] {testPopup} chưa được load.");
				return;
			}

			box.Close();
		}

		static async Awaitable ShowPopup(System.Type type)
		{
			BaseBox box = await PopupManager.GetAsync(type);
			if (box != null) box.Show();
		}

		static void EnsurePopupManager()
		{
			if (PopupManager.Instance != null) return;

			PopupManager found = Object.FindFirstObjectByType<PopupManager>(FindObjectsInactive.Include);
			if (found != null)
			{
				if (!found.gameObject.activeSelf) found.gameObject.SetActive(true);
				return;
			}

			new GameObject(nameof(PopupManager)).AddComponent<PopupManager>();
			Debug.Log("[PopupCatalog] Scene chưa có PopupManager, đã tạo mới.");
		}

		static System.Type ResolvePopupType(string typeName)
		{
			System.Type type = TypeCache.GetTypesDerivedFrom<BaseBox>()
				.FirstOrDefault(t => !t.IsAbstract && t.Name == typeName);

			if (type == null) Debug.LogError($"[PopupCatalog] Không tìm thấy type BaseBox '{typeName}'.");
			return type;
		}

		[VerticalGroup(Right), PropertyOrder(10)]
		[Title("Danh sách popup", bold: true)]
		[ShowInInspector, HideLabel, Searchable]
		[TableList(ShowPaging = true, NumberOfItemsPerPage = 25)]
		[ShowIf("@popupCatalog != null")]
		List<PopupEntry> popupRows;

		[VerticalGroup(Right), OnInspectorGUI, PropertyOrder(99)]
		void MarkPopupDirty()
		{
			if (popupCatalog != null && GUI.changed) EditorUtility.SetDirty(popupCatalog);
		}

		static PopupCatalogSO LocatePopupCatalog()
		{
			string guid = AssetDatabase.FindAssets("t:PopupCatalogSO").FirstOrDefault();
			return string.IsNullOrEmpty(guid)
				? null
				: AssetDatabase.LoadAssetAtPath<PopupCatalogSO>(AssetDatabase.GUIDToAssetPath(guid));
		}
	}
}
