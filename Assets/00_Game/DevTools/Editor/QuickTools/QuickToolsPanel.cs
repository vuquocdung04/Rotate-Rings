using Sirenix.OdinInspector;

namespace DevTools.QuickTools
{
	[System.Serializable]
	public class QuickToolsPanel : IDevToolPanel
	{
		public string Title => "Quick Tools";
		public int Order => 20;
		public SdfIconType Icon => SdfIconType.LightningChargeFill;

		[InlineProperty, HideLabel, PropertyOrder(0)]
		public FastPlayModeSection fastPlayMode = new FastPlayModeSection();

		[InlineProperty, HideLabel, PropertyOrder(1)]
		public SpriteAtlasSection spriteAtlas = new SpriteAtlasSection();

		[InlineProperty, HideLabel, PropertyOrder(2)]
		public SpriteSetupSection spriteSetup = new SpriteSetupSection();
	}
}
