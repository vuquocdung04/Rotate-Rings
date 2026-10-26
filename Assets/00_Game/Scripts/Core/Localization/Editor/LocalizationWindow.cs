using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ModuleTools.Localization
{
	public class LocalizationWindow : OdinEditorWindow
	{
		[MenuItem("Modules/Localization")]
		static void Open()
		{
			var window = GetWindow<LocalizationWindow>();
			window.titleContent = new GUIContent("Localization");
			window.minSize = new Vector2(820, 480);
		}

		const string ConfigDir = "Assets/00_Game/Configs/Localization";
		const string JsonPath = ConfigDir + "/Resources/" + LocalizationManager.RESOURCE_PATH + ".json";
		const string KeysPath = "Assets/00_Game/Scripts/Utils/Constants/LocalizationKeys.cs";
		const string Split = "Split";
		const string Left = "Split/Left";
		const string Right = "Split/Right";
		const float LeftWidth = 320f;
		const float LabelWidth = 110f;
		const float ButtonHeight = 26f;
		const int LanguagesPerRow = 3;

		const string SimulateNone = "Không (dùng máy thật)";

		LocalizationConfigSO _config;
		bool _located;
		LocalizationData _output;
		int _outputKeyCount;
		bool _outputLoaded;

		[HorizontalGroup(Split, LeftWidth, MarginRight = 10)]
		[VerticalGroup(Left), PropertyOrder(0)]
		[Title("Config", bold: true)]
		[ShowInInspector, LabelText("Asset"), LabelWidth(50)]
		LocalizationConfigSO Config
		{
			get
			{
				if (_config == null && !_located)
				{
					_located = true;
					_config = Locate();
				}

				return _config;
			}
			set
			{
				_config = value;
				_located = true;
			}
		}

		[VerticalGroup(Left), PropertyOrder(1)]
		[ShowIf(nameof(HasNoConfig))]
		[InfoBox("Chưa có LocalizationConfigSO — tạo để nhập link Google Sheet.", InfoMessageType.Warning)]
		[Button("Tạo LocalizationConfigSO", ButtonHeight = 28), GUIColor(0.4f, 0.9f, 0.5f)]
		void CreateConfig()
		{
			Directory.CreateDirectory(ConfigDir);

			LocalizationConfigSO created = ScriptableObject.CreateInstance<LocalizationConfigSO>();
			string path = AssetDatabase.GenerateUniqueAssetPath($"{ConfigDir}/LocalizationConfigSO.asset");

			AssetDatabase.CreateAsset(created, path);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			_config = created;
			_located = true;
			Selection.activeObject = created;
		}

		bool HasNoConfig => Config == null;

		bool HasConfig => Config != null;

		LocalizationData Output
		{
			get
			{
				if (_outputLoaded) return _output;

				_outputLoaded = true;
				_output = LocalizationSheetImporter.ReadOutput(JsonPath);
				_outputKeyCount = _output == null
					? 0
					: _output.texts.Values.SelectMany(table => table.Keys).Distinct().Count();

				return _output;
			}
		}

		[VerticalGroup(Left), OnInspectorGUI, PropertyOrder(2)]
		void DrawLeft()
		{
			DrawSync();
			DrawOutput();
		}

		[VerticalGroup(Right), OnInspectorGUI, PropertyOrder(10)]
		void DrawRight()
		{
			DrawLive();

			if (Application.isPlaying) GUIHelper.RequestRepaint();
		}

		[VerticalGroup(Right), PropertyOrder(11)]
		[Title("Thông số", bold: true)]
		[ShowInInspector, HideLabel, ShowIf(nameof(HasConfig))]
		[InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
		LocalizationConfigSO ConfigEditor
		{
			get => Config;
			set => Config = value;
		}

		[VerticalGroup(Right), OnInspectorGUI, PropertyOrder(12)]
		void DrawFormat()
		{
			SirenixEditorGUI.Title("Format sheet", null, TextAlignment.Left, true);
			SirenixEditorGUI.InfoMessageBox(
				"Dòng 1: Key | English | Vietnamese | Japanese ...\n" +
				"Tên cột ngôn ngữ = tên enum SystemLanguage để tự nhận theo máy (Vietnamese, ChineseSimplified...).\n" +
				"Cột/key rỗng hoặc bắt đầu bằng # hay // bị bỏ qua — dùng làm ghi chú.\n" +
				"Ô trống = chưa dịch, game tự lấy bản fallback.\n" +
				"Tham số dùng {0}, {1} ... gọi LocalizationManager.Get(key, args).");
		}

		void DrawSync()
		{
			SirenixEditorGUI.Title("Google Sheet", null, TextAlignment.Left, true);

			string id = LocalizationSheetImporter.ExtractSpreadsheetId(Config != null ? Config.spreadsheetUrl : null);

			using (new EditorGUI.DisabledScope(id.Length == 0))
			{
				if (GUILayout.Button("Mở Google Sheet", GUILayout.Height(ButtonHeight)))
					LocalizationSheetImporter.OpenSheet(Config);
			}

			using (new EditorGUI.DisabledScope(Config == null || Application.isPlaying))
			{
				GUI.color = new Color(0.45f, 0.85f, 0.5f);
				bool clicked = GUILayout.Button(
					new GUIContent("Tải sheet → JSON + Keys", $"Ghi {JsonPath}\nvà sinh {KeysPath}"),
					GUILayout.Height(32));
				GUI.color = Color.white;

				if (clicked)
				{
					LocalizationConfigSO config = Config;
					EditorApplication.delayCall += () =>
					{
						if (LocalizationSheetImporter.Import(config, JsonPath, KeysPath)) _outputLoaded = false;
					};
				}
			}

			if (Application.isPlaying) GUILayout.Label("Thoát Play để tải sheet (sinh keys sẽ recompile).", EditorStyles.miniLabel);
		}

		void DrawOutput()
		{
			SirenixEditorGUI.Title("Dữ liệu đã xuất", null, TextAlignment.Left, true);

			LocalizationData output = Output;

			if (output == null)
			{
				SirenixEditorGUI.WarningMessageBox($"Chưa có {JsonPath}.");
				return;
			}

			Stat("Fallback", output.fallback);
			Stat("Tổng key", _outputKeyCount.ToString());

			foreach (string language in output.languages)
			{
				int count = output.texts.TryGetValue(language, out var table) ? table.Count : 0;
				Stat(language, count >= _outputKeyCount ? count.ToString() : $"{count} / {_outputKeyCount} (thiếu {_outputKeyCount - count})");
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Ping JSON", GUILayout.Height(ButtonHeight)))
				EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath));
			if (GUILayout.Button("Đọc lại", GUILayout.Height(ButtonHeight)))
				_outputLoaded = false;
			GUILayout.EndHorizontal();
		}

		void DrawLive()
		{
			SirenixEditorGUI.BeginBox();
			SirenixEditorGUI.BeginBoxHeader();
			GUILayout.Label("Live", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			GUILayout.Label(Application.isPlaying ? "Play Mode" : "chưa vào Play", EditorStyles.miniLabel);
			SirenixEditorGUI.EndBoxHeader();

			DrawSimulate();

			GUILayout.Space(4);

			if (!Application.isPlaying)
			{
				Stat("Đã chọn tay", LocalizationManager.HasUserChoice ? GamePrefs.Get<string>(StringHelper.LANGUAGE) : "chưa");
				DrawClearChoice();
				SirenixEditorGUI.InfoMessageBox("Vào Play để xem và đổi ngôn ngữ đang chạy.");
				SirenixEditorGUI.EndBox();
				return;
			}

			SystemLanguage device = LocalizationManager.DeviceLanguage;

			Stat("Đang dùng", LocalizationManager.Language);
			Stat("Máy", $"{device} → {LocalizationManager.FromSystemLanguage(device) ?? "không hỗ trợ, dùng fallback"}");
			Stat("Nguồn", LocalizationManager.HasUserChoice ? "user tự chọn" : "theo máy");

			GUILayout.Space(4);

			var languages = LocalizationManager.Languages;

			for (int i = 0; i < languages.Count; i += LanguagesPerRow)
			{
				GUILayout.BeginHorizontal();

				for (int j = i; j < Mathf.Min(i + LanguagesPerRow, languages.Count); j++)
				{
					string language = languages[j];
					bool active = language == LocalizationManager.Language;

					GUI.color = active ? new Color(0.45f, 0.85f, 0.5f) : Color.white;
					if (GUILayout.Button(language, GUILayout.Height(ButtonHeight)) && !active)
						LocalizationManager.SetLanguage(language);
					GUI.color = Color.white;
				}

				GUILayout.EndHorizontal();
			}

			DrawClearChoice();
			SirenixEditorGUI.EndBox();
		}

		void DrawSimulate()
		{
			string[] options = SimulateOptions();
			string current = EditorPrefs.GetString(LocalizationManager.SIMULATED_DEVICE_LANGUAGE_PREF, "");
			int index = Mathf.Max(0, Array.IndexOf(options, current));

			int next = EditorGUILayout.Popup(
				new GUIContent("Giả lập máy", "Chỉ trong Editor. Có tác dụng khi chưa chọn tay — bấm 'Bỏ lựa chọn' rồi vào Play."),
				index,
				options);

			if (next == index) return;

			if (next == 0) EditorPrefs.DeleteKey(LocalizationManager.SIMULATED_DEVICE_LANGUAGE_PREF);
			else EditorPrefs.SetString(LocalizationManager.SIMULATED_DEVICE_LANGUAGE_PREF, options[next]);

			if (Application.isPlaying && !LocalizationManager.HasUserChoice) LocalizationManager.ClearUserChoice();
		}

		string[] SimulateOptions()
		{
			var languages = Output != null ? Output.languages : new List<string>();

			return new[] { SimulateNone }
				.Concat(languages.Where(language => Enum.TryParse(language, out SystemLanguage _)))
				.ToArray();
		}

		void DrawClearChoice()
		{
			GUI.color = new Color(0.95f, 0.65f, 0.4f);
			bool clicked = GUILayout.Button(
				new GUIContent("Bỏ lựa chọn, theo máy", "Xóa key LANGUAGE — mô phỏng user chưa từng chọn ngôn ngữ."),
				GUILayout.Height(ButtonHeight));
			GUI.color = Color.white;

			if (clicked) LocalizationManager.ClearUserChoice();
		}

		static void Stat(string label, string value)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(LabelWidth));
			GUILayout.Label(value, EditorStyles.boldLabel);
			GUILayout.EndHorizontal();
		}

		static LocalizationConfigSO Locate()
		{
			string guid = AssetDatabase.FindAssets("t:LocalizationConfigSO").FirstOrDefault();

			return string.IsNullOrEmpty(guid)
				? null
				: AssetDatabase.LoadAssetAtPath<LocalizationConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
		}
	}
}
