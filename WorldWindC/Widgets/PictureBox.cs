using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Net;
using Timer=System.Timers.Timer;

namespace WorldWind.Widgets {
	/// <summary>
	/// Summary description for PictureBox.
	/// </summary>
	public class PictureBox : IWidget, IInteractive {
		private string m_Text = "";
		private byte m_Opacity = 255;
		private Point m_Location = new Point(0, 0);
		private Size m_Size = new Size(0, 20);
		private bool m_Visible = true;
		private bool m_Enabled = true;
		private IWidget m_ParentWidget = null;
		private object m_Tag = null;
		private string m_Name = "";
		private string m_SaveFilePath = null;
		private Color m_ForeColor = Color.White;

		private double m_RefreshTime = 0;
		private Timer m_RefreshTimer = new Timer();

		private string m_ImageUri = null;
		private string clickableUrl = null;

		public string ClickableUrl {
			get {
				return clickableUrl;
			}
			set {
				clickableUrl = value;
			}
		}

		public double RefreshTime {
			get {
				return m_RefreshTime;
			}
			set {
				m_RefreshTime = value;
				if (m_RefreshTime > 0) {
					m_RefreshTimer.Interval = value;
				}
			}
		}
		public byte Opacity {
			get {
				return m_Opacity;
			}
			set {
				m_Opacity = value;
			}
		}

		public PictureBox() {}

		#region Properties
		public string SaveFilePath {
			get {
				return m_SaveFilePath;
			}
			set {
				m_SaveFilePath = value;
			}
		}

