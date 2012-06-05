using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX.Direct3D;

namespace WorldWind.Widgets {
	/// <summary>
	/// Summary description for TextLabel.
	/// </summary>
	public class TextBox : IWidget, IInteractive {
		private string m_Text = "";
		private Point m_Location = new Point(0, 0);
		private Size m_Size = new Size(0, 20);
		private bool m_Visible = true;
		private bool m_Enabled = true;
		private IWidget m_ParentWidget = null;
		private object m_Tag = null;
		private Color m_ForeColor = Color.White;
		private string m_Name = "";
		private Point m_LastMouseClickPosition = Point.Empty;

		public TextBox() {}

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

		private int m_SelectionStart = -1;
		private int m_SelectionEnd = -1;
		private int m_CaretPos = -1;

		private bool m_RecalculateCaretPos = false;

		public void Render(DrawArgs drawArgs) {
			if (m_Visible) {
				string displayText = m_Text;
				string caretText = "|";

				if (m_MouseDownPosition != Point.Empty) {
					int startX = (this.m_LastMousePosition.X >= this.m_MouseDownPosition.X ? this.m_MouseDownPosition.X : this.m_LastMousePosition.X);
					int endX = (this.m_LastMousePosition.X < this.m_MouseDownPosition.X ? this.m_MouseDownPosition.X : this.m_LastMousePosition.X);

					int prevWidth = 0;
					bool startXFound = false;
					bool endXFound = false;
					for (int i = 1; i <= displayText.Length; i++) {
						Rectangle rect = drawArgs.DefaultDrawingFont.MeasureString(null, displayText.Substring(0, i).Replace(" ", "I"), DrawTextFormat.None, m_ForeColor);

						if (!startXFound
						    && startX <= rect.Width) {
							startX = prevWidth;
							m_SelectionStart = i - 1;
							startXFound = true;
						}

						if (!endXFound
						    && endX <= rect.Width) {
							endX = prevWidth;
							m_SelectionEnd = i - 1;
							endXFound = true;
						}

						if (startXFound && endXFound) {
							break;
						}

						prevWidth = rect.Width;
					}

					if (!endXFound) {
						m_SelectionEnd = displayText.Length;
						endX = prevWidth;
					}

					WidgetUtil.DrawBox(AbsoluteLocation.X + startX, AbsoluteLocation.Y, endX - startX, this.ClientSize.Height, 0.0f, Color.FromArgb(200, 200, 200, 200).ToArgb(), drawArgs.Device);
				}

				drawArgs.DefaultDrawingFont.DrawText(null, m_Text, new Rectangle(AbsoluteLocation.X, AbsoluteLocation.Y, m_Size.Width, m_Size.Height), DrawTextFormat.NoClip, m_ForeColor);

				if (DateTime.Now.Millisecond < 500) {
					string space = " W";

					Rectangle spaceRect = drawArgs.DefaultDrawingFont.MeasureString(null, space, DrawTextFormat.None, m_ForeColor);

					space = "W";

					Rectangle spaceRect1 = drawArgs.DefaultDrawingFont.MeasureString(null, space, DrawTextFormat.None, m_ForeColor);

					int spaceWidth = spaceRect.Width - spaceRect1.Width;

					if (m_RecalculateCaretPos) {
						if (m_LastMouseClickPosition == Point.Empty) {
							m_CaretPos = displayText.Length;
						}
						else if (displayText.Length == 0) {
							m_CaretPos = 0;
						}
						else {
							for (int i = 1; i < displayText.Length; i++) {
								Rectangle rect = drawArgs.DefaultDrawingFont.MeasureString(null, displayText.Substring(0, i).Replace(" ", "i"), DrawTextFormat.None, m_ForeColor);

								if (m_LastMouseClickPosition.X
								    <= rect.Width) {
									m_CaretPos = i - 1;
									break;
								}
							}

							m_RecalculateCaretPos = false;
						}
					}

					if (m_CaretPos >= 0) {
						Rectangle caretRect = drawArgs.DefaultDrawingFont.MeasureString(null, caretText, DrawTextFormat.None, m_ForeColor);

						Rectangle textRect = drawArgs.DefaultDrawingFont.MeasureString(null, displayText.Substring(0, m_CaretPos), DrawTextFormat.None, m_ForeColor);

						int caretOffset = 0;
						if (m_CaretPos != 0 && m_CaretPos == displayText.Length
						    && displayText[displayText.Length - 1] == ' ') {
							caretOffset = spaceWidth;
						}
						else if (m_CaretPos < displayText.Length && m_CaretPos > 0
						         && displayText[m_CaretPos - 1] == ' ') {
							caretOffset = spaceWidth;
						}

						drawArgs.DefaultDrawingFont.DrawText(null, caretText, new Rectangle(AbsoluteLocation.X + textRect.Width - caretRect.Width/2 + caretOffset, AbsoluteLocation.Y, m_Size.Width, m_Size.Height), DrawTextFormat.NoClip, Color.Cyan); //m_ForeColor);
					}
				}
			}
		}
		#endregion

