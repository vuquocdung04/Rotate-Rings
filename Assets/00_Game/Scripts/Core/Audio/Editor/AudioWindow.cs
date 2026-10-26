using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ModuleTools.Audio
{
	public class AudioWindow : OdinEditorWindow
	{
		[MenuItem("Modules/Audio")]
		static void Open()
		{
			var window = GetWindow<AudioWindow>();
			window.titleContent = new GUIContent("Audio");
			window.minSize = new Vector2(900, 520);
		}

		const string CatalogFolder = "Assets/00_Game/Configs/Audio";
		const string KeysPath = "Assets/00_Game/Scripts/Utils/Constants/AudioKeys.cs";
		const string Split = "Split";
		const string Left = "Split/Left";
		const string Right = "Split/Right";
		const float LeftWidth = 300f;
		const float ButtonHeight = 26f;

		AudioCatalogSO _catalog;
		bool _located;
		List<AudioEntry> _rows;
		List<AudioCatalogSO> _catalogs;

		List<AudioCatalogSO> Catalogs => _catalogs ??= AudioKeysGenerator.LocateAll();

		void InvalidateCatalogs() => _catalogs = null;

		[HorizontalGroup(Split, LeftWidth, MarginRight = 10)]
		[VerticalGroup(Left), PropertyOrder(0)]
		[Title("Catalog", bold: true)]
		[ShowInInspector, LabelText("Asset"), LabelWidth(70)]
		[OnValueChanged(nameof(SyncRows))]
		AudioCatalogSO Catalog
		{
			get
			{
				if (_catalog == null && !_located)
				{
					_located = true;
					_catalog = Catalogs.FirstOrDefault();
					SyncRows();
				}

				return _catalog;
			}
			set
			{
				_catalog = value;
				_located = true;
				InvalidateCatalogs();
			}
		}

		[VerticalGroup(Left), PropertyOrder(1)]
		[InfoBox("Chưa có AudioCatalogSO. Bấm 'Tạo Catalog' để tạo trong Configs/Audio.",
			InfoMessageType.Warning, VisibleIf = "@Catalog == null")]
		[Button("Tạo Catalog", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.5f)]
		[ShowIf("@Catalog == null")]
		void CreateCatalog()
		{
			Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), CatalogFolder));
			AssetDatabase.Refresh();

			AudioCatalogSO created = ScriptableObject.CreateInstance<AudioCatalogSO>();
			string path = AssetDatabase.GenerateUniqueAssetPath($"{CatalogFolder}/AudioCatalogSO.asset");

			AssetDatabase.CreateAsset(created, path);
			AssetDatabase.SaveAssets();

			Catalog = created;
			InvalidateCatalogs();
			SyncRows();
			EditorGUIUtility.PingObject(created);
		}

		[VerticalGroup(Left), OnInspectorGUI, PropertyOrder(2)]
		void DrawActions()
		{
			if (Catalog == null) return;

			SyncRows();

			SirenixEditorGUI.Title("Danh sách", null, TextAlignment.Left, true);

			if (GUILayout.Button("＋ Thêm audio", GUILayout.Height(ButtonHeight)))
			{
				Undo.RecordObject(Catalog, "Add Audio");
				_rows.Add(new AudioEntry());
				EditorUtility.SetDirty(Catalog);
			}

			SirenixEditorGUI.Title("Sinh code", null, TextAlignment.Left, true);

			GUI.color = new Color(0.45f, 0.85f, 0.5f);
			if (GUILayout.Button("Generate Keys", GUILayout.Height(ButtonHeight)))
			{
				InvalidateCatalogs();
				AudioKeysGenerator.Generate(Catalogs, KeysPath);
			}
			GUI.color = Color.white;

			if (GUILayout.Button("Mở AudioKeys", GUILayout.Height(ButtonHeight))) OpenScript(KeysPath);
		}

		[VerticalGroup(Right), PropertyOrder(11)]
		[Title("Danh sách Audio", bold: true)]
		[ShowInInspector, HideLabel, Searchable]
		[ShowIf("@Catalog != null")]
		[TableList(AlwaysExpanded = true, DrawScrollView = true, MinScrollViewHeight = 160, MaxScrollViewHeight = 440,
			ShowIndexLabels = true)]
		List<AudioEntry> Rows
		{
			get => _rows;
			set => _rows = value;
		}

		[VerticalGroup(Right), OnInspectorGUI, PropertyOrder(12)]
		void DrawValidation()
		{
			if (Catalog == null) return;

			SyncRows();

			List<AudioCatalogSO> catalogs = Catalogs;
			List<string> errors = AudioKeysGenerator.Validate(catalogs);

			var noClip = new List<string>();

			foreach (AudioEntry entry in _rows)
			{
				if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
				if (entry.variants == null || entry.variants.Count == 0 || entry.variants.All(c => c == null))
					noClip.Add(entry.key);
			}

			GUILayout.Space(4);

			foreach (string error in errors)
			{
				SirenixEditorGUI.ErrorMessageBox(error);
			}

			if (noClip.Count > 0) SirenixEditorGUI.WarningMessageBox($"Chưa có clip: {string.Join(", ", noClip)}");

			if (errors.Count == 0 && noClip.Count == 0 && _rows.Count > 0)
			{
				int total = catalogs.Sum(c => c.audioConfigs.Count);
				SirenixEditorGUI.InfoMessageBox($"{total} audio trong {catalogs.Count} catalog, không có lỗi.");
			}

			if (GUI.changed) EditorUtility.SetDirty(Catalog);
		}

		[VerticalGroup(Right), OnInspectorGUI, PropertyOrder(13)]
		void DrawCodeGenInfo()
		{
			if (Catalog == null) return;

			SirenixEditorGUI.Title("File sinh ra", null, TextAlignment.Left, true);
			PathRow("AudioKeys.cs", KeysPath);
		}

		static void OpenScript(string path)
		{
			var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

			if (asset != null) AssetDatabase.OpenAsset(asset);
			else Debug.LogError($"[Audio] Chưa có file {path}.");
		}

		void PathRow(string label, string path)
		{
			bool exists = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path));

			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(170));

			GUI.color = exists ? new Color(0.55f, 0.9f, 0.6f) : new Color(0.75f, 0.75f, 0.75f);
			GUILayout.Label(exists ? "đã có" : "chưa có", EditorStyles.miniLabel, GUILayout.Width(55));
			GUI.color = Color.white;

			EditorGUILayout.SelectableLabel(path, EditorStyles.miniLabel, GUILayout.Height(16));
			GUILayout.EndHorizontal();
		}

		void SyncRows()
		{
			if (_catalog == null)
			{
				_rows = null;
				return;
			}

			if (!ReferenceEquals(_rows, _catalog.audioConfigs)) _rows = _catalog.audioConfigs;
		}
	}
}
