using Sirenix.OdinInspector;
using UnityEditor;

namespace DevTools.QuickTools
{
	[System.Serializable]
	public class FastPlayModeSection
	{
		[Title("Fast Play Mode", bold: true)]
		[ShowInInspector, DisplayAsString, HideLabel, PropertyOrder(0)]
		string Status => EditorSettings.enterPlayModeOptionsEnabled
			? "Status: ON (no domain/scene reload)"
			: "Status: OFF";

		[HorizontalGroup("PlayMode", Width = 160), PropertyOrder(1)]
		[Button("Enable", ButtonHeight = 30)]
		[GUIColor(0.4f, 0.9f, 0.5f)]
		void Enable()
		{
			EditorSettings.enterPlayModeOptionsEnabled = true;
			EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
		}

		[HorizontalGroup("PlayMode", Width = 160), PropertyOrder(2)]
		[Button("Disable", ButtonHeight = 30)]
		[GUIColor(0.85f, 0.85f, 0.85f)]
		void Disable()
		{
			EditorSettings.enterPlayModeOptionsEnabled = false;
			EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
		}
	}
}