		public string ImageUri {
			get {
				return m_ImageUri;
			}
			set {
				m_ImageUri = value;
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

		private Texture m_ImageTexture = null;
		private string displayText = null;

		private bool isLoading = false;
		private Sprite m_sprite = null;
		private SurfaceDescription m_surfaceDescription;
		public bool IsLoaded = false;

		public void Render(DrawArgs drawArgs) {
			if (m_Visible) {
				if (m_ImageTexture == null) {
					if (!m_RefreshTimer.Enabled) {
						displayText = "Loading Image...";
						m_RefreshTimer.Elapsed += new ElapsedEventHandler(m_RefreshTimer_Elapsed);
						m_RefreshTimer.Start();
					}
				}

				if (displayText != null) {
					drawArgs.DefaultDrawingFont.DrawText(null, displayText, new Rectangle(AbsoluteLocation.X, AbsoluteLocation.Y, m_Size.Width, m_Size.Height), DrawTextFormat.None, m_ForeColor);
				}

				if (m_ImageTexture != null
				    && !isLoading) {
					drawArgs.Device.SetTexture(0, m_ImageTexture);

					drawArgs.Device.RenderState.ZBufferEnable = false;

					Point ul = new Point(AbsoluteLocation.X, AbsoluteLocation.Y);
					Point ur = new Point(AbsoluteLocation.X + m_Size.Width, AbsoluteLocation.Y);
					Point ll = new Point(AbsoluteLocation.X, AbsoluteLocation.Y + m_Size.Height);
					Point lr = new Point(AbsoluteLocation.X + m_Size.Width, AbsoluteLocation.Y + m_Size.Height);

					if (m_sprite == null) {
						m_sprite = new Sprite(drawArgs.Device);
					}

					m_sprite.Begin(SpriteFlags.AlphaBlend);

					float xscale = (float) (ur.X - ul.X)/(float) m_surfaceDescription.Width;
					float yscale = (float) (lr.Y - ur.Y)/(float) m_surfaceDescription.Height;
					m_sprite.Transform = Matrix.Scaling(xscale, yscale, 0);
					m_sprite.Transform *= Matrix.Translation(0.5f*(ul.X + ur.X), 0.5f*(ur.Y + lr.Y), 0);
					m_sprite.Draw(m_ImageTexture, new Vector3(m_surfaceDescription.Width/2, m_surfaceDescription.Height/2, 0), Vector3.Empty, Color.FromArgb(m_Opacity, 255, 255, 255).ToArgb());

					// Reset transform to prepare for text rendering later
					m_sprite.Transform = Matrix.Identity;
					m_sprite.End();
				}
			}
		}
		#endregion

		private bool isUpdating = false;

		private void m_RefreshTimer_Elapsed(object sender, ElapsedEventArgs e) {
			if (isUpdating) {
				return;
			}

			isUpdating = true;

			if (m_ImageUri == null) {
				return;
			}

			if (m_ImageUri.ToLower().StartsWith("http://")) {
				if (m_SaveFilePath == null) {
					return;
				}

				//download it
				try {
					WebDownload webDownload = new WebDownload(m_ImageUri);
					webDownload.DownloadType = DownloadType.Unspecified;

					FileInfo saveFile = new FileInfo(m_SaveFilePath);
					if (!saveFile.Directory.Exists) {
						saveFile.Directory.Create();
					}

					webDownload.DownloadFile(m_SaveFilePath);
				}
				catch {}
			}
			else {
				m_SaveFilePath = m_ImageUri;
			}

			if (m_ImageTexture != null
			    && !m_ImageTexture.Disposed) {
				m_ImageTexture.Dispose();
				m_ImageTexture = null;
			}

			if (!File.Exists(m_SaveFilePath)) {
				displayText = "Image Not Found";
				return;
			}

			m_ImageTexture = ImageHelper.LoadTexture(m_SaveFilePath);
			m_surfaceDescription = m_ImageTexture.GetLevelDescription(0);

			int width = ClientSize.Width;
			int height = ClientSize.Height;

			if (ClientSize.Width == 0) {
				width = m_surfaceDescription.Width;
			}
			if (ClientSize.Height == 0) {
				height = m_surfaceDescription.Height;
			}

			if (ParentWidget is Form) {
				Form parentForm = (Form) ParentWidget;
				parentForm.ClientSize = new Size(width, height + parentForm.HeaderHeight);
			}
			else {
				ParentWidget.ClientSize = new Size(width, height);
			}

			ClientSize = new Size(width, height);

			IsLoaded = true;
			isUpdating = false;
			displayText = null;
			if (m_RefreshTime == 0) {
				m_RefreshTimer.Stop();
			}
		}

		#region IInteractive Members
		public bool OnKeyDown(KeyEventArgs e) {
			// TODO:  Add PictureBox.OnKeyDown implementation
			return false;
		}

		public bool OnKeyUp(KeyEventArgs e) {
			// TODO:  Add PictureBox.OnKeyUp implementation
			return false;
		}

		public bool OnKeyPress(KeyPressEventArgs e) {
			// TODO:  Add PictureBox.OnKeyPress implementation
			return false;
		}

		public bool OnMouseDown(MouseEventArgs e) {
			// TODO:  Add PictureBox.OnMouseDown implementation
			return false;
		}

		public bool OnMouseEnter(EventArgs e) {
			// TODO:  Add PictureBox.OnMouseEnter implementation
			return false;
		}

		public bool OnMouseLeave(EventArgs e) {
			// TODO:  Add PictureBox.OnMouseLeave implementation
			return false;
		}

		public bool OnMouseMove(MouseEventArgs e) {
			// TODO:  Add PictureBox.OnMouseMove implementation
			return false;
		}

		private int clickBuffer = 5;

		public bool OnMouseUp(MouseEventArgs e) {
			if (ClickableUrl != null && e.X > AbsoluteLocation.X + clickBuffer && e.X < AbsoluteLocation.X + ClientSize.Width - clickBuffer && e.Y > AbsoluteLocation.Y + clickBuffer
			    && e.Y < AbsoluteLocation.Y + ClientSize.Height - clickBuffer) {
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = ClickableUrl;
				psi.Verb = "open";
				psi.UseShellExecute = true;

				psi.CreateNoWindow = true;
				Process.Start(psi);
			}
			return false;
		}

		public bool OnMouseWheel(MouseEventArgs e) {
			// TODO:  Add PictureBox.OnMouseWheel implementation
			return false;
		}
		#endregion
	}
}