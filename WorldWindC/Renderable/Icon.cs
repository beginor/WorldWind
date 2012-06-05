using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Net;
using Timer=System.Timers.Timer;

namespace WorldWind.Renderable {
	/// <summary>
	/// Contains one texture for our icon texture cache
	/// </summary>
	public class IconTexture : IDisposable {
		public Texture Texture;
		public int Width;
		public int Height;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.IconTexture"/> class 
		/// from a texture file on disk.
		/// </summary>
		public IconTexture(Device device, string textureFileName) {
			if (ImageHelper.IsGdiSupportedImageFormat(textureFileName)) {
				// Load without rescaling source bitmap
				using (Image image = ImageHelper.LoadImage(textureFileName)) {
					LoadImage(device, image);
				}
			}
			else {
				// Only DirectX can read this file, might get upscaled depending on input dimensions.
				Texture = ImageHelper.LoadIconTexture(textureFileName);
				// Read texture level 0 size
				using (Surface s = Texture.GetSurfaceLevel(0)) {
					SurfaceDescription desc = s.Description;
					Width = desc.Width;
					Height = desc.Height;
				}
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.IconTexture"/> class 
		/// from a bitmap.
		/// </summary>
		public IconTexture(Device device, Bitmap image) {
			LoadImage(device, image);
		}

		protected void LoadImage(Device device, Image image) {
			Width = (int) Math.Round(Math.Pow(2, (int) (Math.Ceiling(Math.Log(image.Width)/Math.Log(2)))));
			if (Width > device.DeviceCaps.MaxTextureWidth) {
				Width = device.DeviceCaps.MaxTextureWidth;
			}

			Height = (int) Math.Round(Math.Pow(2, (int) (Math.Ceiling(Math.Log(image.Height)/Math.Log(2)))));
			if (Height > device.DeviceCaps.MaxTextureHeight) {
				Height = device.DeviceCaps.MaxTextureHeight;
			}

			using (Bitmap textureSource = new Bitmap(Width, Height)) {
				using (Graphics g = Graphics.FromImage(textureSource)) {
					g.DrawImage(image, 0, 0, Width, Height);
					if (Texture != null) {
						Texture.Dispose();
					}
					Texture = new Texture(device, textureSource, Usage.None, Pool.Managed);
				}
			}
		}

		#region IDisposable Members
		public void Dispose() {
			if (Texture != null) {
				Texture.Dispose();
				Texture = null;
			}

			GC.SuppressFinalize(this);
		}
		#endregion
	}

	/// <summary>
	/// Holds a collection of icons
	/// </summary>
	public class Icons : RenderableObjectList {
		/// <summary>
		/// Texture cache
		/// </summary>
		protected Hashtable m_textures = new Hashtable();

		protected Sprite m_sprite;

		private static int hotColor = Color.White.ToArgb();
		private static int normalColor = Color.FromArgb(150, 255, 255, 255).ToArgb();
		private static int nameColor = Color.White.ToArgb();
		private static int descriptionColor = Color.White.ToArgb();

		private Timer refreshTimer;

		/// <summary>
		/// The closest icon the mouse is currently over
		/// </summary>
		protected Icon mouseOverIcon;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.Icons"/> class 
		/// </summary>
		/// <param name="name"></param>
		public Icons(string name) : base(name) {}

		public Icons(string name, string dataSource, TimeSpan refreshInterval, World parentWorld, Cache cache) : base(name, dataSource, refreshInterval, parentWorld, cache) {}

		/// <summary>
		/// Adds an icon to this layer. Deprecated.
		/// </summary>
		public void AddIcon(Icon icon) {
			Add(icon);
		}

		#region RenderableObject methods
		/// <summary>
		/// Add a child object to this layer.
		/// </summary>
		public override void Add(RenderableObject ro) {
			m_children.Add(ro);
			Inited = false;
		}

		public override void Initialize(DrawArgs drawArgs) {
			if (!isOn) {
				return;
			}

			if (m_sprite != null) {
				m_sprite.Dispose();
				m_sprite = null;
			}

			m_sprite = new Sprite(drawArgs.Device);

			TimeSpan smallestRefreshInterval = TimeSpan.MaxValue;

			// Load all textures
			foreach (RenderableObject ro in m_children) {
				Icon icon = ro as Icon;
				if (icon == null) {
					// Child is not an icon
					if (ro.IsOn) {
						ro.Initialize(drawArgs);
					}
					continue;
				}

				if (icon.RefreshInterval.TotalMilliseconds != 0 && icon.RefreshInterval != TimeSpan.MaxValue
				    && icon.RefreshInterval < smallestRefreshInterval) {
					smallestRefreshInterval = icon.RefreshInterval;
				}

				// Child is an icon
				icon.Initialize(drawArgs);

				object key = null;
				IconTexture iconTexture = null;

				if (icon.TextureFileName != null
				    && icon.TextureFileName.Length > 0) {
					if (icon.TextureFileName.ToLower().StartsWith("http://")
					    && icon.SaveFilePath != null) {
						//download it
						try {
							WebDownload webDownload = new WebDownload(icon.TextureFileName);
							webDownload.DownloadType = DownloadType.Unspecified;

							FileInfo saveFile = new FileInfo(icon.SaveFilePath);
							if (!saveFile.Directory.Exists) {
								saveFile.Directory.Create();
							}

							webDownload.DownloadFile(saveFile.FullName);
						}
						catch {}

						iconTexture = (IconTexture) m_textures[icon.SaveFilePath];
						if (iconTexture == null) {
							key = icon.SaveFilePath;
							iconTexture = new IconTexture(drawArgs.Device, icon.SaveFilePath);
						}
					}
					else {
						// Icon image from file
						iconTexture = (IconTexture) m_textures[icon.TextureFileName];
						if (iconTexture == null) {
							key = icon.TextureFileName;
							iconTexture = new IconTexture(drawArgs.Device, icon.TextureFileName);
						}
					}
				}
				else {
					// Icon image from bitmap
					if (icon.Image != null) {
						iconTexture = (IconTexture) m_textures[icon.Image];
						if (iconTexture == null) {
							// Create new texture from image
							key = icon.Image;
							iconTexture = new IconTexture(drawArgs.Device, icon.Image);
						}
					}
				}

				if (iconTexture == null) {
					// No texture set
					continue;
				}

				if (key != null) {
					// New texture, cache it
					m_textures.Add(key, iconTexture);

					// Use default dimensions if not set
					if (icon.Width == 0) {
						icon.Width = iconTexture.Width;
					}
					if (icon.Height == 0) {
						icon.Height = iconTexture.Height;
					}
				}
			}

			// Compute mouse over bounding boxes
			foreach (RenderableObject ro in m_children) {
				Icon icon = ro as Icon;
				if (icon == null) {
					// Child is not an icon
					continue;
				}

				if (GetTexture(icon) == null) {
					// Label only 
					icon.SelectionRectangle = drawArgs.DefaultDrawingFont.MeasureString(null, icon.Name, DrawTextFormat.None, 0);
				}
				else {
					// Icon only
					icon.SelectionRectangle = new Rectangle(0, 0, icon.Width, icon.Height);
				}

				// Center the box at (0,0)
				icon.SelectionRectangle.Offset(-icon.SelectionRectangle.Width/2, -icon.SelectionRectangle.Height/2);
			}

			if (refreshTimer == null
			    && smallestRefreshInterval != TimeSpan.MaxValue) {
				refreshTimer = new Timer(smallestRefreshInterval.TotalMilliseconds);
				refreshTimer.Elapsed += new ElapsedEventHandler(refreshTimer_Elapsed);
				refreshTimer.Start();
			}

			Inited = true;
		}

		public override void Dispose() {
			base.Dispose();

			if (m_textures != null) {
				foreach (IconTexture iconTexture in m_textures.Values) {
					iconTexture.Texture.Dispose();
				}
				m_textures.Clear();
			}

			if (m_sprite != null) {
				m_sprite.Dispose();
				m_sprite = null;
			}

			if (refreshTimer != null) {
				refreshTimer.Stop();
				refreshTimer.Dispose();
				refreshTimer = null;
			}
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			foreach (RenderableObject ro in m_children) {
				if (!ro.IsOn) {
					continue;
				}
				if (!ro.IsSelectable) {
					continue;
				}

				Icon icon = ro as Icon;
				if (icon == null) {
					// Child is not an icon
					if (ro.PerformSelectionAction(drawArgs)) {
						return true;
					}
					continue;
				}

				if (!drawArgs.WorldCamera.ViewFrustum.ContainsPoint(icon.Position)) {
					continue;
				}

				Vector3 projectedPoint = drawArgs.WorldCamera.Project(icon.Position);
				if (!icon.SelectionRectangle.Contains(DrawArgs.LastMousePosition.X - (int) projectedPoint.X, DrawArgs.LastMousePosition.Y - (int) projectedPoint.Y)) {
					continue;
				}

				try {
					if (DrawArgs.IsLeftMouseButtonDown
					    && !DrawArgs.IsRightMouseButtonDown) {
						if (icon.OnClickZoomAltitude != double.NaN || icon.OnClickZoomHeading != double.NaN
						    || icon.OnClickZoomTilt != double.NaN) {
							drawArgs.WorldCamera.SetPosition(icon.Latitude, icon.Longitude, icon.OnClickZoomHeading, icon.OnClickZoomAltitude, icon.OnClickZoomTilt);
						}

						ProcessStartInfo psi = new ProcessStartInfo();
						psi.FileName = icon.ClickableActionURL;
						psi.Verb = "open";
						psi.UseShellExecute = true;

						psi.CreateNoWindow = true;
						Process.Start(psi);
					}
					else if (!DrawArgs.IsLeftMouseButtonDown
					         && DrawArgs.IsRightMouseButtonDown) {
						ScreenOverlay[] overlays = icon.Overlays;
						if (overlays != null
						    && overlays.Length > 0) {
							ContextMenu contextMenu = new ContextMenu();
							foreach (ScreenOverlay curOverlay in overlays) {
								contextMenu.MenuItems.Add(curOverlay.Name, new EventHandler(icon.OverlayOnOpen));
							}
							contextMenu.Show(DrawArgs.StaticParentControl, DrawArgs.LastMousePosition);
						}
					}
					return true;
				}
				catch {}
			}
			return false;
		}

		public override void Render(DrawArgs drawArgs) {
			if (!isOn) {
				return;
			}

			if (!Inited) {
				return;
			}

			// First render everything except icons
			foreach (RenderableObject ro in m_children) {
				//	if(ro is Icon)
				//		continue;

				if (!ro.IsOn) {
					continue;
				}

				// Child is not an icon
				ro.Render(drawArgs);
			}

			int closestIconDistanceSquared = int.MaxValue;
			Icon closestIcon = null;

			// Now render just the icons
			m_sprite.Begin(SpriteFlags.AlphaBlend);
			foreach (RenderableObject ro in m_children) {
				if (!ro.IsOn) {
					continue;
				}

				Icon icon = ro as Icon;
				if (icon == null) {
					continue;
				}

				Vector3 translationVector = new Vector3((float) (icon.PositionD.X - drawArgs.WorldCamera.ReferenceCenter.X), (float) (icon.PositionD.Y - drawArgs.WorldCamera.ReferenceCenter.Y), (float) (icon.PositionD.Z - drawArgs.WorldCamera.ReferenceCenter.Z));

				// Find closest mouse-over icon
				Vector3 projectedPoint = drawArgs.WorldCamera.Project(translationVector);

				int dx = DrawArgs.LastMousePosition.X - (int) projectedPoint.X;
				int dy = DrawArgs.LastMousePosition.Y - (int) projectedPoint.Y;
				if (icon.SelectionRectangle.Contains(dx, dy)) {
					// Mouse is over, check whether this icon is closest
					int distanceSquared = dx*dx + dy*dy;
					if (distanceSquared < closestIconDistanceSquared) {
						closestIconDistanceSquared = distanceSquared;
						closestIcon = icon;
					}
				}

				if (icon != mouseOverIcon) {
					Render(drawArgs, icon, projectedPoint);
				}
			}

			// Render the mouse over icon last (on top)
			if (mouseOverIcon != null) {
				Vector3 translationVector = new Vector3((float) (mouseOverIcon.PositionD.X - drawArgs.WorldCamera.ReferenceCenter.X), (float) (mouseOverIcon.PositionD.Y - drawArgs.WorldCamera.ReferenceCenter.Y), (float) (mouseOverIcon.PositionD.Z - drawArgs.WorldCamera.ReferenceCenter.Z));

				Render(drawArgs, mouseOverIcon, drawArgs.WorldCamera.Project(translationVector));
			}

			mouseOverIcon = closestIcon;

			m_sprite.End();
		}
		#endregion

		/// <summary>
		/// Draw the icon
		/// </summary>
		protected virtual void Render(DrawArgs drawArgs, Icon icon, Vector3 projectedPoint) {
			if (!icon.Inited) {
				icon.Initialize(drawArgs);
			}

			if (!drawArgs.WorldCamera.ViewFrustum.ContainsPoint(icon.Position)) {
				return;
			}

			// Check icons for within "visual" range
			double distanceToIcon = Vector3.Length(icon.Position - drawArgs.WorldCamera.Position);
			if (distanceToIcon > icon.MaximumDisplayDistance) {
				return;
			}
			if (distanceToIcon < icon.MinimumDisplayDistance) {
				return;
			}

			IconTexture iconTexture = GetTexture(icon);
			bool isMouseOver = icon == mouseOverIcon;
			if (isMouseOver) {
				// Mouse is over
				isMouseOver = true;

				if (icon.IsSelectable) {
					DrawArgs.MouseCursor = CursorType.Hand;
				}

				string description = icon.Description;
				if (description == null) {
					description = icon.ClickableActionURL;
				}
				if (description != null) {
					// Render description field
					DrawTextFormat descFormat = DrawTextFormat.NoClip | DrawTextFormat.WordBreak | DrawTextFormat.Bottom;
					int left = drawArgs.ScreenWidth - 256 - 10;
					//if(World.Settings.showLayerManager)
					//    left += World.Settings.layerManagerWidth;
					Rectangle descRect = Rectangle.FromLTRB(left, 10, drawArgs.ScreenWidth - 10, drawArgs.ScreenHeight - 10);

					// Draw outline
					drawArgs.DefaultDrawingFont.DrawText(m_sprite, description, descRect, descFormat, 0xb0 << 24);

					descRect.Offset(2, 0);
					drawArgs.DefaultDrawingFont.DrawText(m_sprite, description, descRect, descFormat, 0xb0 << 24);

					descRect.Offset(0, 2);
					drawArgs.DefaultDrawingFont.DrawText(m_sprite, description, descRect, descFormat, 0xb0 << 24);

					descRect.Offset(-2, 0);
					drawArgs.DefaultDrawingFont.DrawText(m_sprite, description, descRect, descFormat, 0xb0 << 24);

					// Draw description
					descRect.Offset(1, -1);
					drawArgs.DefaultDrawingFont.DrawText(m_sprite, description, descRect, descFormat, descriptionColor);
				}
			}

			int color = isMouseOver ? hotColor : normalColor;
			if (iconTexture == null || isMouseOver) {
				// Render label
				if (icon.Name != null) {
					// Render name field
					const int labelWidth = 1000; // Dummy value needed for centering the text
					if (iconTexture == null) {
						// Center over target as we have no bitmap
						Rectangle rect = new Rectangle((int) projectedPoint.X - (labelWidth >> 1), (int) (projectedPoint.Y - (drawArgs.DefaultDrawingFont.Description.Height >> 1)), labelWidth, drawArgs.ScreenHeight);

						drawArgs.DefaultDrawingFont.DrawText(m_sprite, icon.Name, rect, DrawTextFormat.Center, color);
					}
					else {
						// Adjust text to make room for icon
						int spacing = (int) (icon.Width*0.3f);
						if (spacing > 10) {
							spacing = 10;
						}
						int offsetForIcon = (icon.Width >> 1) + spacing;

						Rectangle rect = new Rectangle((int) projectedPoint.X + offsetForIcon, (int) (projectedPoint.Y - (drawArgs.DefaultDrawingFont.Description.Height >> 1)), labelWidth, drawArgs.ScreenHeight);

						drawArgs.DefaultDrawingFont.DrawText(m_sprite, icon.Name, rect, DrawTextFormat.WordBreak, color);
					}
				}
			}

			if (iconTexture != null) {
				// Render icon
				float xscale = (float) icon.Width/iconTexture.Width;
				float yscale = (float) icon.Height/iconTexture.Height;
				m_sprite.Transform = Matrix.Scaling(xscale, yscale, 0);

				if (icon.IsRotated) {
					m_sprite.Transform *= Matrix.RotationZ((float) icon.Rotation.Radians - (float) drawArgs.WorldCamera.Heading.Radians);
				}

				m_sprite.Transform *= Matrix.Translation(projectedPoint.X, projectedPoint.Y, 0);
				m_sprite.Draw(iconTexture.Texture, new Vector3(iconTexture.Width >> 1, iconTexture.Height >> 1, 0), Vector3.Empty, color);

				// Reset transform to prepare for text rendering later
				m_sprite.Transform = Matrix.Identity;
			}
		}

		/// <summary>
		/// Retrieve an icon's texture
		/// </summary>
		protected IconTexture GetTexture(Icon icon) {
			object key = null;

			if (icon.Image == null) {
				key = (icon.TextureFileName.ToLower().StartsWith("http://") ? icon.SaveFilePath : icon.TextureFileName);
			}
			else {
				key = icon.Image;
			}
			if (key == null) {
				return null;
			}

			IconTexture res = (IconTexture) m_textures[key];
			return res;
		}

		private bool isUpdating = false;

		private void refreshTimer_Elapsed(object sender, ElapsedEventArgs e) {
			if (isUpdating) {
				return;
			}
			isUpdating = true;
			try {
				for (int i = 0; i < this.ChildObjects.Count; i++) {
					RenderableObject ro = (RenderableObject) this.ChildObjects[i];
					if (ro != null && ro.IsOn
					    && ro is Icon) {
						Icon icon = (Icon) ro;

						if (icon.RefreshInterval == TimeSpan.MaxValue
						    || icon.LastRefresh > DateTime.Now - icon.RefreshInterval) {
							continue;
						}

						object key = null;
						IconTexture iconTexture = null;

						if (icon.TextureFileName != null
						    && icon.TextureFileName.Length > 0) {
							if (icon.TextureFileName.ToLower().StartsWith("http://")
							    && icon.SaveFilePath != null) {
								//download it
								try {
									WebDownload webDownload = new WebDownload(icon.TextureFileName);
									webDownload.DownloadType = DownloadType.Unspecified;

									FileInfo saveFile = new FileInfo(icon.SaveFilePath);
									if (!saveFile.Directory.Exists) {
										saveFile.Directory.Create();
									}

									webDownload.DownloadFile(saveFile.FullName);
								}
								catch {}

								iconTexture = (IconTexture) m_textures[icon.SaveFilePath];
								if (iconTexture != null) {
									IconTexture tempTexture = iconTexture;
									m_textures[icon.SaveFilePath] = new IconTexture(DrawArgs.StaticDevice, icon.SaveFilePath);
									tempTexture.Dispose();
								}
								else {
									key = icon.SaveFilePath;
									iconTexture = new IconTexture(DrawArgs.StaticDevice, icon.SaveFilePath);

									// New texture, cache it
									m_textures.Add(key, iconTexture);

									// Use default dimensions if not set
									if (icon.Width == 0) {
										icon.Width = iconTexture.Width;
									}
									if (icon.Height == 0) {
										icon.Height = iconTexture.Height;
									}
								}
							}
							else {
								// Icon image from file
								iconTexture = (IconTexture) m_textures[icon.TextureFileName];
								if (iconTexture != null) {
									IconTexture tempTexture = iconTexture;
									m_textures[icon.SaveFilePath] = new IconTexture(DrawArgs.StaticDevice, icon.TextureFileName);
									tempTexture.Dispose();
								}
								else {
									key = icon.SaveFilePath;
									iconTexture = new IconTexture(DrawArgs.StaticDevice, icon.TextureFileName);

									// New texture, cache it
									m_textures.Add(key, iconTexture);

									// Use default dimensions if not set
									if (icon.Width == 0) {
										icon.Width = iconTexture.Width;
									}
									if (icon.Height == 0) {
										icon.Height = iconTexture.Height;
									}
								}
							}
						}
						else {
							// Icon image from bitmap
							if (icon.Image != null) {
								iconTexture = (IconTexture) m_textures[icon.Image];
								if (iconTexture != null) {
									IconTexture tempTexture = iconTexture;
									m_textures[icon.SaveFilePath] = new IconTexture(DrawArgs.StaticDevice, icon.Image);
									tempTexture.Dispose();
								}
								else {
									key = icon.SaveFilePath;
									iconTexture = new IconTexture(DrawArgs.StaticDevice, icon.Image);

									// New texture, cache it
									m_textures.Add(key, iconTexture);

									// Use default dimensions if not set
									if (icon.Width == 0) {
										icon.Width = iconTexture.Width;
									}
									if (icon.Height == 0) {
										icon.Height = iconTexture.Height;
									}
								}
							}
						}

						icon.LastRefresh = DateTime.Now;
					}
				}
			}
			catch {}
			finally {
				isUpdating = false;
			}
		}
	}

	/// <summary>
	/// One icon in an icon layer
	/// </summary>
	public class Icon : RenderableObject {
		public double OnClickZoomAltitude = double.NaN;
		public double OnClickZoomHeading = double.NaN;
		public double OnClickZoomTilt = double.NaN;
		public string SaveFilePath = null;
		public DateTime LastRefresh = DateTime.MinValue;
		public TimeSpan RefreshInterval = TimeSpan.MaxValue;

		private Angle m_rotation = Angle.Zero;
		private bool m_isRotated = false;
		private Point3d m_positionD = new Point3d();

		public bool IsRotated {
			get {
				return m_isRotated;
			}
			set {
				m_isRotated = value;
			}
		}

		public Angle Rotation {
			get {
				return m_rotation;
			}
			set {
				m_rotation = value;
			}
		}

		private ArrayList overlays = new ArrayList();

		//not a good way to handle this
		public void OverlayOnOpen(object o, EventArgs e) {
			MenuItem mi = (MenuItem) o;

			foreach (ScreenOverlay overlay in overlays) {
				if (overlay == null) {
					continue;
				}

				if (overlay.Name.Equals(mi.Text)) {
					if (!overlay.IsOn) {
						overlay.IsOn = true;
					}
				}
			}
		}

		public ScreenOverlay[] Overlays {
			get {
				if (overlays == null) {
					return null;
				}
				else {
					return (ScreenOverlay[]) overlays.ToArray(typeof (ScreenOverlay));
				}
			}
		}

		public void AddOverlay(ScreenOverlay overlay) {
			if (overlay != null) {
				overlays.Add(overlay);
			}
		}

		public void RemoveOverlay(ScreenOverlay overlay) {
			for (int i = 0; i < overlays.Count; i++) {
				ScreenOverlay curOverlay = (ScreenOverlay) overlays[i];
				if (curOverlay.IconImagePath == overlay.IconImagePath
				    && overlay.Name == curOverlay.Name) {
					overlays.RemoveAt(i);
				}
			}
		}

		#region private members
		/// <summary>
		/// On-Click browse to location
		/// </summary>
		protected string m_clickableActionURL;

		/// <summary>
		/// Latitude (North/South) in decimal degrees
		/// </summary>
		protected double m_latitude;

		/// <summary>
		/// Longitude (East/West) in decimal degrees
		/// </summary>
		protected double m_longitude;
		#endregion

		/// <summary>
		/// Longer description of icon (addition to name)
		/// </summary>
		public string Description;

		/// <summary>
		/// The icon altitude above sea level
		/// </summary>
		public double Altitude;

		/// <summary>
		/// Icon bitmap path. (Overrides Image)
		/// </summary>
		public string TextureFileName;

		/// <summary>
		/// Icon image.  Leave TextureFileName=null if using Image.  
		/// Caller is responsible for disposing the Bitmap when the layer is removed, 
		/// either by calling Dispose on Icon or on the Image directly.
		/// </summary>
		public Bitmap Image;

		/// <summary>
		/// Icon on-screen rendered width (pixels).  Defaults to icon image width.  
		/// If source image file is not a valid GDI+ image format, width may be increased to closest power of 2.
		/// </summary>
		public int Width;

		/// <summary>
		/// Icon on-screen rendered height (pixels).  Defaults to icon image height.  
		/// If source image file is not a valid GDI+ image format, height may be increased to closest power of 2.
		/// </summary>
		public int Height;

		/// <summary>
		/// On-Click browse to location
		/// </summary>
		public string ClickableActionURL {
			get {
				return m_clickableActionURL;
			}
			set {
				IsSelectable = value != null;
				m_clickableActionURL = value;
			}
		}

		public Point3d PositionD {
			get {
				return m_positionD;
			}
			set {
				m_positionD = value;
			}
		}

		/// <summary>
		/// The maximum distance (meters) the icon will be visible from
		/// </summary>
		public double MaximumDisplayDistance = double.MaxValue;

		/// <summary>
		/// The minimum distance (meters) the icon will be visible from
		/// </summary>
		public double MinimumDisplayDistance;

		/// <summary>
		/// Bounding box centered at (0,0) used to calculate whether mouse is over icon/label
		/// </summary>
		public Rectangle SelectionRectangle;

		/// <summary>
		/// Latitude (North/South) in decimal degrees
		/// </summary>
		public double Latitude {
			get {
				return m_latitude;
			}
		}

		/// <summary>
		/// Longitude (East/West) in decimal degrees
		/// </summary>
		public double Longitude {
			get {
				return m_longitude;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.Icon"/> class 
		/// </summary>
		/// <param name="name">Name of the icon</param>
		/// <param name="latitude">Latitude in decimal degrees.</param>
		/// <param name="longitude">Longitude in decimal degrees.</param>
		public Icon(string name, double latitude, double longitude) : base(name) {
			m_latitude = latitude;
			m_longitude = longitude;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.Icon"/> class 
		/// </summary>
		/// <param name="name">Name of the icon</param>
		/// <param name="latitude">Latitude in decimal degrees.</param>
		/// <param name="longitude">Longitude in decimal degrees.</param>
		/// <param name="heightAboveSurface">Icon height (meters) above sea level.</param>
		public Icon(string name, double latitude, double longitude, double heightAboveSurface) : base(name) {
			m_latitude = latitude;
			m_longitude = longitude;
			Altitude = heightAboveSurface;
		}

		#region Obsolete
		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.Icon"/> class 
		/// </summary>
		/// <param name="name">Name of the icon</param>
		/// <param name="latitude">Latitude in decimal degrees.</param>
		/// <param name="longitude">Longitude in decimal degrees.</param>
		/// <param name="heightAboveSurface">Icon height (meters) above sea level.</param>
		[Obsolete]
		public Icon(string name, double latitude, double longitude, double heightAboveSurface, World parentWorld) : base(name) {
			m_latitude = latitude;
			m_longitude = longitude;
			this.Altitude = heightAboveSurface;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.Icon"/> class 
		/// </summary>
		/// <param name="name">Name of the icon</param>
		/// <param name="latitude">Latitude in decimal degrees.</param>
		/// <param name="longitude">Longitude in decimal degrees.</param>
		/// <param name="heightAboveSurface">Icon height (meters) above sea level.</param>
		[Obsolete]
		public Icon(string name, string description, double latitude, double longitude, double heightAboveSurface, World parentWorld, Bitmap image, int width, int height, string actionURL) : base(name) {
			this.Description = description;
			m_latitude = latitude;
			m_longitude = longitude;
			this.Altitude = heightAboveSurface;
			this.Image = image;
			this.Width = width;
			this.Height = height;
			ClickableActionURL = actionURL;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.Icon"/> class 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="description"></param>
		/// <param name="latitude"></param>
		/// <param name="longitude"></param>
		/// <param name="heightAboveSurface"></param>
		/// <param name="parentWorld"></param>
		/// <param name="TextureFileName"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="actionURL"></param>
		[Obsolete]
		public Icon(string name, string description, double latitude, double longitude, double heightAboveSurface, World parentWorld, string TextureFileName, int width, int height, string actionURL) : base(name) {
			this.Description = description;
			m_latitude = latitude;
			m_longitude = longitude;
			this.Altitude = heightAboveSurface;
			this.TextureFileName = TextureFileName;
			this.Width = width;
			this.Height = height;
			ClickableActionURL = actionURL;
		}
		#endregion

		/// <summary>
		/// Sets the geographic position of the icon.
		/// </summary>
		/// <param name="latitude">Latitude in decimal degrees.</param>
		/// <param name="longitude">Longitude in decimal degrees.</param>
		public void SetPosition(double latitude, double longitude) {
			m_latitude = latitude;
			m_longitude = longitude;

			// Recalculate XYZ coordinates
			Inited = false;
		}

		/// <summary>
		/// Sets the geographic position of the icon.
		/// </summary>
		/// <param name="latitude">Latitude in decimal degrees.</param>
		/// <param name="longitude">Longitude in decimal degrees.</param>
		/// <param name="altitude">The icon altitude above sea level.</param>
		public void SetPosition(double latitude, double longitude, double altitude) {
			m_latitude = latitude;
			m_longitude = longitude;
			Altitude = altitude;

			// Recalculate XYZ coordinates
			Inited = false;
		}

		#region RenderableObject methods
		public override void Initialize(DrawArgs drawArgs) {
			double samplesPerDegree = 50.0/(drawArgs.WorldCamera.ViewRange.Degrees);
			double elevation = drawArgs.CurrentWorld.TerrainAccessor.GetElevationAt(m_latitude, m_longitude, samplesPerDegree);
			double altitude = (World.Settings.VerticalExaggeration*Altitude + World.Settings.VerticalExaggeration*elevation);
			Position = MathEngine.SphericalToCartesian(m_latitude, m_longitude, altitude + drawArgs.WorldCamera.WorldRadius);

			m_positionD = MathEngine.SphericalToCartesianD(Angle.FromDegrees(m_latitude), Angle.FromDegrees(m_longitude), altitude + drawArgs.WorldCamera.WorldRadius);

			Inited = true;
		}

		/// <summary>
		/// Disposes the icon (when disabled)
		/// </summary>
		public override void Dispose() {
			// Nothing to dispose
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			// Handled by parent
			return false;
		}

		private Matrix lastView = Matrix.Identity;

		public override void Update(DrawArgs drawArgs) {
			if (drawArgs.WorldCamera.ViewMatrix != lastView && drawArgs.CurrentWorld.TerrainAccessor != null
			    && drawArgs.WorldCamera.Altitude < 300000) {
				double samplesPerDegree = 50.0/drawArgs.WorldCamera.ViewRange.Degrees;
				double elevation = drawArgs.CurrentWorld.TerrainAccessor.GetElevationAt(m_latitude, m_longitude, samplesPerDegree);
				double altitude = World.Settings.VerticalExaggeration*Altitude + World.Settings.VerticalExaggeration*elevation;
				Position = MathEngine.SphericalToCartesian(m_latitude, m_longitude, altitude + drawArgs.WorldCamera.WorldRadius);

				lastView = drawArgs.WorldCamera.ViewMatrix;
			}

			if (overlays != null) {
				for (int i = 0; i < overlays.Count; i++) {
					ScreenOverlay curOverlay = (ScreenOverlay) overlays[i];
					if (curOverlay != null) {
						curOverlay.Update(drawArgs);
					}
				}
			}
		}

		public override void Render(DrawArgs drawArgs) {
			if (overlays != null) {
				for (int i = 0; i < overlays.Count; i++) {
					ScreenOverlay curOverlay = (ScreenOverlay) overlays[i];
					if (curOverlay != null
					    && curOverlay.IsOn) {
						curOverlay.Render(drawArgs);
					}
				}
			}
		}
		#endregion

		private void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e) {}
	}
}