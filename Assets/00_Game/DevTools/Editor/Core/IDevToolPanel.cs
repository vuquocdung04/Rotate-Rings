using Sirenix.OdinInspector;

namespace DevTools
{
	public interface IDevToolPanel
	{
		string Title { get; }
		int Order { get; }
		SdfIconType Icon { get; }
	}
}
