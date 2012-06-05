using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX.Direct3D;
using System.Threading;
using System.Drawing;
using WorldWind.Terrain;
using System.IO;
using Microsoft.DirectX;
using WorldWind.Net;
using WorldWind.Utilities;

namespace WorldWind.Renderable {
	/// <summary>
	/// Used by MODIS Icons
	/// </summary>
	public class DownloadableIcon : RenderableObject {
		private Texture iconTexture;
		public bool LoadImage;
		public bool IsTextureAvailable;
		public ImageLayer imageLayer;
		private float north;
		private float south;
		private float east;
		private float west;
		private float centerLat;
		private float centerLon;
		private float layerRadius;
		public Thread downloadThread;
		private string imageUrl;
		private string saveTexturePath;
		private string caption;
		private Sprite sprite;
		private string iconFilePath = null;
		private Rectangle spriteSize;

		private int iconSize;
		private DrawArgs drawArgs;
		private TerrainAccessor _terrainAccessor;
		private CustomVertex.TransformedColored[] progressBar = new CustomVertex.TransformedColored[4];
		private CustomVertex.TransformedColored[] progressBarOutline = new CustomVertex.TransformedColored[5];
		private static int progressDefaultColor = Color.Red.ToArgb();
		private static int progressColorLoading = Color.CornflowerBlue.ToArgb();
		private static int progressColorConversion = Color.YellowGreen.ToArgb();
		private static int bottomLeftTextColor = Color.Cyan.ToArgb();

		private World m_ParentWorld;
		private float downloadProgress;
		private DownloadState downloadState = DownloadState.Pending;

		public int Width {
			get {
				return iconSize / 2;
			}
		}
		public int Height {
			get {
				return iconSize / 2;
			}
		}
		public float Latitude {
			get {
				return centerLat;
			}
		}
		public float Longitude {
			get {
				return centerLon;
			}
		}
		public string IconFilePath {
			get {
				return iconFilePath;
			}
			set {
				iconFilePath = value;
			}
		}
		public float North {
			get {
				return north;
			}
		}
		public float South {
			get {
				return south;
			}
		}
		public float West {
			get {
				return west;
			}
		}
		public float East {
			get {
				return East;
			}
		}
		public string SaveTexturePath {
			get {
				return saveTexturePath;
			}
		}

		public DownloadableIcon(string name, World parentWorld, float distanceAboveSurface, float west, float south, float east, float north, string imageUrl, string saveTexturePath, Texture iconTexture, int iconSize, string caption, TerrainAccessor terrainAccessor)
			: base(name, parentWorld.Position, parentWorld.Orientation) {
			this.imageUrl = imageUrl;
			this.saveTexturePath = saveTexturePath;
			this.iconTexture = iconTexture;
			this.iconSize = iconSize;

			this.north = north;
			this.south = south;
			this.west = west;
			this.east = east;
			this.caption = caption;

			this._terrainAccessor = terrainAccessor;

			this.centerLat = 0.5f * (this.north + this.south);
			this.centerLon = 0.5f * (this.west + this.east);

			this.m_ParentWorld = parentWorld;
			this.layerRadius = (float)parentWorld.EquatorialRadius + distanceAboveSurface;
			this.IsTextureAvailable = File.Exists(saveTexturePath);
			if (this.IsTextureAvailable) {
				this.downloadProgress = 1.0f;
			}
		}

		public override void Initialize(DrawArgs drawArgs) {
			this.drawArgs = drawArgs;

			using (Surface s = this.iconTexture.GetSurfaceLevel(0)) {
				SurfaceDescription desc = s.Description;
				this.spriteSize = new Rectangle(0, 0, desc.Width, desc.Height);
			}

			this.sprite = new Sprite(drawArgs.Device);

			for (int i = 0; i < progressBarOutline.Length; i++) {
				progressBarOutline[i].Z = 0.5f;
			}
			for (int i = 0; i < progressBar.Length; i++) {
				progressBar[i].Z = 0.5f;
			}

			this.Inited = true;
		}

		public override void Dispose() {
			this.Inited = false;
			this.LoadImage = false;
			if (this.imageLayer != null) {
				this.imageLayer.Dispose();
			}

			this.downloadState = DownloadState.Cancelled;

			if (this.sprite != null) {
				this.sprite.Dispose();
			}
		}

