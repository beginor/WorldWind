using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
//using WorldWind.Menu;
using WorldWind.Net;
using WorldWind.Terrain;
using WorldWind.VisualControl;
using Timer=System.Timers.Timer;

namespace WorldWind.Renderable {
	/// <summary>
	/// Use this class to map a single image to the planet at a desired altitude.
	/// Source image must be in Plate Carree (geographic) map projection:
	/// http://en.wikipedia.org/wiki/Plate_Carr%E9e_Projection
	/// TODO: Update this code to take advantage of the Texture Manager
	/// </summary>
	public class ImageLayer : RenderableObject {
		#region Private Members
		protected double layerRadius;
		protected double minLat;
		protected double maxLat;
		protected double minLon;
		protected double maxLon;
		private World m_ParentWorld;
		private Stream m_TextureStream = null;

		protected bool _disableZbuffer;
		protected CustomVertex.PositionColoredTextured[] vertices;
		protected static CustomVertex.TransformedColored[] progressBarOutline = new CustomVertex.TransformedColored[5];
		protected static CustomVertex.TransformedColored[] progressBar = new CustomVertex.TransformedColored[4];
		protected short[] indices;

		protected Texture texture;
		protected Device device;
		protected string _imageUrl;
		protected string _imagePath;

		// The triangular mesh density for the rendered sector
		protected int meshPointCount = 64;
		protected TerrainAccessor _terrainAccessor;

		protected int progressBarBackColor = Color.FromArgb(100, 255, 255, 255).ToArgb();
		protected int progressBarOutlineColor = Color.SlateGray.ToArgb();
		protected int textColor = Color.Black.ToArgb();

		protected float downloadPercent;
		protected Thread downloadThread;
		protected float verticalExaggeration;
		protected string m_legendImagePath;
		protected Colorbar legendControl;

		private int m_TransparentColor = 0;
		#endregion

		private TimeSpan cacheExpiration = TimeSpan.MaxValue;
		private Timer refreshTimer = null;

		#region Properties
		/// <summary>
		/// Gets or sets the color used for transparent areas.
		/// </summary>
		/// <value></value>
		public int TransparentColor {
			get {
				return m_TransparentColor;
			}
			set {
				m_TransparentColor = value;
			}
		}
		/// <summary>
		/// The url of the image (when image is on network)
		/// </summary>
		public string ImageUrl {
			get {
				return this._imageUrl;
			}
			set {
				this._imageUrl = value;
			}
		}

		/// <summary>
		/// The Path of the image (local file)
		/// </summary>
		public string ImagePath {
			get {
				return this._imagePath;
			}
			set {
				if (value != null) {
					value = value.Trim();
				}
				if (value == null
				    || value.Trim().Length <= 0) {
					_imagePath = null;
				}
				else {
					_imagePath = value.Trim();
				}
			}
		}

		/// <summary>
		/// Opacity (0..1)
		/// </summary>
		public float OpacityPercent {
			get {
				return (float) this.m_opacity/255;
			}
		}

		public bool DisableZBuffer {
			get {
				return this._disableZbuffer;
			}
			set {
				this._disableZbuffer = value;
			}
		}

		/// <summary>
		/// Longitude at left edge of image
		/// </summary>
		public double MinLon {
			get {
				return this.minLon;
			}
			set {
				this.minLon = value;
			}
		}

		/// <summary>
		/// Latitude at lower edge of image
		/// </summary>
		public double MinLat {
			get {
				return this.minLat;
			}
			set {
				this.minLat = value;
			}
		}

		/// <summary>
		/// Longitude at upper edge of image
		/// </summary>
		public double MaxLon {
			get {
				return this.maxLon;
			}
			set {
				this.maxLon = value;
			}
		}

		/// <summary>
		/// Latitude at upper edge of image
		/// </summary>
		public double MaxLat {
			get {
				return this.maxLat;
			}
			set {
				this.maxLat = value;
			}
		}

		/// <summary>
		/// Path or URL of layer legend image
		/// </summary>
		public string LegendImagePath {
			get {
				return m_legendImagePath;
			}
			set {
				m_legendImagePath = value;
			}
		}
		#endregion

