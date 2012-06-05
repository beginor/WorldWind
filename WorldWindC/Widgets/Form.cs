using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Font=Microsoft.DirectX.Direct3D.Font;

namespace WorldWind.Widgets {
	public delegate void VisibleChangedHandler(object o, bool state);

	public class Form : IWidget, IInteractive {
		private Point m_Location = new Point(0, 0);
		private Size m_Size = new Size(300, 200);
		private IWidget m_ParentWidget = null;
		private IWidgetCollection m_ChildWidgets = new WidgetCollection();
		private string m_Name = "";

		private Color m_BackgroundColor = Color.FromArgb(100, 0, 0, 0);

		private bool m_HideBorder = false;
		private Color m_BorderColor = Color.GhostWhite;
		private Color m_HeaderColor = Color.FromArgb(120, Color.Coral.R, Color.Coral.G, Color.Coral.B);

		private int m_HeaderHeight = 20;

		private Color m_TextColor = Color.GhostWhite;
		private Font m_WorldWindDingsFont = null;
		private Font m_TextFont = null;

		private bool m_AutoHideHeader = false;
		private bool m_Visible = true;
		private bool m_Enabled = true;
		private object m_Tag = null;
		private string m_Text = "";

		public Form() {}

		#region Properties
		public bool HideBorder {
			get {
				return m_HideBorder;
			}
			set {
				m_HideBorder = value;
			}
		}

