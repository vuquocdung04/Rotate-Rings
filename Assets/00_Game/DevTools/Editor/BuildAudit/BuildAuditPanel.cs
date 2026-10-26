using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace DevTools.BuildAudit
{
	[System.Serializable]
	public class BuildAuditPanel : IDevToolPanel
	{
		public string Title => "Build Report";
		public int Order => 0;
		public SdfIconType Icon => SdfIconType.BarChartFill;

		public enum SortMode
		{
			Size,
			Name,
			Type,
		}

		BuildAuditReport _report;

		[OnInspectorInit]
		void Initialize()
		{
			if (_report == null)
			{
				Reload();
			}
		}

		[HorizontalGroup("Split", 0.64f), VerticalGroup("Split/Left")]
		[HorizontalGroup("Split/Left/Bar"), PropertyOrder(-1)]
		[Button(ButtonSizes.Medium, Icon = SdfIconType.ArrowClockwise)]
		void Reload()
		{
			_report = BuildAuditCollector.Load();
			_textures = _report != null ? _report.Textures : null;
			_models = _report != null ? _report.Models : null;
			ApplySort();
		}

		[HorizontalGroup("Split/Left/Bar"), PropertyOrder(-1)]
		[ShowInInspector, EnumToggleButtons, HideLabel, OnValueChanged("ApplySort")]
		SortMode _sort = SortMode.Size;

		[HorizontalGroup("Split/Left/Bar", MaxWidth = 130), PropertyOrder(-1)]
		[ShowInInspector, ToggleLeft, LabelText("Full Path"), LabelWidth(65)]
		bool ShowFullPath
		{
			get => BuildAuditUtil.ShowFullPath;
			set => BuildAuditUtil.ShowFullPath = value;
		}

		[TabGroup("Split/Left/Tabs", "Textures", UseFixedHeight = true), PropertyOrder(0)]
		[ShowInInspector, HideLabel, Searchable]
		[TableList(IsReadOnly = true, ShowPaging = true, ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
		List<BuildAuditTexture> _textures;

		[TabGroup("Split/Left/Tabs", "FBX", UseFixedHeight = true), PropertyOrder(0)]
		[ShowInInspector, HideLabel, Searchable]
		[TableList(IsReadOnly = true, ShowPaging = true, ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
		List<BuildAuditModel> _models;

		[TabGroup("Split/Left/Tabs", "All Assets", UseFixedHeight = true), PropertyOrder(0)]
		[ShowInInspector, HideLabel, Searchable]
		[TableList(IsReadOnly = true, ShowPaging = true, ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
		List<BuildAuditAsset> _assets;

		[VerticalGroup("Split/Right"), PropertyOrder(0)]
		[ShowInInspector, HideLabel, Title("Categories")]
		[TableList(IsReadOnly = true, AlwaysExpanded = true, HideToolbar = true)]
		List<BuildAuditCategory> Categories => _report != null ? _report.Categories : null;

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(1)]
		[ShowInInspector, LabelText("Platform"), DisplayAsString(false), LabelWidth(90)]
		string InfoPlatform => _report != null ? _report.Platform : "-";

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(1)]
		[ShowInInspector, LabelText("Build Date"), DisplayAsString(false), LabelWidth(90)]
		string InfoDate => _report != null ? _report.BuildDate : "-";

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(1)]
		[ShowInInspector, LabelText("Build Size"), DisplayAsString(false), LabelWidth(90), GUIColor(1f, 0.9f, 0.55f)]
		string InfoBuildSize => _report != null ? _report.OutputReadable : "-";

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(1)]
		[ShowInInspector, LabelText("Assets Size"), DisplayAsString(false), LabelWidth(90)]
		string InfoAssetsSize => _report != null ? _report.TotalReadable : "-";

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(1)]
		[ShowInInspector, LabelText("Asset Count"), DisplayAsString(false), LabelWidth(90)]
		string InfoAssets => _report != null ? _report.AssetCount.ToString() : "-";

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(1)]
		[ShowInInspector, LabelText("Source"), DisplayAsString(false), LabelWidth(90)]
		string InfoSource => _report != null ? _report.Source : "-";

		[BoxGroup("Split/Right/Build Info"), PropertyOrder(2)]
		[ShowInInspector, LabelText("Scenes"), ReadOnly]
		[ListDrawerSettings(ShowFoldout = true)]
		List<string> InfoScenes => _report != null ? _report.Scenes : null;

		void ApplySort()
		{
			if (_report?.Assets == null)
			{
				_assets = null;
				return;
			}

			switch (_sort)
			{
				case SortMode.Size:
					_report.Assets = _report.Assets.OrderByDescending(a => a.Bytes).ToList();
					break;
				case SortMode.Name:
					_report.Assets = _report.Assets.OrderBy(a => a.Path).ToList();
					break;
				case SortMode.Type:
					_report.Assets = _report.Assets
						.OrderBy(a => a.Type)
						.ThenByDescending(a => a.Bytes)
						.ToList();
					break;
			}

			_assets = _report.Assets;
		}
	}
}