		public TimeSpan CacheExpiration {
			get {
				return cacheExpiration;
			}
			set {
				cacheExpiration = value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.ImageLayer"/> class.
		/// </summary>
		public ImageLayer(string name, float layerRadius) : base(name) {
			this.layerRadius = layerRadius;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public ImageLayer(string name, World parentWorld, double distanceAboveSurface, string imagePath, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, byte opacity, TerrainAccessor terrainAccessor) : base(name, parentWorld.Position, parentWorld.Orientation) {
			this.m_ParentWorld = parentWorld;
			this.layerRadius = (float) parentWorld.EquatorialRadius + distanceAboveSurface;
			this._imagePath = imagePath;
			minLat = minLatitude;
			maxLat = maxLatitude;
			minLon = minLongitude;
			maxLon = maxLongitude;
			this.m_opacity = opacity;
			this._terrainAccessor = terrainAccessor;
			this._imagePath = imagePath;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public ImageLayer(string name, World parentWorld, double distanceAboveSurface, Stream textureStream, int transparentColor, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, double opacityPercent, TerrainAccessor terrainAccessor) : this(name, parentWorld, distanceAboveSurface, null, minLatitude, maxLatitude, minLongitude, maxLongitude, (byte) (255*opacityPercent), terrainAccessor) {
			m_TextureStream = textureStream;
			m_TransparentColor = transparentColor;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public ImageLayer(string name, World parentWorld, double distanceAboveSurface, string imagePath, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, double opacityPercent, TerrainAccessor terrainAccessor) : this(name, parentWorld, distanceAboveSurface, imagePath, minLatitude, maxLatitude, minLongitude, maxLongitude, (byte) (255*opacityPercent), terrainAccessor) {}

		/// <summary>
		/// Layer initialization code
		/// </summary>
		public override void Initialize(DrawArgs drawArgs) {
			try {
				this.device = drawArgs.Device;

				if (_imagePath == null && _imageUrl != null
				    && _imageUrl.ToLower().StartsWith("http://")) {
					_imagePath = getFilePathFromUrl(_imageUrl);
				}

				FileInfo imageFileInfo = null;
				if (_imagePath != null) {
					imageFileInfo = new FileInfo(_imagePath);
				}

				if (downloadThread != null
				    && downloadThread.IsAlive) {
					return;
				}

				if (_imagePath != null && cacheExpiration != TimeSpan.MaxValue && cacheExpiration.TotalMilliseconds > 0 && _imageUrl.ToLower().StartsWith("http://") && imageFileInfo != null && imageFileInfo.Exists
				    && imageFileInfo.LastWriteTime < DateTime.Now - cacheExpiration) {
					//attempt to redownload it
					downloadThread = new Thread(new ThreadStart(DownloadImage));
					downloadThread.Name = "ImageLayer.DownloadImage";

					downloadThread.IsBackground = true;
					downloadThread.Start();

					return;
				}

				if (m_TextureStream != null) {
					UpdateTexture(m_TextureStream, m_TransparentColor);
					verticalExaggeration = World.Settings.VerticalExaggeration;
					CreateMesh();
					Inited = true;
					return;
				}
				else if (imageFileInfo != null
				         && imageFileInfo.Exists) {
					UpdateTexture(_imagePath);
					verticalExaggeration = World.Settings.VerticalExaggeration;
					CreateMesh();
					Inited = true;
					return;
				}

				if (_imageUrl != null
				    && _imageUrl.ToLower().StartsWith("http://")) {
					//download it...
					downloadThread = new Thread(new ThreadStart(DownloadImage));
					downloadThread.Name = "ImageLayer.DownloadImage";

					downloadThread.IsBackground = true;
					downloadThread.Start();

					return;
				}

				// No image available
				Dispose();
				isOn = false;
				return;
			}
			catch {}
		}

		/// <summary>
		/// Downloads image from web
		/// </summary>
		protected void DownloadImage() {
			try {
				if (_imagePath != null) {
					Directory.CreateDirectory(Path.GetDirectoryName(this._imagePath));
				}

				using (WebDownload downloadReq = new WebDownload(this._imageUrl)) {
					downloadReq.ProgressCallback += new DownloadProgressHandler(UpdateDownloadProgress);
					string filePath = getFilePathFromUrl(_imageUrl);

					if (_imagePath == null) {
						// Download to RAM
						downloadReq.DownloadMemory();
						texture = ImageHelper.LoadTexture(downloadReq.ContentStream);
					}
					else {
						downloadReq.DownloadFile(_imagePath);
						UpdateTexture(_imagePath);
					}
					CreateMesh();
					Inited = true;
				}
			}
			catch (ThreadAbortException) {}
			catch (Exception caught) {
				if (!showedError) {
					string msg = string.Format("Image download of file\n\n{1}\n\nfor layer '{0}' failed:\n\n{2}", name, _imageUrl, caught.Message);
					MessageBox.Show(msg, "Image download failed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
					showedError = true;
				}

				if (_imagePath != null) {
					FileInfo imageFile = new FileInfo(_imagePath);
					if (imageFile.Exists) {
						UpdateTexture(_imagePath);
						CreateMesh();
						Inited = true;
					}
				}
				else {
					isOn = false;
				}
			}
		}

		private bool showedError = false;

		/// <summary>
		/// Download progress callback 
		/// </summary>
		protected void UpdateDownloadProgress(int bytesRead, int bytesTotal) {
			if (bytesRead < bytesTotal) {
				downloadPercent = (float) bytesRead/bytesTotal;
			}
		}

		/// <summary>
		/// Update layer (called from worker thread)
		/// </summary>
		public override void Update(DrawArgs drawArgs) {
			try {
				if (!this.Inited) {
					this.Initialize(drawArgs);
					if (!this.Inited) {
						return;
					}
				}

				if (m_SurfaceImage == null && Inited
				    && Math.Abs(this.verticalExaggeration - World.Settings.VerticalExaggeration) > 0.01f) {
					// Vertical exaggeration changed - rebuild mesh
					this.verticalExaggeration = World.Settings.VerticalExaggeration;
					this.Inited = false;
					CreateMesh();
					this.Inited = true;
				}

				if (cacheExpiration != TimeSpan.MaxValue
				    && cacheExpiration.TotalMilliseconds > 0) {
					if (refreshTimer == null) {
						refreshTimer = new Timer(cacheExpiration.TotalMilliseconds);
						refreshTimer.Elapsed += new ElapsedEventHandler(refreshTimer_Elapsed);
						refreshTimer.Start();
					}

					if (refreshTimer.Interval
					    != cacheExpiration.TotalMilliseconds) {
						refreshTimer.Interval = cacheExpiration.TotalMilliseconds;
					}
				}
				else if (refreshTimer != null
				         && refreshTimer.Enabled) {
					refreshTimer.Stop();
				}
			}
			catch {}
		}

		/// <summary>
		/// Handle mouse click
		/// </summary>
		/// <returns>true if click was handled.</returns>
		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}

		public override byte Opacity {
			get {
				return m_opacity;
			}
			set {
				m_opacity = value;

				if (vertices == null) {
					return;
				}

				// Update mesh opacity
				int opacityColor = m_opacity << 24;
				for (int index = 0; index < vertices.Length; index++) {
					vertices[index].Color = opacityColor;
				}
			}
		}

		private SurfaceImage m_SurfaceImage = null;

		/// <summary>
		/// Builds the image's mesh 
		/// </summary>
		protected virtual void CreateMesh() {
			int upperBound = meshPointCount - 1;
			float scaleFactor = (float) 1/upperBound;
			double latrange = Math.Abs(maxLat - minLat);
			double lonrange;
			if (minLon < maxLon) {
				lonrange = maxLon - minLon;
			}
			else {
				lonrange = 360.0f + maxLon - minLon;
			}

			int opacityColor = Color.FromArgb(this.m_opacity, 0, 0, 0).ToArgb();
			vertices = new CustomVertex.PositionColoredTextured[meshPointCount*meshPointCount];
			for (int i = 0; i < meshPointCount; i++) {
				for (int j = 0; j < meshPointCount; j++) {
					double height = 0;
					if (this._terrainAccessor != null) {
						height = this.verticalExaggeration*this._terrainAccessor.GetElevationAt((double) maxLat - scaleFactor*latrange*i, (double) minLon + scaleFactor*lonrange*j, (double) upperBound/latrange);
					}

					Vector3 pos = MathEngine.SphericalToCartesian(maxLat - scaleFactor*latrange*i, minLon + scaleFactor*lonrange*j, layerRadius + height);

					vertices[i*meshPointCount + j].X = pos.X;
					vertices[i*meshPointCount + j].Y = pos.Y;
					vertices[i*meshPointCount + j].Z = pos.Z;

					vertices[i*meshPointCount + j].Tu = j*scaleFactor;
					vertices[i*meshPointCount + j].Tv = i*scaleFactor;
					vertices[i*meshPointCount + j].Color = opacityColor;
				}
			}

			indices = new short[2*upperBound*upperBound*3];
			for (int i = 0; i < upperBound; i++) {
				for (int j = 0; j < upperBound; j++) {
					indices[(2*3*i*upperBound) + 6*j] = (short) (i*meshPointCount + j);
					indices[(2*3*i*upperBound) + 6*j + 1] = (short) ((i + 1)*meshPointCount + j);
					indices[(2*3*i*upperBound) + 6*j + 2] = (short) (i*meshPointCount + j + 1);

					indices[(2*3*i*upperBound) + 6*j + 3] = (short) (i*meshPointCount + j + 1);
					indices[(2*3*i*upperBound) + 6*j + 4] = (short) ((i + 1)*meshPointCount + j);
					indices[(2*3*i*upperBound) + 6*j + 5] = (short) ((i + 1)*meshPointCount + j + 1);
				}
			}
		}

		/// <summary>
		/// Draws the layer
		/// </summary>
		public override void Render(DrawArgs drawArgs) {
			if (downloadThread != null
			    && downloadThread.IsAlive) {
				RenderProgress(drawArgs);
			}

			if (!this.Inited) {
				return;
			}

			try {
				if (texture == null
				    || m_SurfaceImage != null) {
					return;
				}

				drawArgs.Device.SetTexture(0, this.texture);

				if (this._disableZbuffer) {
					if (drawArgs.Device.RenderState.ZBufferEnable) {
						drawArgs.Device.RenderState.ZBufferEnable = false;
					}
				}
				else {
					if (!drawArgs.Device.RenderState.ZBufferEnable) {
						drawArgs.Device.RenderState.ZBufferEnable = true;
					}
				}

				//	drawArgs.device.Clear(ClearFlags.ZBuffer, 0, 1.0f, 0);

				drawArgs.Device.VertexFormat = CustomVertex.PositionColoredTextured.Format;
				drawArgs.Device.TextureState[0].AlphaOperation = TextureOperation.Modulate;
				drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Add;
				drawArgs.Device.TextureState[0].AlphaArgument1 = TextureArgument.TextureColor;

				drawArgs.Device.Transform.World = Matrix.Translation((float) -drawArgs.WorldCamera.ReferenceCenter.X, (float) -drawArgs.WorldCamera.ReferenceCenter.Y, (float) -drawArgs.WorldCamera.ReferenceCenter.Z);

				drawArgs.Device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, vertices.Length, indices.Length/3, indices, true, vertices);

				drawArgs.Device.Transform.World = drawArgs.WorldCamera.WorldMatrix;
			}
			finally {
				if (this._disableZbuffer) {
					drawArgs.Device.RenderState.ZBufferEnable = true;
				}
			}
		}

		protected virtual void RenderProgress(DrawArgs drawArgs) {
			device.RenderState.ZBufferEnable = false;
			double centerLat = 0.5*(maxLat + minLat);
			double centerLon = 0.5*(maxLon + minLon);

			Vector3 v = MathEngine.SphericalToCartesian(centerLat, centerLon, this.layerRadius);

			if (drawArgs.WorldCamera.ViewFrustum.ContainsPoint(v)
			    && MathEngine.SphericalDistanceDegrees(centerLat, centerLon, drawArgs.WorldCamera.Latitude.Degrees, drawArgs.WorldCamera.Longitude.Degrees) < 2*drawArgs.WorldCamera.ViewRange.Degrees) {
				v.Project(drawArgs.Device.Viewport, drawArgs.Device.Transform.Projection, drawArgs.Device.Transform.View, drawArgs.Device.Transform.World);

				WorldWind.Widgets.WidgetUtil.DrawBox((int)v.X, (int)v.Y, 200, 40, 0.0f, progressBarBackColor, drawArgs.Device);
				Vector2[] boxOutline = new Vector2[5];
				boxOutline[0].X = (int) v.X;
				boxOutline[0].Y = (int) v.Y;

				boxOutline[1].X = (int) v.X + 200;
				boxOutline[1].Y = (int) v.Y;

				boxOutline[2].X = (int) v.X + 200;
				boxOutline[2].Y = (int) v.Y + 40;

				boxOutline[3].X = (int) v.X;
				boxOutline[3].Y = (int) v.Y + 40;

				boxOutline[4].X = (int) v.X;
				boxOutline[4].Y = (int) v.Y;

				//MenuUtils.DrawLine(boxOutline, progressBarOutlineColor, drawArgs.device);
				WorldWind.Widgets.WidgetUtil.DrawLine(boxOutline, progressBarOutlineColor, drawArgs.Device);

				drawArgs.DefaultDrawingFont.DrawText(null, "Downloading Remote Image...", new Rectangle((int) v.X + 5, (int) v.Y + 5, 200, 50), DrawTextFormat.NoClip, textColor);

				DrawProgressBar(drawArgs, v.X + 100, v.Y + 30, 180, 10, World.Settings.downloadProgressColor);
			}
			device.RenderState.ZBufferEnable = true;
		}

		/// <summary>
		/// Displays a progress bar
		/// </summary>
		private void DrawProgressBar(DrawArgs drawArgs, float x, float y, float width, float height, int color) {
			float halfWidth = width/2;
			float halfHeight = height/2;
			progressBarOutline[0].X = x - halfWidth;
			progressBarOutline[0].Y = y - halfHeight;
			progressBarOutline[0].Z = 0.5f;
			progressBarOutline[0].Color = color;

			progressBarOutline[1].X = x + halfWidth;
			progressBarOutline[1].Y = y - halfHeight;
			progressBarOutline[1].Z = 0.5f;
			progressBarOutline[1].Color = color;

			progressBarOutline[2].X = x + halfWidth;
			progressBarOutline[2].Y = y + halfHeight;
			progressBarOutline[2].Z = 0.5f;
			progressBarOutline[2].Color = color;

			progressBarOutline[3].X = x - halfWidth;
			progressBarOutline[3].Y = y + halfHeight;
			progressBarOutline[3].Z = 0.5f;
			progressBarOutline[3].Color = color;

			progressBarOutline[4].X = x - halfWidth;
			progressBarOutline[4].Y = y - halfHeight;
			progressBarOutline[4].Z = 0.5f;
			progressBarOutline[4].Color = color;

			drawArgs.Device.VertexFormat = CustomVertex.TransformedColored.Format;
			drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
			drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, 4, progressBarOutline);

			int barlength = (int) (this.downloadPercent*2*halfWidth);

			progressBar[0].X = x - halfWidth;
			progressBar[0].Y = y - halfHeight;
			progressBar[0].Z = 0.5f;
			progressBar[0].Color = color;

			progressBar[1].X = x - halfWidth;
			progressBar[1].Y = y + halfHeight;
			progressBar[1].Z = 0.5f;
			progressBar[1].Color = color;

			progressBar[2].X = x - halfWidth + barlength;
			progressBar[2].Y = y - halfHeight;
			progressBar[2].Z = 0.5f;
			progressBar[2].Color = color;

			progressBar[3].X = x - halfWidth + barlength;
			progressBar[3].Y = y + halfHeight;
			progressBar[3].Z = 0.5f;
			progressBar[3].Color = color;

			drawArgs.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, progressBar);
		}

		private static string getFilePathFromUrl(string url) {
			if (url.ToLower().StartsWith("http://")) {
				url = url.Substring(7);
			}

			// ShockFire: Remove any illegal characters from the path
			foreach (char invalidChar in Path.GetInvalidPathChars()) {
				url = url.Replace(invalidChar.ToString(), "");
			}

			// ShockFire: Also remove other illegal chars that are not included in InvalidPathChars for no good reason
			url = url.Replace(":", "").Replace("*", "").Replace("?", "");

			return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), Path.Combine("Cache\\ImageUrls", url));
		}

