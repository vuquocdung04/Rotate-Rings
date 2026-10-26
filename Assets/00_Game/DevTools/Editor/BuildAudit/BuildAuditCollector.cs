using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DevTools.BuildAudit
{
	public class BuildAuditCollector : IPostprocessBuildWithReport
	{
		public int callbackOrder => 99999;

		public static string DataPath
		{
			get
			{
				string root = Directory.GetParent(Application.dataPath).FullName;
				return Path.Combine(root, "BuildReports", "last-build-audit.json");
			}
		}

		public void OnPostprocessBuild(BuildReport report)
		{
			if (report == null)
			{
				return;
			}

			BuildAuditReport data = Build(report);
			Save(data);

			Debug.Log($"[DevTools.BuildAudit] Saved size breakdown ({data.AssetCount} assets, {data.TotalReadable}) to {DataPath}");
		}

		static BuildAuditReport Build(BuildReport report)
		{
			var data = new BuildAuditReport
			{
				Platform = report.summary.platform.ToString(),
				BuildDate = report.summary.buildEndedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
				OutputBytes = (long)report.summary.totalSize,
			};

			foreach (var scene in EditorBuildSettings.scenes)
			{
				if (scene.enabled && !string.IsNullOrEmpty(scene.path))
				{
					data.Scenes.Add(scene.path);
				}
			}

			var byAsset = new Dictionary<string, BuildAuditAsset>();
			long total = 0;

			var packed = report.packedAssets;
			if (packed != null && packed.Length > 0)
			{
				data.Source = "PackedAssets";
				foreach (PackedAssets bundle in packed)
				{
					if (bundle.contents == null)
					{
						continue;
					}

					foreach (PackedAssetInfo info in bundle.contents)
					{
						string typeName = info.type != null ? info.type.Name : "Unknown";
						string path = string.IsNullOrEmpty(info.sourceAssetPath)
							? $"(built-in) {typeName}"
							: info.sourceAssetPath;

						if (!byAsset.TryGetValue(path, out var entry))
						{
							entry = new BuildAuditAsset { Path = path, Type = typeName };
							byAsset.Add(path, entry);
						}

						entry.Bytes += (long)info.packedSize;
						total += (long)info.packedSize;
					}
				}
			}

			if (byAsset.Count == 0)
			{
				data.Source = "OutputFiles";
				byAsset.Clear();
				total = 0;

				BuildFile[] files = report.GetFiles();
				foreach (BuildFile file in files)
				{
					string path = file.path;
					if (!byAsset.TryGetValue(path, out var entry))
					{
						entry = new BuildAuditAsset { Path = path, Type = file.role };
						byAsset.Add(path, entry);
					}

					entry.Bytes += (long)file.size;
					total += (long)file.size;
				}
			}

			data.TotalBytes = total;
			data.AssetCount = byAsset.Count;

			foreach (var entry in byAsset.Values)
			{
				entry.SizeReadable = BuildAuditUtil.HumanSize(entry.Bytes);
				entry.Percent = total > 0 ? (float)(entry.Bytes / (double)total * 100.0) : 0f;
				data.Assets.Add(entry);
			}

			data.Assets.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));

			var byType = new Dictionary<string, BuildAuditCategory>();
			foreach (var entry in data.Assets)
			{
				if (!byType.TryGetValue(entry.Type, out var cat))
				{
					cat = new BuildAuditCategory { Name = entry.Type };
					byType.Add(entry.Type, cat);
				}

				cat.Bytes += entry.Bytes;
				cat.Count++;
			}

			foreach (var cat in byType.Values)
			{
				cat.SizeReadable = BuildAuditUtil.HumanSize(cat.Bytes);
				cat.PercentOfBuild = total > 0 ? (float)(cat.Bytes / (double)total * 100.0) : 0f;
				data.Categories.Add(cat);
			}

			data.Categories.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));

			Enrich(data);

			return data;
		}

		static void Enrich(BuildAuditReport data)
		{
			foreach (var entry in data.Assets)
			{
				if (string.IsNullOrEmpty(entry.Path) || entry.Path.StartsWith("("))
				{
					continue;
				}

				var importer = AssetImporter.GetAtPath(entry.Path);

				if (importer is TextureImporter tImp)
				{
					data.Textures.Add(BuildTexture(entry, tImp));
				}
				else if (importer is ModelImporter mImp)
				{
					data.Models.Add(BuildModel(entry, mImp));
				}
			}

			data.Textures.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
			data.Models.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
		}

		static BuildAuditTexture BuildTexture(BuildAuditAsset entry, TextureImporter importer)
		{
			var result = new BuildAuditTexture
			{
				SizeReadable = entry.SizeReadable,
				Percent = entry.Percent,
				Path = entry.Path,
				Bytes = entry.Bytes,
				MaxSize = importer.maxTextureSize,
				Mip = importer.mipmapEnabled,
				Crunch = importer.crunchedCompression,
				ReadWrite = importer.isReadable,
			};

			var tex = AssetDatabase.LoadAssetAtPath<Texture>(entry.Path);
			if (tex is Texture2D t2d)
			{
				result.Dimensions = $"{t2d.width}x{t2d.height}";
				result.Format = t2d.format.ToString();
			}
			else if (tex != null)
			{
				result.Dimensions = $"{tex.width}x{tex.height}";
				result.Format = tex.graphicsFormat.ToString();
			}

			return result;
		}

		static BuildAuditModel BuildModel(BuildAuditAsset entry, ModelImporter importer)
		{
			var result = new BuildAuditModel
			{
				SizeReadable = entry.SizeReadable,
				Percent = entry.Percent,
				Path = entry.Path,
				Bytes = entry.Bytes,
				MeshCompression = importer.meshCompression.ToString(),
				ReadWrite = importer.isReadable,
			};

			var assets = AssetDatabase.LoadAllAssetsAtPath(entry.Path);
			foreach (var obj in assets)
			{
				if (obj is Mesh mesh)
				{
					result.Vertices += mesh.vertexCount;
					for (int i = 0; i < mesh.subMeshCount; i++)
					{
						result.Triangles += (int)(mesh.GetIndexCount(i) / 3);
					}
				}
			}

			return result;
		}

		static void Save(BuildAuditReport data)
		{
			string dir = Path.GetDirectoryName(DataPath);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			File.WriteAllText(DataPath, JsonUtility.ToJson(data, true));
		}

		public static BuildAuditReport Load()
		{
			if (!File.Exists(DataPath))
			{
				return null;
			}

			return JsonUtility.FromJson<BuildAuditReport>(File.ReadAllText(DataPath));
		}
	}
}
