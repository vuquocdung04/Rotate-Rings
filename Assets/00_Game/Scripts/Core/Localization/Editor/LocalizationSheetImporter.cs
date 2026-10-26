using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ModuleTools.Localization
{
	public static class LocalizationSheetImporter
	{
		const string ExportUrl = "https://docs.google.com/spreadsheets/d/{0}/export?format=csv&gid={1}";
		const string EditUrl = "https://docs.google.com/spreadsheets/d/{0}/edit";
		const string Title = "Localization";

		static readonly Regex IdPattern = new Regex(@"/spreadsheets/d/([a-zA-Z0-9_-]+)");
		static readonly Regex CamelBoundary = new Regex(@"(?<=[a-z0-9])(?=[A-Z])");

		public static string ExtractSpreadsheetId(string urlOrId)
		{
			if (string.IsNullOrWhiteSpace(urlOrId)) return "";

			Match match = IdPattern.Match(urlOrId);
			return match.Success ? match.Groups[1].Value : urlOrId.Trim();
		}

		public static void OpenSheet(LocalizationConfigSO config)
		{
			string id = ExtractSpreadsheetId(config != null ? config.spreadsheetUrl : null);
			if (id.Length > 0) Application.OpenURL(string.Format(EditUrl, id));
		}

		public static LocalizationData ReadOutput(string jsonPath)
		{
			string full = Path.Combine(Directory.GetCurrentDirectory(), jsonPath);
			if (!File.Exists(full)) return null;

			try
			{
				return JsonConvert.DeserializeObject<LocalizationData>(File.ReadAllText(full));
			}
			catch (Exception e)
			{
				Debug.LogError($"[Localization] Không đọc được {jsonPath}: {e.Message}");
				return null;
			}
		}

		public static bool Import(LocalizationConfigSO config, string jsonPath, string keysPath)
		{
			var errors = new List<string>();
			string id = ExtractSpreadsheetId(config.spreadsheetUrl);

			if (id.Length == 0) errors.Add("Chưa nhập link Google Sheet.");
			if (config.sheets == null || config.sheets.Count == 0) errors.Add("Chưa khai báo tab nào.");
			if (string.IsNullOrWhiteSpace(config.fallbackLanguage)) errors.Add("Chưa đặt fallback language.");

			if (errors.Count > 0) return Fail(errors);

			var tables = new List<(string Name, string Csv)>();

			try
			{
				for (int i = 0; i < config.sheets.Count; i++)
				{
					LocalizationSheetEntry sheet = config.sheets[i];
					string name = string.IsNullOrWhiteSpace(sheet.name) ? $"gid {sheet.gid}" : sheet.name;
					string progress = $"[{i + 1}/{config.sheets.Count}] Đang tải {name}...";

					if (!TryDownload(string.Format(ExportUrl, id, sheet.gid), progress, (float)i / config.sheets.Count, out string csv, out string error))
					{
						errors.Add($"Tab '{name}': {error}");
						return Fail(errors);
					}

					tables.Add((name, csv));
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			var warnings = new List<string>();
			LocalizationData data = Build(config.fallbackLanguage.Trim(), tables, out List<string> keys, errors, warnings);
			string keysSource = BuildKeysSource(keys, errors);

			if (errors.Count > 0) return Fail(errors);

			WriteAsset(jsonPath, JsonConvert.SerializeObject(data, Formatting.Indented));
			WriteAsset(keysPath, keysSource);
			AssetDatabase.Refresh();

			if (warnings.Count > 0)
				Debug.LogWarning("[Localization] Lưu ý:\n- " + string.Join("\n- ", warnings));

			string summary = $"{keys.Count} key, {data.languages.Count} ngôn ngữ ({string.Join(", ", data.languages)}).";
			Debug.Log($"<color=cyan>[Localization]</color> Đã xuất {jsonPath} và {keysPath}: {summary}");
			EditorUtility.DisplayDialog(Title, $"Đã tải {tables.Count} tab.\n{summary}" + (warnings.Count > 0 ? $"\n\n{warnings.Count} cảnh báo — xem Console." : ""), "OK");

			return true;
		}

		public static LocalizationData Build(
			string fallbackLanguage,
			IList<(string Name, string Csv)> tables,
			out List<string> keys,
			List<string> errors,
			List<string> warnings)
		{
			var data = new LocalizationData { fallback = fallbackLanguage };
			var owners = new Dictionary<string, string>();
			keys = new List<string>();

			foreach ((string name, string csv) in tables)
			{
				List<List<string>> rows = ParseCsv(csv);
				if (rows.Count == 0)
				{
					warnings.Add($"Tab '{name}' rỗng.");
					continue;
				}

				var columns = new List<(int Index, string Language)>();
				List<string> header = rows[0];

				for (int c = 1; c < header.Count; c++)
				{
					string language = header[c].Trim();
					if (IsIgnored(language)) continue;

					if (columns.Any(col => col.Language == language))
					{
						errors.Add($"Tab '{name}': cột ngôn ngữ '{language}' bị trùng.");
						continue;
					}

					columns.Add((c, language));

					if (data.texts.ContainsKey(language)) continue;

					data.texts[language] = new Dictionary<string, string>();
					data.languages.Add(language);

					if (!Enum.TryParse(language, out SystemLanguage _))
						warnings.Add($"Cột '{language}' không khớp tên SystemLanguage nên không tự nhận theo máy (vẫn chọn tay được).");
				}

				for (int r = 1; r < rows.Count; r++)
				{
					string key = Cell(rows[r], 0).Trim();
					if (IsIgnored(key)) continue;

					string location = $"tab '{name}' dòng {r + 1}";

					if (owners.TryGetValue(key, out string owner))
					{
						errors.Add($"{location}: key '{key}' trùng với {owner}.");
						continue;
					}

					owners[key] = location;
					keys.Add(key);

					foreach ((int index, string language) in columns)
					{
						string value = Cell(rows[r], index);
						if (value.Length > 0) data.texts[language][key] = value;
					}
				}
			}

			if (!data.texts.ContainsKey(fallbackLanguage))
				errors.Add($"Sheet không có cột fallback '{fallbackLanguage}'.");

			foreach (string language in data.languages)
			{
				int missing = keys.Count - data.texts[language].Count;
				if (missing > 0) warnings.Add($"{language} thiếu {missing}/{keys.Count} bản dịch.");
			}

			return data;
		}

		public static List<List<string>> ParseCsv(string csv)
		{
			var rows = new List<List<string>>();
			var row = new List<string>();
			var cell = new StringBuilder();
			bool quoted = false;

			for (int i = 0; i < csv.Length; i++)
			{
				char c = csv[i];

				if (quoted)
				{
					if (c == '"')
					{
						if (i + 1 < csv.Length && csv[i + 1] == '"')
						{
							cell.Append('"');
							i++;
						}
						else
						{
							quoted = false;
						}
					}
					else if (c != '\r')
					{
						cell.Append(c);
					}

					continue;
				}

				switch (c)
				{
					case '"':
						quoted = true;
						break;
					case ',':
						row.Add(cell.ToString());
						cell.Clear();
						break;
					case '\r':
						break;
					case '\n':
						row.Add(cell.ToString());
						cell.Clear();
						rows.Add(row);
						row = new List<string>();
						break;
					default:
						cell.Append(c);
						break;
				}
			}

			if (cell.Length > 0 || row.Count > 0)
			{
				row.Add(cell.ToString());
				rows.Add(row);
			}

			return rows;
		}

		static string BuildKeysSource(List<string> keys, List<string> errors)
		{
			var entries = new List<(string Name, string Key)>();
			var seen = new Dictionary<string, string>();

			foreach (string key in keys)
			{
				string name = ToConstantName(key);

				if (name.Length == 0)
				{
					errors.Add($"Key '{key}' không sinh được tên hằng hợp lệ.");
					continue;
				}

				if (seen.TryGetValue(name, out string clash))
				{
					errors.Add($"Key '{key}' sinh ra '{name}', trùng với key '{clash}'.");
					continue;
				}

				seen[name] = key;
				entries.Add((name, key));
			}

			entries.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

			var sb = new StringBuilder();
			sb.AppendLine("// <auto-generated>");
			sb.AppendLine("// Sinh tu Google Sheet. Khong sua tay.");
			sb.AppendLine("// Sua tren sheet roi bam Tai sheet trong Modules -> Localization.");
			sb.AppendLine("// </auto-generated>");
			sb.AppendLine();
			sb.AppendLine("public static class LocalizationKeys");
			sb.AppendLine("{");

			foreach ((string name, string key) in entries)
			{
				sb.AppendLine($"    public const string {name} = \"{Escape(key)}\";");
			}

			sb.AppendLine();
			sb.AppendLine("    public static readonly string[] All =");
			sb.AppendLine("    {");

			foreach ((string name, string _) in entries)
			{
				sb.AppendLine($"        {name},");
			}

			sb.AppendLine("    };");
			sb.AppendLine("}");

			return sb.ToString();
		}

		static bool TryDownload(string url, string info, float progress, out string text, out string error)
		{
			text = null;
			error = null;

			using UnityWebRequest request = UnityWebRequest.Get(url);
			UnityWebRequestAsyncOperation operation = request.SendWebRequest();

			while (!operation.isDone)
			{
				if (EditorUtility.DisplayCancelableProgressBar("Đang tải Google Sheet...", info, progress))
				{
					request.Abort();
					error = "Đã hủy.";
					return false;
				}

				Thread.Sleep(20);
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				error = request.responseCode == 404 || request.responseCode == 400
					? $"Sai Spreadsheet Id hoặc gid ({request.responseCode})."
					: request.error;
				return false;
			}

			text = request.downloadHandler.text;

			if (text.TrimStart().StartsWith("<"))
			{
				error = "Google trả về trang HTML — sheet chưa share 'Anyone with the link → Viewer'.";
				return false;
			}

			return true;
		}

		static void WriteAsset(string path, string content)
		{
			string full = Path.Combine(Directory.GetCurrentDirectory(), path);
			Directory.CreateDirectory(Path.GetDirectoryName(full));
			File.WriteAllText(full, content, new UTF8Encoding(false));
			AssetDatabase.ImportAsset(path);
		}

		static bool Fail(List<string> errors)
		{
			string message = string.Join("\n- ", errors);
			Debug.LogError("[Localization] Không xuất được:\n- " + message);
			EditorUtility.DisplayDialog(Title, "Không xuất được:\n- " + message, "OK");
			return false;
		}

		static bool IsIgnored(string value)
		{
			return string.IsNullOrWhiteSpace(value) || value.StartsWith("#") || value.StartsWith("//");
		}

		static string Cell(List<string> row, int index) => index < row.Count ? row[index] : "";

		static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

		static string ToConstantName(string key)
		{
			string spaced = CamelBoundary.Replace(key.Trim(), "_");
			var sb = new StringBuilder(spaced.Length);

			foreach (char c in spaced)
			{
				sb.Append(c < 128 && char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_');
			}

			string collapsed = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');

			if (collapsed.Length == 0) return "";

			return char.IsDigit(collapsed[0]) ? "_" + collapsed : collapsed;
		}
	}
}
