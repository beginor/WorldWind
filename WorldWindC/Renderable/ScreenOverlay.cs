using System.Drawing;
using WorldWind.Widgets;

namespace WorldWind.Renderable {

	/// <summary>
	/// Summary description for ScreenOverlay.
	/// </summary>
	public class ScreenOverlay : RenderableObject {
		private Form overlay = null;

		private ScreenAlignment alignment = ScreenAlignment.Left;

		private string clickableUrl = null;
		private int m_Width = 0;
		private int m_Height = 0;
		private bool m_ShowHeader = true;

		private int m_StartX = 0;
		private int m_StartY = 0;
		private string m_ImageUri = null;
		private string m_SaveFilePath = null;
		private double m_RefreshTimeSec = 0;
		private bool m_HideBorder = false;
		private Color m_BorderColor = Color.White;

		public Color BorderColor {
			get {
				return (overlay == null ? m_BorderColor : overlay.BorderColor);
			}
			set {
				m_BorderColor = value;
				if (overlay != null) {
					overlay.BorderColor = value;
				}
			}
		}

		public bool HideBorder {
			get {
				return (overlay == null ? m_HideBorder : overlay.HideBorder);
			}
			set {
				m_HideBorder = value;
				if (overlay != null) {
					overlay.HideBorder = value;
				}
			}
		}

		public string ClickableUrl {
			get {
				return clickableUrl;
			}
			set {
				clickableUrl = value;
				if (pBox != null) {
					pBox.ClickableUrl = value;
				}
			}
		}

		public ScreenAlignment Alignment {
			get {
				return alignment;
			}
			set {
				alignment = value;
			}
		}

		public double RefreshTimeSec {
			get {
				return m_RefreshTimeSec;
			}
			set {
				m_RefreshTimeSec = value;
			}
		}

		public bool ShowHeader {
			get {
				return m_ShowHeader;
			}
			set {
				m_ShowHeader = value;
			}
		}

		public string SaveFilePath {
			get {
				return m_SaveFilePath;
			}
			set {
				m_SaveFilePath = value;
			}
		}

		public int Width {
			get {
				return m_Width;
			}
			set {
				m_Width = value;
			}
		}

		public int Height {
			get {
				return m_Height;
			}
			set {
				m_Height = value;
			}
		}

		public ScreenOverlay(string name, int startX, int startY, string imageUri) : base(name) {
			m_StartX = startX;
			m_StartY = startY;
			m_ImageUri = imageUri;
		}

		public override void Dispose() {
			if (overlay != null) {
				overlay.Visible = false;
			}

			Inited = false;
		}

		private PictureBox pBox;

		public override void Initialize(DrawArgs drawArgs) {
			if (overlay == null) {
				overlay = new Form();
				overlay.Text = name;
				if (alignment == ScreenAlignment.Left) {
					overlay.ClientLocation = new Point(m_StartX, m_StartY);
				}
				else {
					overlay.ClientLocation = new Point(drawArgs.ParentControl.Width - m_StartX, m_StartY);
				}
				overlay.OnVisibleChanged += new VisibleChangedHandler(overlay_OnVisibleChanged);
				overlay.ClientSize = new Size(m_Width, m_Height);
				overlay.AutoHideHeader = !m_ShowHeader;
				overlay.HideBorder = m_HideBorder;
				overlay.BorderColor = m_BorderColor;

				pBox = new PictureBox();
				pBox.ClickableUrl = clickableUrl;
				pBox.RefreshTime = m_RefreshTimeSec*1000;
				pBox.Opacity = Opacity;
				pBox.ParentWidget = overlay;
				pBox.ImageUri = m_ImageUri;
				pBox.SaveFilePath = m_SaveFilePath;
				pBox.ClientLocation = new Point(0, overlay.HeaderHeight);
				pBox.ClientSize = new Size(m_Width, m_Height);
				pBox.Visible = true;

				overlay.ChildWidgets.Add(pBox);
				DrawArgs.RootWidget.ChildWidgets.Add(overlay);
			}

			if (!overlay.Visible) {
				overlay.Visible = true;
			}

			Inited = true;
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}

		public override void Render(DrawArgs drawArgs) {
			if (overlay != null && overlay.Visible && pBox != null && pBox.Visible
			    && pBox.IsLoaded) {
				if (pBox.ClientSize.Width
				    != overlay.ClientSize.Width) {
					pBox.ClientSize = new Size(overlay.ClientSize.Width, pBox.ClientSize.Height);
				}

				if (pBox.ClientSize.Height
				    != overlay.ClientSize.Height - overlay.HeaderHeight) {
					pBox.ClientSize = new Size(pBox.ClientSize.Width, overlay.ClientSize.Height - overlay.HeaderHeight);
				}
			}
		}

		public override void Update(DrawArgs drawArgs) {
			if (IsOn && !Inited) {
				Initialize(drawArgs);
			}
			else if (!IsOn && Inited) {
				Dispose();
			}
		}

		public override byte Opacity {
			get {
				return base.Opacity;
			}
			set {
				base.Opacity = value;
				if (pBox != null) {
					pBox.Opacity = value;
				}
			}
		}

		private void overlay_OnVisibleChanged(object o, bool state) {
			if (!state) {
				IsOn = false;
			}
		}
	}
}