		public bool WasClicked(DrawArgs drawArgs) {
			int halfIconWidth = (int)(0.3f * this.iconSize);
			int halfIconHeight = (int)(0.3f * this.iconSize);

			Vector3 projectedPoint = MathEngine.SphericalToCartesian(0.5f * (this.north + this.south), 0.5f * (this.west + this.east), this.layerRadius);
			if (!drawArgs.WorldCamera.ViewFrustum.ContainsPoint(projectedPoint)) {
				return false;
			}
			projectedPoint.Project(drawArgs.Device.Viewport, drawArgs.Device.Transform.Projection, drawArgs.Device.Transform.View, drawArgs.Device.Transform.World);

			int top = (int)projectedPoint.Y - halfIconHeight;
			int bottom = (int)projectedPoint.Y + halfIconHeight;
			int left = (int)projectedPoint.X - halfIconWidth;
			int right = (int)projectedPoint.X + halfIconWidth;

			try {
				if (DrawArgs.LastMousePosition.X < right && DrawArgs.LastMousePosition.X > left && DrawArgs.LastMousePosition.Y > top
					 && DrawArgs.LastMousePosition.Y < bottom) {
					return true;
				}
				else {
					return false;
				}
			}
			catch {
			}
			return false;
		}

		public bool DownLoadImage(DrawArgs drawArgs) {
			if (this.imageLayer != null
				 || (this.downloadThread != null && this.downloadThread.IsAlive)) {
				return false;
			}

			this.LoadImage = true;
			this.drawArgs = drawArgs;
			//download the thing...
			if (!this.IsTextureAvailable) {
				this.downloadThread = new Thread(new ThreadStart(this.DownloadImage));
				this.downloadThread.Name = "DownloadableImageFromIconSet.DownloadImage";
				this.downloadThread.IsBackground = true;
				this.downloadThread.Start();
			}
			else {
				this.imageLayer = new ImageLayer(this.Name, this.m_ParentWorld, this.layerRadius - (float)m_ParentWorld.EquatorialRadius, this.saveTexturePath, this.south, this.north, this.west, this.east, 255, this._terrainAccessor);
			}
			return true;
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return DownLoadImage(drawArgs);
		}

		public void DownloadImage() {
			try {
				this.downloadState = DownloadState.Downloading;
				this.downloadProgress = 0.0f;

				if (File.Exists(this.saveTexturePath)) {
					File.Delete(this.saveTexturePath);
				}

				if (!Directory.Exists(Path.GetDirectoryName(this.saveTexturePath))) {
					Directory.CreateDirectory(Path.GetDirectoryName(this.saveTexturePath));
				}
				using (WebDownload dl = new WebDownload(imageUrl)) {
					dl.ProgressCallback += new DownloadProgressHandler(UpdateProgress);
					dl.DownloadMemory();

					this.downloadState = DownloadState.Converting;
					ImageHelper.ConvertToDxt1(dl.ContentStream, saveTexturePath);
				}

				if (this.downloadState
					 == DownloadState.Cancelled) {
					return;
				}

				this.IsTextureAvailable = true;
				if (this.LoadImage) {
					this.imageLayer = new ImageLayer(this.Name, m_ParentWorld, this.layerRadius - (float)m_ParentWorld.EquatorialRadius, this.saveTexturePath, this.south, this.north, this.west, this.east, 255, /*this.terrainInfo*/null);
				}
				this.downloadState = DownloadState.Pending;
			}
			catch (Exception caught) {
				Log.Write(caught);
			}
		}

		private void UpdateProgress(int current, int total) {
			downloadProgress = total > 0 ? (float)current / total : 0;
		}

		public override void Update(DrawArgs drawArgs) {
			if (this.LoadImage && this.imageLayer != null
				 && !this.imageLayer.Inited) {
				drawArgs.Repaint = true;
				this.imageLayer.Initialize(drawArgs);
			}

			if (!this.LoadImage && this.imageLayer != null
				 && this.imageLayer.Inited) {
				drawArgs.Repaint = true;
				this.imageLayer.Dispose();
			}

			if (this.imageLayer != null
				 && this.imageLayer.Inited) {
				this.imageLayer.Update(drawArgs);
			}
		}

