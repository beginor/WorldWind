using System.Drawing;
using Microsoft.DirectX.Direct3D;

namespace WorldWind.Widgets {
	/// <summary>
	/// Summary description for TextLabel.
	/// </summary>
	public class TextLabel : IWidget {
		private string m_Text = "";
		private Point m_Location = new Point(0, 0);
		private Size m_Size = new Size(0, 20);
		private bool m_Visible = true;
		private bool m_Enabled = true;
		private IWidget m_ParentWidget = null;
		private object m_Tag = null;
		private Color m_ForeColor = Color.White;
		private string m_Name = "";

		public TextLabel() {}

		#region Properties
		public string Name {
			get {
				return m_Name;
			}
			set {
				m_Name = value;
			}
		}
		public Color ForeColor {
			get {
				return m_ForeColor;
			}
			set {
				m_ForeColor = value;
			}
		}
		public string Text {
			get {
				return m_Text;
			}
			set {
				m_Text = value;
			}
		}
		#endregion

		#region IWidget Members
		public IWidget ParentWidget {
			get {
				return m_ParentWidget;
			}
			set {
				m_ParentWidget = value;
			}
		}

		public bool Visible {
			get {
				return m_Visible;
			}
			set {
				m_Visible = value;
			}
		}

		public object Tag {
			get {
				return m_Tag;
			}
			set {
				m_Tag = value;
			}
		}

		public IWidgetCollection ChildWidgets {
			get {
				// TODO:  Add TextLabel.ChildWidgets getter implementation
				return null;
			}
			set {
				// TODO:  Add TextLabel.ChildWidgets setter implementation
			}
		}

		public Size ClientSize {
			get {
				return m_Size;
			}
			set {
				m_Size = value;
			}
		}

		public bool Enabled {
			get {
				return m_Enabled;
			}
			set {
				m_Enabled = value;
			}
		}

		public Point ClientLocation {
			get {
				return m_Location;
			}
			set {
				m_Location = value;
			}
		}

		public Point AbsoluteLocation {
			get {
				if (m_ParentWidget != null) {
					return new Point(m_Location.X + m_ParentWidget.AbsoluteLocation.X, m_Location.Y + m_ParentWidget.AbsoluteLocation.Y);
				}
				else {
					return m_Location;
				}
			}
		}

		public void Render(DrawArgs drawArgs) {
			if (m_Visible) {
				drawArgs.DefaultDrawingFont.DrawText(null, m_Text, new Rectangle(AbsoluteLocation.X, AbsoluteLocation.Y, m_Size.Width, m_Size.Height), DrawTextFormat.NoClip, m_ForeColor);
			}
		}
		#endregion
	}
}