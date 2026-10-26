using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DevTools
{
	public class DevToolHub : OdinMenuEditorWindow
	{
		[MenuItem("Tools/DevTool")]
		static void Open()
		{
			var window = GetWindow<DevToolHub>();
			window.titleContent = new GUIContent("DevTool");
			window.minSize = new Vector2(1100, 600);
			window.MenuWidth = 180f;
		}

		List<IDevToolPanel> _panels;

		protected override OdinMenuTree BuildMenuTree()
		{
			_panels ??= DiscoverPanels();

			var tree = new OdinMenuTree();
			tree.Selection.SupportsMultiSelect = false;
			tree.Config.DrawSearchToolbar = true;

			foreach (var panel in _panels)
			{
				tree.Add(panel.Title, panel, panel.Icon);
			}

			return tree;
		}

		static List<IDevToolPanel> DiscoverPanels()
		{
			var panels = new List<IDevToolPanel>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch
				{
					continue;
				}

				foreach (var type in types)
				{
					if (type.IsAbstract || type.IsInterface)
					{
						continue;
					}

					if (!typeof(IDevToolPanel).IsAssignableFrom(type))
					{
						continue;
					}

					if (type.GetConstructor(Type.EmptyTypes) == null)
					{
						continue;
					}

					panels.Add((IDevToolPanel)Activator.CreateInstance(type));
				}
			}

			panels.Sort((a, b) => a.Order.CompareTo(b.Order));
			return panels;
		}
	}
}