		public Font TextFont {
			get {
				return m_TextFont;
			}
			set {
				m_TextFont = value;
			}
		}
		public string Name {
			get {
				return m_Name;
			}
			set {
				m_Name = value;
			}
		}
		public bool AutoHideHeader {
			get {
				return m_AutoHideHeader;
			}
			set {
				m_AutoHideHeader = value;
			}
		}
		public Color HeaderColor {
			get {
				return m_HeaderColor;
			}
			set {
				m_HeaderColor = value;
			}
		}
		public int HeaderHeight {
			get {
				return m_HeaderHeight;
			}
			set {
				m_HeaderHeight = value;
			}
		}
		public Color BorderColor {
			get {
				return m_BorderColor;
			}
			set {
				m_BorderColor = value;
			}
		}
		public Color BackgroundColor {
			get {
				return m_BackgroundColor;
			}
			set {
				m_BackgroundColor = value;
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
				if (m_Visible != value) {
					m_Visible = value;
					if (OnVisibleChanged != null) {
						OnVisibleChanged(this, value);
					}
				}
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
				return m_ChildWidgets;
			}
			set {
				m_ChildWidgets = value;
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

		[DllImport("gdi32.dll")]
		private static extern int AddFontResource(string lpszFilename);

		private int resizeBuffer = 5;

		public virtual void Render(DrawArgs drawArgs) {
			if (!Visible) {
				return;
			}

			if (m_TextFont == null) {
				System.Drawing.Font localHeaderFont = new System.Drawing.Font("Arial", 12.0f, FontStyle.Italic | FontStyle.Bold);
				m_TextFont = new Font(drawArgs.Device, localHeaderFont);
			}

			if (m_WorldWindDingsFont == null) {
				AddFontResource(Path.Combine(Application.StartupPath, "World Wind Dings 1.04.ttf"));
				PrivateFontCollection fpc = new PrivateFontCollection();
				fpc.AddFontFile(Path.Combine(Application.StartupPath, "World Wind Dings 1.04.ttf"));
				System.Drawing.Font worldwinddings = new System.Drawing.Font(fpc.Families[0], 12.0f);

				m_WorldWindDingsFont = new Font(drawArgs.Device, worldwinddings);
			}

			if (DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer
			    && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer) {
				DrawArgs.MouseCursor = CursorType.SizeNWSE;
			}
			else if (DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer
			         && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer) {
				DrawArgs.MouseCursor = CursorType.SizeNESW;
			}
			else if (DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer + ClientSize.Height
			         && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				DrawArgs.MouseCursor = CursorType.SizeNESW;
			}
			else if (DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer + ClientSize.Height
			         && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				DrawArgs.MouseCursor = CursorType.SizeNWSE;
			}
			else if ((DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height)
			         || (DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height)) {
				DrawArgs.MouseCursor = CursorType.SizeWE;
			}
			else if ((DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer)
			         || (DrawArgs.LastMousePosition.X > AbsoluteLocation.X - resizeBuffer && DrawArgs.LastMousePosition.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && DrawArgs.LastMousePosition.Y > AbsoluteLocation.Y - resizeBuffer + ClientSize.Height && DrawArgs.LastMousePosition.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height)) {
				DrawArgs.MouseCursor = CursorType.SizeNS;
			}

			if (ClientSize.Height
			    > drawArgs.ParentControl.Height) {
				ClientSize = new Size(ClientSize.Width, drawArgs.ParentControl.Height);
			}

			if (ClientSize.Width
			    > drawArgs.ParentControl.Width) {
				ClientSize = new Size(drawArgs.ParentControl.Width, ClientSize.Height);
			}

			if (!m_AutoHideHeader
			    || (DrawArgs.LastMousePosition.X >= m_Location.X && DrawArgs.LastMousePosition.X <= m_Location.X + m_Size.Width && DrawArgs.LastMousePosition.Y >= m_Location.Y && DrawArgs.LastMousePosition.Y <= m_Location.Y + m_Size.Height)) {
				WidgetUtil.DrawBox(m_Location.X, m_Location.Y, m_Size.Width, m_HeaderHeight, 0.0f, m_HeaderColor.ToArgb(), drawArgs.Device);

				m_TextFont.DrawText(null, m_Text, new Rectangle(m_Location.X + 2, m_Location.Y, m_Size.Width, m_HeaderHeight), DrawTextFormat.None, m_TextColor.ToArgb());

				m_WorldWindDingsFont.DrawText(null, "E", new Rectangle(m_Location.X + m_Size.Width - 15, m_Location.Y + 2, m_Size.Width, m_Size.Height), DrawTextFormat.NoClip, Color.White.ToArgb());

				m_OutlineVertsHeader[0].X = AbsoluteLocation.X;
				m_OutlineVertsHeader[0].Y = AbsoluteLocation.Y;

				m_OutlineVertsHeader[1].X = AbsoluteLocation.X + ClientSize.Width;
				m_OutlineVertsHeader[1].Y = AbsoluteLocation.Y;

				m_OutlineVertsHeader[2].X = AbsoluteLocation.X + ClientSize.Width;
				m_OutlineVertsHeader[2].Y = AbsoluteLocation.Y + m_HeaderHeight;

				m_OutlineVertsHeader[3].X = AbsoluteLocation.X;
				m_OutlineVertsHeader[3].Y = AbsoluteLocation.Y + m_HeaderHeight;

				m_OutlineVertsHeader[4].X = AbsoluteLocation.X;
				m_OutlineVertsHeader[4].Y = AbsoluteLocation.Y;

				if (!m_HideBorder) {
					WidgetUtil.DrawLine(m_OutlineVertsHeader, m_BorderColor.ToArgb(), drawArgs.Device);
				}
			}

			WidgetUtil.DrawBox(m_Location.X, m_Location.Y + m_HeaderHeight, m_Size.Width, m_Size.Height - m_HeaderHeight, 0.0f, m_BackgroundColor.ToArgb(), drawArgs.Device);

			for (int index = m_ChildWidgets.Count - 1; index >= 0; index--) {
				IWidget currentChildWidget = m_ChildWidgets[index] as IWidget;
				if (currentChildWidget != null) {
					if (currentChildWidget.ParentWidget == null
					    || currentChildWidget.ParentWidget != this) {
						currentChildWidget.ParentWidget = this;
					}
					currentChildWidget.Render(drawArgs);
				}
			}

			m_OutlineVerts[0].X = AbsoluteLocation.X;
			m_OutlineVerts[0].Y = AbsoluteLocation.Y + m_HeaderHeight;

			m_OutlineVerts[1].X = AbsoluteLocation.X + ClientSize.Width;
			m_OutlineVerts[1].Y = AbsoluteLocation.Y + m_HeaderHeight;

			m_OutlineVerts[2].X = AbsoluteLocation.X + ClientSize.Width;
			m_OutlineVerts[2].Y = AbsoluteLocation.Y + ClientSize.Height;

			m_OutlineVerts[3].X = AbsoluteLocation.X;
			m_OutlineVerts[3].Y = AbsoluteLocation.Y + ClientSize.Height;

			m_OutlineVerts[4].X = AbsoluteLocation.X;
			m_OutlineVerts[4].Y = AbsoluteLocation.Y + m_HeaderHeight;

			if (!m_HideBorder) {
				WidgetUtil.DrawLine(m_OutlineVerts, m_BorderColor.ToArgb(), drawArgs.Device);
			}
		}

		private Vector2[] m_OutlineVerts = new Vector2[5];
		private Vector2[] m_OutlineVertsHeader = new Vector2[5];
		#endregion

		private bool m_IsDragging = false;
		private Point m_LastMousePosition = new Point(0, 0);

		private bool isResizingLeft = false;
		private bool isResizingRight = false;
		private bool isResizingBottom = false;
		private bool isResizingTop = false;
		private bool isResizingUL = false;
		private bool isResizingUR = false;
		private bool isResizingLL = false;
		private bool isResizingLR = false;

		#region IInteractive Members
		public bool OnMouseDown(MouseEventArgs e) {
			bool handled = false;

			bool inClientArea = false;

			if (e.X >= m_Location.X && e.X <= m_Location.X + m_Size.Width && e.Y >= m_Location.Y
			    && e.Y <= m_Location.Y + m_Size.Height) {
				m_ParentWidget.ChildWidgets.BringToFront(this);
				inClientArea = true;
			}

			if (e.X > AbsoluteLocation.X - resizeBuffer && e.X < AbsoluteLocation.X + resizeBuffer && e.Y > AbsoluteLocation.Y - resizeBuffer
			    && e.Y < AbsoluteLocation.Y + resizeBuffer) {
				isResizingUL = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer + ClientSize.Width && e.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && e.Y > AbsoluteLocation.Y - resizeBuffer
			         && e.Y < AbsoluteLocation.Y + resizeBuffer) {
				isResizingUR = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer && e.X < AbsoluteLocation.X + resizeBuffer && e.Y > AbsoluteLocation.Y - resizeBuffer + ClientSize.Height
			         && e.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				isResizingLL = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer + ClientSize.Width && e.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && e.Y > AbsoluteLocation.Y - resizeBuffer + ClientSize.Height
			         && e.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				isResizingLR = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer && e.X < AbsoluteLocation.X + resizeBuffer && e.Y > AbsoluteLocation.Y - resizeBuffer
			         && e.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				isResizingLeft = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer + ClientSize.Width && e.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && e.Y > AbsoluteLocation.Y - resizeBuffer
			         && e.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				isResizingRight = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer && e.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && e.Y > AbsoluteLocation.Y - resizeBuffer
			         && e.Y < AbsoluteLocation.Y + resizeBuffer) {
				isResizingTop = true;
			}
			else if (e.X > AbsoluteLocation.X - resizeBuffer && e.X < AbsoluteLocation.X + resizeBuffer + ClientSize.Width && e.Y > AbsoluteLocation.Y - resizeBuffer + ClientSize.Height
			         && e.Y < AbsoluteLocation.Y + resizeBuffer + ClientSize.Height) {
				isResizingBottom = true;
			}
			else if (e.X >= m_Location.X && e.X <= AbsoluteLocation.X + ClientSize.Width && e.Y >= AbsoluteLocation.Y
			         && e.Y <= AbsoluteLocation.Y + m_HeaderHeight) {
				m_IsDragging = true;
				handled = true;
			}
			m_LastMousePosition = new Point(e.X, e.Y);

			if (!handled) {
				for (int i = 0; i < m_ChildWidgets.Count; i++) {
					if (!handled) {
						if (m_ChildWidgets[i] is IInteractive) {
							IInteractive currentInteractive = m_ChildWidgets[i] as IInteractive;
							handled = currentInteractive.OnMouseDown(e);
						}
					}
				}
			}

			if (!handled && inClientArea) {
				handled = true;
			}

			return handled;
		}

		public bool OnMouseUp(MouseEventArgs e) {
			bool handled = false;
			if (e.Button
			    == MouseButtons.Left) {
				if (m_IsDragging) {
					m_IsDragging = false;
				}
			}

			bool inClientArea = false;

			if (e.X >= m_Location.X && e.X <= m_Location.X + m_Size.Width && e.Y >= m_Location.Y
			    && e.Y <= m_Location.Y + m_Size.Height) {
				inClientArea = true;
			}

			if (inClientArea) {
				if (isPointInCloseBox(new Point(e.X, e.Y))) {
					Visible = false;
					handled = true;
				}
			}

			for (int i = 0; i < m_ChildWidgets.Count; i++) {
				if (m_ChildWidgets[i] is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[i] as IInteractive;
					handled = currentInteractive.OnMouseUp(e);
				}
			}

			if (!handled && inClientArea) {
				handled = true;
			}

			if (isResizingTop) {
				isResizingTop = false;
			}
			if (isResizingBottom) {
				isResizingBottom = false;
			}
			if (isResizingLeft) {
				isResizingLeft = false;
			}
			if (isResizingRight) {
				isResizingRight = false;
			}
			if (isResizingUL) {
				isResizingUL = false;
			}
			if (isResizingUR) {
				isResizingUR = false;
			}
			if (isResizingLL) {
				isResizingLL = false;
			}
			if (isResizingLR) {
				isResizingLR = false;
			}

			return handled;
		}

		public bool OnKeyDown(KeyEventArgs e) {
			return false;
		}

		public bool OnKeyUp(KeyEventArgs e) {
			return false;
		}

		public bool OnKeyPress(KeyPressEventArgs e) {
			return false;
		}

		public bool OnMouseEnter(EventArgs e) {
			return false;
		}

		public event VisibleChangedHandler OnVisibleChanged;

		private Size minSize = new Size(20, 20);

		public bool OnMouseMove(MouseEventArgs e) {
			bool handled = false;
			int deltaX = e.X - m_LastMousePosition.X;
			int deltaY = e.Y - m_LastMousePosition.Y;

			if (isResizingTop || isResizingUL || isResizingUR) {
				m_Location.Y += deltaY;
				m_Size.Height -= deltaY;
			}
			else if (isResizingBottom || isResizingLL || isResizingLR) {
				m_Size.Height += deltaY;
			}
			else if (isResizingRight || isResizingUR || isResizingLR) {
				m_Size.Width += deltaX;
			}
			else if (isResizingLeft || isResizingUL || isResizingLL) {
				m_Location.X += deltaX;
				m_Size.Width -= deltaX;
			}
			else if (m_IsDragging) {
				m_Location.X += deltaX;
				m_Location.Y += deltaY;

				if (m_Location.X < 0) {
					m_Location.X = 0;
				}
				if (m_Location.Y < 0) {
					m_Location.Y = 0;
				}
				if (m_Location.Y + m_Size.Height
				    > DrawArgs.StaticParentControl.Height) {
					m_Location.Y = DrawArgs.StaticParentControl.Height - m_Size.Height;
				}
				if (m_Location.X + m_Size.Width
				    > DrawArgs.StaticParentControl.Width) {
					m_Location.X = DrawArgs.StaticParentControl.Width - m_Size.Width;
				}

				handled = true;
			}

			if (m_Size.Width
			    < minSize.Width) {
				m_Size.Width = minSize.Width;
			}

			if (m_Size.Height
			    < minSize.Height) {
				m_Size.Height = minSize.Height;
			}

			m_LastMousePosition = new Point(e.X, e.Y);

			for (int i = 0; i < m_ChildWidgets.Count; i++) {
				if (m_ChildWidgets[i] is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[i] as IInteractive;
					handled = currentInteractive.OnMouseMove(e);
				}
			}

			bool inClientArea = false;
			if (e.X >= m_Location.X && e.X <= m_Location.X + m_Size.Width && e.Y >= m_Location.Y
			    && e.Y <= m_Location.Y + m_Size.Height) {
				inClientArea = true;
			}

			if (!handled && inClientArea) {
				handled = true;
			}
			return handled;
		}

		private bool isPointInCloseBox(Point absolutePoint) {
			int closeBoxSize = 10;
			int closeBoxYOffset = 2;
			int closeBoxXOffset = m_Size.Width - 15;

			if (absolutePoint.X >= m_Location.X + closeBoxXOffset && absolutePoint.X <= m_Location.X + closeBoxXOffset + closeBoxSize && absolutePoint.Y >= m_Location.Y + closeBoxYOffset
			    && absolutePoint.Y <= m_Location.Y + closeBoxYOffset + closeBoxSize) {
				return true;
			}

			return false;
		}

		public bool OnMouseLeave(EventArgs e) {
			return false;
		}

		public bool OnMouseWheel(MouseEventArgs e) {
			return false;
		}
		#endregion
	}
}