		/// <summary>
		/// Switch to a different image
		/// </summary>
		public void UpdateTexture(string fileName) {
			try {
				if (this.device != null) {
					Texture oldTexture = this.texture;

					this._imagePath = fileName;
					Texture newTexture = ImageHelper.LoadTexture(fileName);
					this.texture = newTexture;

					if (oldTexture != null) {
						oldTexture.Dispose();
					}
				}
			}
			catch {}
		}

		/// <summary>
		/// Switch to a different image
		/// </summary>
		public void UpdateTexture(Stream textureStream) {
			UpdateTexture(textureStream, 0);
		}

		/// <summary>
		/// Switch to a different image
		/// </summary>
		public void UpdateTexture(Stream textureStream, int transparentColor) {
			try {
				if (this.device != null) {
					Texture oldTexture = this.texture;

					Texture newTexture = ImageHelper.LoadTexture(textureStream);
					this.texture = newTexture;

					if (oldTexture != null) {
						oldTexture.Dispose();
					}
				}
			}
			catch {}
		}

		/// <summary>
		/// Cleanup when layer is disabled
		/// </summary>
		public override void Dispose() {
			this.Inited = false;

			if (downloadThread != null) {
				if (downloadThread.IsAlive) {
					downloadThread.Abort();
				}
				downloadThread = null;
			}

			if (m_SurfaceImage != null) {
				m_ParentWorld.WorldSurfaceRenderer.RemoveSurfaceImage(m_SurfaceImage.ImageFilePath);
				m_SurfaceImage = null;
			}
			if (this.texture != null) {
				this.texture.Dispose();
				this.texture = null;
			}

			if (legendControl != null) {
				legendControl.Dispose();
				legendControl = null;
			}

			if (refreshTimer != null
			    && refreshTimer.Enabled) {
				refreshTimer.Stop();
				refreshTimer = null;
			}
		}

