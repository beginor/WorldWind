using System;
using System.Windows.Forms;

namespace WorldWind.Widgets {
	/// <summary>
	/// Interface must be implemented in order to recieve user input.  Can be used by IRenderables and IWidgets.
	/// </summary>
	public interface IInteractive {
		#region Methods
		bool OnKeyDown(KeyEventArgs e);

		bool OnKeyUp(KeyEventArgs e);

		bool OnKeyPress(KeyPressEventArgs e);

		bool OnMouseDown(MouseEventArgs e);

		bool OnMouseEnter(EventArgs e);

		bool OnMouseLeave(EventArgs e);

		bool OnMouseMove(MouseEventArgs e);

		bool OnMouseUp(MouseEventArgs e);

		bool OnMouseWheel(MouseEventArgs e);
		#endregion
	}
}