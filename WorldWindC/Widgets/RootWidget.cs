using System;
using System.Drawing;
using System.Windows.Forms;

namespace WorldWind.Widgets {
	/// <summary>
	/// Summary description for Widget.
	/// </summary>
	public class RootWidget : IWidget, IInteractive {
		private IWidget m_ParentWidget = null;
		private IWidgetCollection m_ChildWidgets = new WidgetCollection();
		private Control m_ParentControl;

		public RootWidget(Control parentControl) {
			m_ParentControl = parentControl;
		}

		#region Methods
		public void Render(DrawArgs drawArgs) {
			for (int index = m_ChildWidgets.Count - 1; index >= 0; index--) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;
				if (currentWidget != null) {
					if (currentWidget.ParentWidget == null
					    || currentWidget.ParentWidget != this) {
						currentWidget.ParentWidget = this;
					}

					currentWidget.Render(drawArgs);
				}
			}
		}
		#endregion

		#region Properties
		public Point AbsoluteLocation {
			get {
				return new Point(0, 0);
			}
		}
		public string Name {
			get {
				return "Main Frame";
			}
			set {}
		}
		public IWidgetCollection ChildWidgets {
			get {
				return m_ChildWidgets;
			}
			set {
				m_ChildWidgets = value;
			}
		}

		public IWidget ParentWidget {
			get {
				return m_ParentWidget;
			}
			set {
				m_ParentWidget = value;
			}
		}

		private Point m_Location = new Point(0, 0);
		private bool m_Enabled = false;
		private bool m_Visible = false;
		private object m_Tag = null;

		public Point ClientLocation {
			get {
				return m_Location;
			}
			set {
				m_Location = value;
			}
		}

		public Size ClientSize {
			get {
				return m_ParentControl.Size;
			}
			set {
				m_ParentControl.Size = value;
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
		#endregion

		#region IInteractive Members
		public bool OnMouseDown(MouseEventArgs e) {
			for (int index = 0; index < m_ChildWidgets.Count; index++) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;

				if (currentWidget != null
				    && currentWidget is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[index] as IInteractive;

					bool handled = currentInteractive.OnMouseDown(e);
					if (handled) {
						return handled;
					}
				}
			}

			return false;
		}

		public bool OnMouseUp(MouseEventArgs e) {
			for (int index = 0; index < m_ChildWidgets.Count; index++) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;

				if (currentWidget != null
				    && currentWidget is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[index] as IInteractive;

					bool handled = currentInteractive.OnMouseUp(e);
					if (handled) {
						return handled;
					}
				}
			}

			return false;
		}

		public bool OnKeyPress(KeyPressEventArgs e) {
			for (int index = 0; index < m_ChildWidgets.Count; index++) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;

				if (currentWidget != null
				    && currentWidget is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[index] as IInteractive;

					bool handled = currentInteractive.OnKeyPress(e);
					if (handled) {
						return handled;
					}
				}
			}
			return false;
		}

		public bool OnKeyDown(KeyEventArgs e) {
			for (int index = 0; index < m_ChildWidgets.Count; index++) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;

				if (currentWidget != null
				    && currentWidget is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[index] as IInteractive;

					bool handled = currentInteractive.OnKeyDown(e);
					if (handled) {
						return handled;
					}
				}
			}
			return false;
		}

		public bool OnKeyUp(KeyEventArgs e) {
			for (int index = 0; index < m_ChildWidgets.Count; index++) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;

				if (currentWidget != null
				    && currentWidget is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[index] as IInteractive;

					bool handled = currentInteractive.OnKeyUp(e);
					if (handled) {
						return handled;
					}
				}
			}
			return false;
		}

		public bool OnMouseEnter(EventArgs e) {
			// TODO:  Add RootWidget.OnMouseEnter implementation
			return false;
		}

		public bool OnMouseMove(MouseEventArgs e) {
			for (int index = 0; index < m_ChildWidgets.Count; index++) {
				IWidget currentWidget = m_ChildWidgets[index] as IWidget;

				if (currentWidget != null
				    && currentWidget is IInteractive) {
					IInteractive currentInteractive = m_ChildWidgets[index] as IInteractive;

					bool handled = currentInteractive.OnMouseMove(e);
					if (handled) {
						return handled;
					}
				}
			}

			return false;
		}

		public bool OnMouseLeave(EventArgs e) {
			// TODO:  Add RootWidget.OnMouseLeave implementation
			return false;
		}

		public bool OnMouseWheel(MouseEventArgs e) {
			// TODO:  Add RootWidget.OnMouseWheel implementation
			return false;
		}
		#endregion
	}
}