		#region IInteractive Members
		public bool OnKeyDown(KeyEventArgs e) {
			if (this.m_MouseDownPosition
			    != Point.Empty) {
				return true;
			}
			return false;
		}

		public bool OnKeyUp(KeyEventArgs e) {
			if (this.m_MouseDownPosition
			    != Point.Empty) {
				return true;
			}
			return false;
		}

		public bool OnKeyPress(KeyPressEventArgs e) {
			if (this.m_MouseDownPosition != Point.Empty
			    || this.m_LastMouseClickPosition != Point.Empty) {
				switch (e.KeyChar) {
					case '\b':
						if (this.m_MouseDownPosition != Point.Empty
						    && this.m_SelectionStart != this.m_SelectionEnd) {
							this.Text = this.Text.Remove(m_SelectionStart, m_SelectionEnd - m_SelectionStart);
							m_CaretPos = m_SelectionStart;
							m_MouseDownPosition = Point.Empty;
						}
						else if (m_CaretPos > 0) {
							this.Text = this.Text.Remove(m_CaretPos - 1, 1);
							m_CaretPos--;
						}

						break;
					default:
						this.Text += e.KeyChar;
						break;
				}
				return true;
			}
			return false;
		}

		private Point m_MouseDownPosition = Point.Empty;
		private Point m_LastMousePosition = Point.Empty;

		public bool OnMouseDown(MouseEventArgs e) {
			if (e.Button == MouseButtons.Left
			    && IsInClientArea(e)) {
				m_MouseDownPosition = new Point(e.X - AbsoluteLocation.X, e.Y - AbsoluteLocation.Y);
				this.m_LastMousePosition = m_MouseDownPosition;
				m_RecalculateCaretPos = true;
				return true;
			}
			else {
				m_MouseDownPosition = Point.Empty;
				m_RecalculateCaretPos = true;
				return false;
			}
		}

		public bool OnMouseEnter(EventArgs e) {
			// TODO:  Add TextBox.OnMouseEnter implementation
			return false;
		}

		public bool OnMouseLeave(EventArgs e) {
			// TODO:  Add TextBox.OnMouseLeave implementation
			return false;
		}

		public bool OnMouseMove(MouseEventArgs e) {
			if (e.Button == MouseButtons.Left
			    && m_MouseDownPosition != Point.Empty) {
				m_LastMousePosition = new Point(e.X - AbsoluteLocation.X, e.Y - AbsoluteLocation.Y);
				m_RecalculateCaretPos = true;
				return true;
			}
			else {
				return false;
			}
		}

		private bool IsInClientArea(Point p) {
			if (p.X >= AbsoluteLocation.X && p.X <= AbsoluteLocation.X + ClientSize.Width && p.Y >= AbsoluteLocation.Y
			    && p.Y <= AbsoluteLocation.Y + ClientSize.Height) {
				return true;
			}
			else {
				return false;
			}
		}

		private bool IsInClientArea(MouseEventArgs e) {
			if (e.X >= AbsoluteLocation.X && e.X <= AbsoluteLocation.X + ClientSize.Width && e.Y >= AbsoluteLocation.Y
			    && e.Y <= AbsoluteLocation.Y + ClientSize.Height) {
				return true;
			}
			else {
				return false;
			}
		}

		public bool OnMouseUp(MouseEventArgs e) {
			if (IsInClientArea(e)) {
				this.m_LastMouseClickPosition = new Point(e.X - AbsoluteLocation.X, e.Y - AbsoluteLocation.Y);
				m_RecalculateCaretPos = true;
				return true;
			}
			else {
				return false;
			}
		}

		public bool OnMouseWheel(MouseEventArgs e) {
			// TODO:  Add TextBox.OnMouseWheel implementation
			return false;
		}
		#endregion
	}
}