using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace DevTools.BuildAudit
{
	[Serializable]
	public class BuildAuditReport
	{
		public string Platform;
		public string BuildDate;
		public long TotalBytes;
		public long OutputBytes;
		public int AssetCount;
		public string Source;
		public List<string> Scenes = new List<string>();

		public List<BuildAuditCategory> Categories = new List<BuildAuditCategory>();
		public List<BuildAuditAsset> Assets = new List<BuildAuditAsset>();
		public List<BuildAuditTexture> Textures = new List<BuildAuditTexture>();
		public List<BuildAuditModel> Models = new List<BuildAuditModel>();

		public string TotalReadable => BuildAuditUtil.HumanSize(TotalBytes);
		public string OutputReadable => BuildAuditUtil.HumanSize(OutputBytes);
	}

	[Serializable]
	public class BuildAuditCategory
	{
		[TableColumnWidth(150, false)]
		[ReadOnly]
		public string Name;

		[TableColumnWidth(90, false)]
		[ReadOnly, HideLabel]
		public string SizeReadable;

		[TableColumnWidth(60, false)]
		[ReadOnly, HideLabel]
		public int Count;

		[ProgressBar(0, 100, ColorGetter = "GetColor")]
		[HideLabel]
		public float PercentOfBuild;

		[HideInTables]
		public long Bytes;

		private Color GetColor()
		{
			return BuildAuditUtil.HeatColor(PercentOfBuild, 40f);
		}
	}

	[Serializable]
	public class BuildAuditAsset
	{
		[TableColumnWidth(90, false)]
		[ReadOnly, HideLabel]
		public string SizeReadable;

		[ProgressBar(0, 100, ColorGetter = "GetColor", Height = 14)]
		[TableColumnWidth(130, false)]
		[HideLabel]
		public float Percent;

		[TableColumnWidth(130, false)]
		[ReadOnly, HideLabel]
		public string Type;

		[HideInTables]
		public string Path;

		[HideInTables]
		public long Bytes;

		[OnInspectorGUI, PropertyOrder(8)]
		private void Asset()
		{
			GUILayout.BeginHorizontal();
			var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
			var tex = BuildAuditUtil.GetThumbnail(Path);
			if (tex != null)
			{
				GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
			}

			GUILayout.Label(BuildAuditUtil.RichPath(Path), BuildAuditUtil.PathStyle);
			GUILayout.EndHorizontal();
		}

		[Button("Ping"), TableColumnWidth(55, false), PropertyOrder(9)]
		private void Ping()
		{
			BuildAuditUtil.Ping(Path);
		}

		private Color GetColor()
		{
			return BuildAuditUtil.HeatColor(Percent, 10f);
		}
	}

	[Serializable]
	public class BuildAuditTexture
	{
		[TableColumnWidth(90, false)]
		[ReadOnly, HideLabel]
		public string SizeReadable;

		[ProgressBar(0, 100, ColorGetter = "GetColor", Height = 14)]
		[TableColumnWidth(110, false)]
		[HideLabel]
		public float Percent;

		[HideInTables]
		public string Dimensions;

		[HideInTables]
		public string Format;

		[HideInTables]
		public int MaxSize;

		[HideInTables]
		public bool Mip;

		[HideInTables]
		public bool Crunch;

		[HideInTables]
		public bool ReadWrite;

		[HideInTables]
		public string Path;

		[HideInTables]
		public long Bytes;

		[OnInspectorGUI, PropertyOrder(8)]
		private void Asset()
		{
			GUILayout.BeginHorizontal();
			var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
			var tex = BuildAuditUtil.GetThumbnail(Path);
			if (tex != null)
			{
				GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
			}

			GUILayout.Label(BuildAuditUtil.RichPath(Path), BuildAuditUtil.PathStyle);
			GUILayout.EndHorizontal();
		}

		[Button("Ping"), TableColumnWidth(55, false), PropertyOrder(9)]
		private void Ping()
		{
			BuildAuditUtil.Ping(Path);
		}

		private Color GetColor()
		{
			return BuildAuditUtil.HeatColor(Percent, 10f);
		}
	}

	[Serializable]
	public class BuildAuditModel
	{
		[TableColumnWidth(90, false)]
		[ReadOnly, HideLabel]
		public string SizeReadable;

		[ProgressBar(0, 100, ColorGetter = "GetColor", Height = 14)]
		[TableColumnWidth(110, false)]
		[HideLabel]
		public float Percent;

		[TableColumnWidth(80, false)]
		[ReadOnly, HideLabel]
		public int Vertices;

		[TableColumnWidth(80, false)]
		[ReadOnly, HideLabel]
		public int Triangles;

		[TableColumnWidth(120, false)]
		[ReadOnly, HideLabel]
		public string MeshCompression;

		[TableColumnWidth(75, false)]
		[ReadOnly, HideLabel]
		public bool ReadWrite;

		[HideInTables]
		public string Path;

		[HideInTables]
		public long Bytes;

		[OnInspectorGUI, PropertyOrder(8)]
		private void Asset()
		{
			GUILayout.BeginHorizontal();
			var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
			var tex = BuildAuditUtil.GetThumbnail(Path);
			if (tex != null)
			{
				GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
			}

			GUILayout.Label(BuildAuditUtil.RichPath(Path), BuildAuditUtil.PathStyle);
			GUILayout.EndHorizontal();
		}

		[Button("Ping"), TableColumnWidth(55, false), PropertyOrder(9)]
		private void Ping()
		{
			BuildAuditUtil.Ping(Path);
		}

		private Color GetColor()
		{
			return BuildAuditUtil.HeatColor(Percent, 10f);
		}
	}

	public static class BuildAuditUtil
	{
		static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

		public static string HumanSize(long bytes)
		{
			if (bytes <= 0)
			{
				return "0 B";
			}

			double size = bytes;
			int unit = 0;
			while (size >= 1024.0 && unit < Units.Length - 1)
			{
				size /= 1024.0;
				unit++;
			}

			return unit == 0
				? $"{bytes} B"
				: $"{size:0.##} {Units[unit]}";
		}

		public static Color HeatColor(float percent, float scale)
		{
			float t = Mathf.Clamp01(percent / scale);
			return Color.Lerp(new Color(0.36f, 0.72f, 0.45f), new Color(0.85f, 0.32f, 0.32f), t);
		}

		static GUIStyle _pathStyle;

		public static GUIStyle PathStyle
		{
			get
			{
#if UNITY_EDITOR
				if (_pathStyle == null)
				{
					_pathStyle = new GUIStyle(UnityEditor.EditorStyles.label) { richText = true };
				}
#endif
				return _pathStyle;
			}
		}

		public static bool ShowFullPath;

		public static string RichPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return string.Empty;
			}

			int slash = path.LastIndexOf('/');
			string file = slash < 0 ? path : path.Substring(slash + 1);

			if (!ShowFullPath || slash < 0)
			{
				return $"<b><color=#ffffff>{file}</color></b>";
			}

			string dir = path.Substring(0, slash + 1);
			return $"<color=#8a8a8a>{dir}</color><b><color=#ffffff>{file}</color></b>";
		}

		public static UnityEngine.Object LoadAsset(string path)
		{
#if UNITY_EDITOR
			return string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
#else
			return null;
#endif
		}

		public static Texture GetThumbnail(string path)
		{
#if UNITY_EDITOR
			var obj = LoadAsset(path);
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
#else
			return null;
#endif
		}

		public static void Ping(string path)
		{
#if UNITY_EDITOR
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			var obj = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
			if (obj == null)
			{
				Debug.LogWarning($"[DevTools.BuildAudit] Can't locate asset at: {path}");
				return;
			}

			UnityEditor.EditorUtility.FocusProjectWindow();
			UnityEditor.Selection.activeObject = obj;
			UnityEditor.EditorGUIUtility.PingObject(obj);
#endif
		}
	}
}
