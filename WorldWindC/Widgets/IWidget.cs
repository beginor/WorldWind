using System.Drawing;

namespace WorldWind.Widgets {
	/// <summary>
	/// Base Interface for DirectX GUI Widgets
	/// </summary>
	public interface IWidget {
		#region Methods
		void Render(DrawArgs drawArgs);
		#endregion

		#region Properties
		IWidgetCollection ChildWidgets { get; set; }
		IWidget ParentWidget { get; set; }
		Point AbsoluteLocation { get; }
		Point ClientLocation { get; set; }
		Size ClientSize { get; set; }
		bool Enabled { get; set; }
		bool Visible { get; set; }
		object Tag { get; set; }
		string Name { get; set; }
		#endregion
	}
}