		/// <summary>
		/// Change opacity
		/// </summary>
		/// <param name="percent">0=transparent, 1=opaque</param>
		public void UpdateOpacity(float percent) {
			Debug.Assert(percent <= 1);
			Debug.Assert(percent >= 0);

			this.m_opacity = (byte) (255*percent);

			this.Inited = false;
			CreateMesh();
			this.Inited = true;
		}

		/// <summary>
		/// Change radius
		/// </summary>
		/// <param name="layerRadius">Sphere radius (meters)</param>
		public void UpdateLayerRadius(float layerRadius) {
			this.layerRadius = layerRadius;

			this.Inited = false;
			CreateMesh();
			this.Inited = true;
		}

		public override void BuildContextMenu(ContextMenu menu) {
			base.BuildContextMenu(menu);

			if (m_legendImagePath == null
			    || m_legendImagePath.Length <= 0) {
				return;
			}

			// Add legend menu item
			MenuItem mi = new MenuItem("&Show Legend", new EventHandler(OnLegendClick));
			menu.MenuItems.Add(0, mi);
		}

		/// <summary>
		/// Called when user chooses to display legendControl
		/// </summary>
		protected virtual void OnLegendClick(object sender, EventArgs e) {
			if (legendControl == null) {
				legendControl = new Colorbar(null);
			}
			legendControl.LoadImage(m_legendImagePath);
		}

		private bool abortedFirstRefresh = false;

		private void refreshTimer_Elapsed(object sender, ElapsedEventArgs e) {
			if (!abortedFirstRefresh) {
				abortedFirstRefresh = true;
				return;
			}

			if (_imageUrl == null
			    && _imagePath != null) {
				UpdateTexture(_imagePath);
			}
			else if (_imageUrl != null
			         && _imageUrl.ToLower().StartsWith("http://")) {
				_imagePath = getFilePathFromUrl(_imageUrl);
				DownloadImage();
			}
		}
	}
}