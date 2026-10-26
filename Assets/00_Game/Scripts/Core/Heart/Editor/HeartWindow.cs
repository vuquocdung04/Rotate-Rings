using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ModuleTools.Heart
{
	public class HeartWindow : OdinEditorWindow
	{
		[MenuItem("Modules/Heart")]
		static void Open()
		{
			var window = GetWindow<HeartWindow>();
			window.titleContent = new GUIContent("Heart");
			window.minSize = new Vector2(760, 440);
		}

		const string BypassKey = "DevTools.Heart.BypassHeartCost";
		const string ConfigDir = "Assets/00_Game/Configs/Heart/Resources";
		const string Split = "Split";
		const string Left = "Split/Left";
		const string Right = "Split/Right";
		const float LeftWidth = 300f;
		const float LabelWidth = 110f;
		const float ButtonHeight = 26f;

		HeartConfigSO _config;
		bool _located;
		float _unlimitedMinutes = 1f;

		static HeartManager Live
		{
			get
			{
				if (!Application.isPlaying) return null;

				HeartManager manager = HeartManager.Instance;
				return manager != null ? manager : null;
			}
		}

		[InitializeOnLoadMethod]
		static void Hook()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		static void OnPlayModeChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredPlayMode)
			{
				HeartManager.BypassHeartCost = EditorPrefs.GetBool(BypassKey, false);
			}
		}

		[HorizontalGroup(Split, LeftWidth, MarginRight = 10)]
		[VerticalGroup(Left), PropertyOrder(0)]
		[Title("Config", bold: true)]
		[ShowInInspector, LabelText("Asset"), LabelWidth(50)]
		HeartConfigSO Config
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
		[InfoBox("Chưa có HeartConfigSO — game đang chạy bằng giá trị mặc định (5 tim / 30 phút).", InfoMessageType.Warning)]
		[Button("Tạo HeartConfigSO", ButtonHeight = 28), GUIColor(0.4f, 0.9f, 0.5f)]
		void CreateConfig()
		{
			Directory.CreateDirectory(ConfigDir);

			HeartConfigSO created = ScriptableObject.CreateInstance<HeartConfigSO>();
			string path = AssetDatabase.GenerateUniqueAssetPath($"{ConfigDir}/{HeartConfigSO.RESOURCE_PATH}.asset");

			AssetDatabase.CreateAsset(created, path);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			_config = created;
			_located = true;
			Selection.activeObject = created;
		}

		bool HasNoConfig => Config == null;

		bool HasConfig => Config != null;

		[VerticalGroup(Left), OnInspectorGUI, PropertyOrder(2)]
		void DrawLeft()
		{
			HeartManager live = Live;

			DrawTools(live);
			DrawMaintenance();
		}

		[VerticalGroup(Right), OnInspectorGUI, PropertyOrder(10)]
		void DrawRight()
		{
			DrawLive(Live);

			if (Application.isPlaying) GUIHelper.RequestRepaint();
		}

		[VerticalGroup(Right), PropertyOrder(11)]
		[Title("Thông số", bold: true)]
		[ShowInInspector, HideLabel, ShowIf(nameof(HasConfig))]
		[InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
		HeartConfigSO ConfigEditor
		{
			get => Config;
			set => Config = value;
		}

		void DrawLive(HeartManager live)
		{
			SirenixEditorGUI.BeginBox();
			SirenixEditorGUI.BeginBoxHeader();
			GUILayout.Label("Live", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			GUILayout.Label(live != null ? "Play Mode" : "chưa vào Play", EditorStyles.miniLabel);
			SirenixEditorGUI.EndBoxHeader();

			if (live == null)
			{
				SirenixEditorGUI.InfoMessageBox("Vào Play để xem và chỉnh tim đang chạy.");
				SirenixEditorGUI.EndBox();
				return;
			}

			bool mismatch = live.Config != null && Config != null && live.Config != Config;
			if (mismatch)
			{
				SirenixEditorGUI.WarningMessageBox(
					$"Đang chạy bằng '{live.Config.name}', khác asset đang chọn bên trái. Sửa thông số không ăn vào game.");
			}

			Stat("Tim", $"{live.CurrentHeart} / {live.MaxHearts}");
			Stat("Unlimited", live.IsUnlimited
				? $"còn {TimeManager.Format(live.GetUnlimitedTimeRemaining())}"
				: "tắt");
			Stat("Tới tim kế", live.IsFull || live.IsUnlimited
				? "—"
				: TimeManager.Format(TimeSpan.FromSeconds(live.GetTimeToNextHeart())));
			Stat("Tới khi đầy", live.IsFull || live.IsUnlimited
				? "—"
				: TimeManager.Format(TimeSpan.FromSeconds(live.GetTimeToFull())));

			GUILayout.Space(2);

			Stat("Lần đầu chơi", live.WasFirstPlay ? "có" : "không");
			Stat("Comeback", live.WasComebackReward ? $"có (vắng {live.HoursSinceLastLogin:0.0}h)" : $"không (vắng {live.HoursSinceLastLogin:0.0}h)");

			SirenixEditorGUI.EndBox();
		}

		void DrawTools(HeartManager live)
		{
			SirenixEditorGUI.Title("Điều khiển", null, TextAlignment.Left, true);

			using (new EditorGUI.DisabledScope(live == null))
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("-1", GUILayout.Height(ButtonHeight))) live.SetHearts(live.CurrentHeart - 1);
				if (GUILayout.Button("+1", GUILayout.Height(ButtonHeight))) live.SetHearts(live.CurrentHeart + 1);
				if (GUILayout.Button("Hết tim", GUILayout.Height(ButtonHeight))) live.SetHearts(0);

				GUI.color = new Color(0.45f, 0.85f, 0.5f);
				if (GUILayout.Button("Đổ đầy", GUILayout.Height(ButtonHeight))) live.Refill();
				GUI.color = Color.white;
				GUILayout.EndHorizontal();

				GUILayout.Space(4);

				GUILayout.BeginHorizontal();
				GUILayout.Label("Unlimited", GUILayout.Width(70));
				_unlimitedMinutes = Mathf.Max(0f, EditorGUILayout.FloatField(_unlimitedMinutes, GUILayout.Width(60)));
				GUILayout.Label("phút");
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Cộng thêm", GUILayout.Height(ButtonHeight))) live.TryAddUnlimited(_unlimitedMinutes);
				if (GUILayout.Button("Tắt", GUILayout.Height(ButtonHeight))) live.ClearUnlimited();
				GUILayout.EndHorizontal();
			}

			if (live == null) GUILayout.Label("Vào Play để dùng các nút trên.", EditorStyles.miniLabel);

			GUILayout.Space(6);

			bool bypass = EditorPrefs.GetBool(BypassKey, false);
			bool next = EditorGUILayout.ToggleLeft(
				new GUIContent("Chơi không trừ tim", "Chỉ chạy trong Editor. Thoát Play vẫn nhớ lựa chọn."),
				bypass);

			if (next != bypass)
			{
				EditorPrefs.SetBool(BypassKey, next);
				HeartManager.BypassHeartCost = next;
			}
		}

		void DrawMaintenance()
		{
			SirenixEditorGUI.Title("Save", null, TextAlignment.Left, true);

			GUI.color = new Color(0.95f, 0.65f, 0.4f);
			bool clicked = GUILayout.Button(
				new GUIContent("Xóa save tim", "Mô phỏng lần đầu chơi."),
				GUILayout.Height(ButtonHeight));
			GUI.color = Color.white;

			if (clicked && EditorUtility.DisplayDialog(
					"Xóa save tim",
					"Xóa HEART, IS_UNLIMITER_HEART, TIME_UNLIMITER_HEART, TIME_LAST_OVER_HEART, LAST_TIME_LOGIN.\n\nVào lại Play để chạy nhánh first play.",
					"Xóa", "Hủy"))
			{
				HeartManager.ClearSave();
			}
		}

		static void Stat(string label, string value)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(LabelWidth));
			GUILayout.Label(value, EditorStyles.boldLabel);
			GUILayout.EndHorizontal();
		}

		static HeartConfigSO Locate()
		{
			string guid = AssetDatabase.FindAssets("t:HeartConfigSO").FirstOrDefault();

			return string.IsNullOrEmpty(guid)
				? null
				: AssetDatabase.LoadAssetAtPath<HeartConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
		}
	}
}