		public override void Render(DrawArgs drawArgs) {
			if (!this.Inited) {
				return;
			}

			if (this.imageLayer != null && this.imageLayer.Inited
				 && this.LoadImage) {
				if (!this.imageLayer.DisableZBuffer) {
					this.imageLayer.DisableZBuffer = true;
				}

				this.imageLayer.Render(drawArgs);
				drawArgs.DefaultDrawingFont.DrawText(null, this.caption, new Rectangle(10, drawArgs.ScreenHeight - 50, drawArgs.ScreenWidth - 10, 50), DrawTextFormat.NoClip | DrawTextFormat.WordBreak, bottomLeftTextColor);
				return;
			}

			Vector3 centerPoint = MathEngine.SphericalToCartesian(0.5f * (this.north + this.south), 0.5f * (this.west + this.east), this.layerRadius);
			if (!drawArgs.WorldCamera.ViewFrustum.ContainsPoint(centerPoint)) {
				return;
			}

			Vector3 projectedPoint = drawArgs.WorldCamera.Project(centerPoint);

			// This value indicates the non-zoomed scale factor for icons
			const float baseScaling = 0.5f;

			// This value indicates the (maximum) added scale factor for when the mouse is over the icon
			float zoomScaling = 0.5f;

			// This value determines when the icon will start to zoom
			float selectionRadius = 0.5f * this.iconSize;

			float dx = DrawArgs.LastMousePosition.X - projectedPoint.X;
			float dy = DrawArgs.LastMousePosition.Y - projectedPoint.Y;
			float dr = (float)Math.Sqrt(dx * dx + dy * dy);

			bool renderDescription = false;
			if (dr > selectionRadius) {
				zoomScaling = 0;
			}
			else {
				zoomScaling *= (selectionRadius - dr) / selectionRadius;
				renderDescription = true;
				DrawArgs.MouseCursor = CursorType.Hand;
			}

			float scaleFactor = baseScaling + zoomScaling;
			int halfIconWidth = (int)(0.5f * this.iconSize * scaleFactor);
			int halfIconHeight = (int)(0.5f * this.iconSize * scaleFactor);

			if (this.downloadState
				 != DownloadState.Pending) {
				halfIconWidth = (int)(0.5f * this.iconSize);
				halfIconHeight = (int)(0.5f * this.iconSize);
			}

			float scaleWidth = (float)2.0f * halfIconWidth / this.spriteSize.Width;
			float scaleHeight = (float)2.0f * halfIconHeight / this.spriteSize.Height;

			this.sprite.Begin(SpriteFlags.AlphaBlend);
			this.sprite.Transform = Matrix.Transformation2D(new Vector2(0.0f, 0.0f), 0.0f, new Vector2(scaleWidth, scaleHeight), new Vector2(0, 0), 0.0f, new Vector2(projectedPoint.X, projectedPoint.Y));

			this.sprite.Draw(this.iconTexture, this.spriteSize, new Vector3(1.32f * this.iconSize, 1.32f * this.iconSize, 0), new Vector3(0, 0, 0), Color.White);
			this.sprite.End();

			if (this.caption != null && renderDescription) {
				drawArgs.DefaultDrawingFont.DrawText(null, this.caption, new Rectangle((int)projectedPoint.X + halfIconWidth + 5, (int)projectedPoint.Y - halfIconHeight, drawArgs.ScreenWidth, drawArgs.ScreenHeight), DrawTextFormat.WordBreak | DrawTextFormat.NoClip, Color.White.ToArgb());
			}

			if (this.downloadState != DownloadState.Pending
				 || this.IsTextureAvailable) {
				int progressColor = progressDefaultColor;
				if (this.IsTextureAvailable) {
					progressColor = progressColorLoading;
				}
				else if (this.downloadState
							== DownloadState.Converting) {
					progressColor = progressColorConversion;
					if (DateTime.Now.Millisecond < 500) {
						return;
					}
				}

				progressBarOutline[0].X = projectedPoint.X - halfIconWidth;
				progressBarOutline[0].Y = projectedPoint.Y + halfIconHeight + 1;
				progressBarOutline[0].Color = progressColor;

				progressBarOutline[1].X = projectedPoint.X + halfIconWidth;
				progressBarOutline[1].Y = projectedPoint.Y + halfIconHeight + 1;
				progressBarOutline[1].Color = progressColor;

				progressBarOutline[2].X = projectedPoint.X + halfIconWidth;
				progressBarOutline[2].Y = projectedPoint.Y + halfIconHeight + 3;
				progressBarOutline[2].Color = progressColor;

				progressBarOutline[3].X = projectedPoint.X - halfIconWidth;
				progressBarOutline[3].Y = projectedPoint.Y + halfIconHeight + 3;
				progressBarOutline[3].Color = progressColor;

				progressBarOutline[4].X = projectedPoint.X - halfIconWidth;
				progressBarOutline[4].Y = projectedPoint.Y + halfIconHeight + 1;
				progressBarOutline[4].Color = progressColor;

				drawArgs.Device.VertexFormat = CustomVertex.TransformedColored.Format;
				drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
				drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, 4, progressBarOutline);

				int barlength = (int)(downloadProgress * 2 * halfIconWidth);

				progressBar[0].X = projectedPoint.X - halfIconWidth;
				progressBar[0].Y = projectedPoint.Y + halfIconHeight + 1;
				progressBar[0].Color = progressColor;

				progressBar[1].X = projectedPoint.X - halfIconWidth;
				progressBar[1].Y = projectedPoint.Y + halfIconHeight + 3;
				progressBar[1].Color = progressColor;

				progressBar[2].X = projectedPoint.X - halfIconWidth + barlength;
				progressBar[2].Y = projectedPoint.Y + halfIconHeight + 1;
				progressBar[2].Color = progressColor;

				progressBar[3].X = projectedPoint.X - halfIconWidth + barlength;
				progressBar[3].Y = projectedPoint.Y + halfIconHeight + 3;
				progressBar[3].Color = progressColor;

				drawArgs.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, progressBar);
			}
		}

		private void nt_ProgressCallback(int bytesRead, int totalBytes) {
			this.downloadProgress = (float)bytesRead / totalBytes;
			this.drawArgs.Repaint = true;
		}
	}

}
