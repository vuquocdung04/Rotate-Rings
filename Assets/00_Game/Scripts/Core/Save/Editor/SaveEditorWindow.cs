using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ModuleTools.Save
{
	public class SaveEditorWindow : OdinEditorWindow
	{
		[MenuItem("Modules/Save Editor")]
		static void Open()
		{
			var window = GetWindow<SaveEditorWindow>();
			window.titleContent = new GUIContent("Save Editor");
			window.minSize = new Vector2(1060, 480);
		}

		const float LeftWidth = 220f;
		const float ProfileWidth = 360f;
		const float ButtonHeight = 26f;

		PropertyInfo[] _profileProps;
		List<string> _keys;
		Vector2 _rawScroll;

		[OnInspectorGUI]
		void Draw()
		{
			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical(GUILayout.Width(LeftWidth));
			DrawActions();
			GUILayout.EndVertical();

			GUILayout.Space(10);

			GUILayout.BeginVertical();
			DrawRawKeys();
			GUILayout.EndVertical();

			GUILayout.Space(10);

			GUILayout.BeginVertical(GUILayout.Width(ProfileWidth));
			DrawUseProfile();
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
		}

		void DrawRawKeys()
		{
			_keys ??= GamePrefs.Keys.OrderBy(k => k).ToList();

			SirenixEditorGUI.BeginBox();
			SirenixEditorGUI.BeginBoxHeader();
			GUILayout.Label($"PlayerPrefs  ({_keys.Count} key)", EditorStyles.boldLabel);
			SirenixEditorGUI.EndBoxHeader();

			if (_keys.Count == 0)
			{
				SirenixEditorGUI.InfoMessageBox("Chưa có key nào được ghi.");
				SirenixEditorGUI.EndBox();
				return;
			}

			_rawScroll = GUILayout.BeginScrollView(_rawScroll, GUILayout.MaxHeight(420));

			foreach (string key in _keys)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label(key, GUILayout.Width(170));
				EditorGUILayout.SelectableLabel(GamePrefs.PeekRaw(key) ?? "(null)",
					EditorStyles.textField, GUILayout.Height(18));

				if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24)))
				{
					GamePrefs.Delete(key);
					GamePrefs.Flush();
					_keys = null;
					GUIUtility.ExitGUI();
				}
				GUILayout.EndHorizontal();
			}

			GUILayout.EndScrollView();
			SirenixEditorGUI.EndBox();
		}

		void DrawUseProfile()
		{
			SirenixEditorGUI.BeginBox();
			SirenixEditorGUI.BeginBoxHeader();
			GUILayout.Label("UseProfile", EditorStyles.boldLabel);
			SirenixEditorGUI.EndBoxHeader();

			_profileProps ??= typeof(UseProfile)
				.GetProperties(BindingFlags.Public | BindingFlags.Static)
				.Where(p => p.CanRead && p.CanWrite)
				.ToArray();

			float prev = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 150;

			foreach (PropertyInfo prop in _profileProps)
			{
				EditorGUI.BeginChangeCheck();
				object next = DrawProfileField(prop.Name, prop.PropertyType, prop.GetValue(null));
				if (EditorGUI.EndChangeCheck())
				{
					prop.SetValue(null, next);
					GamePrefs.Flush();
					_keys = null;
				}
			}

			EditorGUIUtility.labelWidth = prev;
			SirenixEditorGUI.EndBox();
		}

		object DrawProfileField(string label, Type type, object current)
		{
			if (type == typeof(int)) return EditorGUILayout.IntField(label, current is int i ? i : 0);
			if (type == typeof(bool)) return EditorGUILayout.Toggle(label, current is bool b && b);
			if (type == typeof(float)) return EditorGUILayout.FloatField(label, current is float f ? f : 0f);
			if (type == typeof(string)) return EditorGUILayout.TextField(label, current as string ?? "");

			EditorGUILayout.LabelField(label, current?.ToString() ?? "null");
			return current;
		}

		void DrawActions()
		{
			SirenixEditorGUI.Title("Thao tác", null, TextAlignment.Left, true);

			if (GUILayout.Button("Refresh", GUILayout.Height(ButtonHeight)))
			{
				_keys = null;
			}

			if (GUILayout.Button("Flush", GUILayout.Height(ButtonHeight)))
			{
				GamePrefs.Flush();
				Debug.Log("<color=cyan>[SaveEditor]</color> Đã ghi PlayerPrefs xuống đĩa.");
			}

			GUILayout.Space(10);

			GUI.color = new Color(0.95f, 0.5f, 0.45f);
			if (GUILayout.Button("Delete All", GUILayout.Height(ButtonHeight)))
			{
				DeleteAll();
			}
			GUI.color = Color.white;
		}

		void DeleteAll()
		{
			if (!EditorUtility.DisplayDialog("Xóa toàn bộ save?",
				"Xóa hết key do GamePrefs quản lý. Không hoàn tác!", "Xóa", "Hủy"))
			{
				return;
			}

			GamePrefs.ClearAll();
			_keys = null;
			Debug.Log("<color=red>[SaveEditor]</color> Đã xóa toàn bộ save data.");
		}
	}
}
