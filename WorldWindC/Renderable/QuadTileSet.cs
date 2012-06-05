using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Threading;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Camera;
using WorldWind.Utilities;
using WorldWind.VisualControl;

namespace WorldWind.Renderable {
	/// <summary>
	/// Main class for image tile rendering.  Uses the Terrain Manager to query height values for 3D 
	/// terrain rendering.
	/// Relies on an Update thread to refresh the "tiles" based on lat/lon/view range
	/// </summary>
	public class QuadTileSet : RenderableObject {
		#region Private Members
		private bool m_RenderStruts = true;
		protected string m_ServerLogoFilePath;
		protected Image m_ServerLogoImage;

		private Hashtable m_topmostTiles = new Hashtable();
		protected double m_north;
		protected double m_south;
		protected double m_west;
		protected double m_east;
		private bool renderFileNames = false;

		private Texture m_iconTexture;
		private Sprite sprite;
		private Rectangle m_spriteSize;
		private ProgressBar progressBar;

		private Blend m_sourceBlend = Blend.BlendFactor;
		private Blend m_destinationBlend = Blend.InvBlendFactor;

		// If this value equals CurrentFrameStartTicks the Z buffer needs to be cleared
		private static long lastRenderTime;

		//public static int MaxConcurrentDownloads = 3;
		private double m_layerRadius;
		private bool m_alwaysRenderBaseTiles;
		private float m_tileDrawSpread;
		private float m_tileDrawDistance;
		private bool m_isDownloadingElevation;
		private int m_numberRetries;
		private Hashtable m_downloadRequests = new Hashtable();
		private int m_maxQueueSize = 400;
		private bool m_terrainMapped;
		private ImageStore m_imageStore;
		private CameraBase m_camera;
		private GeoSpatialDownloadRequest[] m_activeDownloads = new GeoSpatialDownloadRequest[20];
		private DateTime[] m_downloadStarted = new DateTime[20];
		private TimeSpan m_connectionWaitTime = TimeSpan.FromMinutes(2);
		private DateTime m_connectionWaitStart;
		private bool m_isConnectionWaiting;
		private bool m_enableColorKeying;
		#endregion

		#region public members
		/// <summary>
		/// Texture showing download in progress
		/// </summary>
		public static Texture DownloadInProgressTexture;

		/// <summary>
		/// Texture showing queued download 
		/// </summary>
		public static Texture DownloadQueuedTexture;

		/// <summary>
		/// Texture showing terrain download in progress
		/// </summary>
		public static Texture DownloadTerrainTexture;

		/// <summary>
		/// If images in cache are older than expration time a refresh 
		/// from server will be attempted.
		/// </summary>
		public TimeSpan CacheExpirationTime = TimeSpan.MaxValue;

		public int ColorKey; // default: 100% transparent black = transparent

		/// <summary>
		/// If a color range is to be transparent this specifies the brightest transparent color.
		/// The darkest transparent color is set using ColorKey.
		/// </summary>
		public int ColorKeyMax;
		#endregion

		public bool RenderStruts {
			get {
				return m_RenderStruts;
			}
			set {
				m_RenderStruts = value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.QuadTileSet"/> class.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="parentWorld"></param>
		/// <param name="distanceAboveSurface"></param>
		/// <param name="north"></param>
		/// <param name="south"></param>
		/// <param name="west"></param>
		/// <param name="east"></param>
		/// <param name="terrainAccessor"></param>
		/// <param name="imageAccessor"></param>
		public QuadTileSet(string name, World parentWorld, double distanceAboveSurface, double north, double south, double west, double east, bool terrainMapped, ImageStore imageStore) : base(name, parentWorld) {
			float layerRadius = (float) (parentWorld.EquatorialRadius + distanceAboveSurface);
			m_north = north;
			m_south = south;
			m_west = west;
			m_east = east;

			// Layer center position
			Position = MathEngine.SphericalToCartesian((north + south)*0.5f, (west + east)*0.5f, layerRadius);

			m_layerRadius = layerRadius;
			m_tileDrawDistance = 3.5f;
			m_tileDrawSpread = 2.9f;
			m_imageStore = imageStore;
			m_terrainMapped = terrainMapped;

			// Default terrain mapped imagery to terrain mapped priority 
			if (terrainMapped) {
				m_renderPriority = RenderPriority.TerrainMappedImages;
			}
		}

		#region Public properties
		/// <summary>
		/// Path to a thumbnail image (e.g. for use as a download indicator).
		/// </summary>
		public virtual string ServerLogoFilePath {
			get {
				return m_ServerLogoFilePath;
			}
			set {
				m_ServerLogoFilePath = value;
			}
		}

		public bool RenderFileNames {
			get {
				return renderFileNames;
			}
			set {
				renderFileNames = value;
			}
		}

		/// <summary>
		/// The image referenced by ServerLogoFilePath. 
		/// </summary>
		public virtual Image ServerLogoImage {
			get {
				if (m_ServerLogoImage == null) {
					if (m_ServerLogoFilePath == null) {
						return null;
					}
					try {
						if (File.Exists(m_ServerLogoFilePath)) {
							m_ServerLogoImage = ImageHelper.LoadImage(m_ServerLogoFilePath);
						}
					}
					catch {}
				}
				return m_ServerLogoImage;
			}
		}

		/// <summary>
		/// Path to a thumbnail image (or download indicator if none available)
		/// </summary>
		public override Image ThumbnailImage {
			get {
				if (base.ThumbnailImage != null) {
					return base.ThumbnailImage;
				}

				return ServerLogoImage;
			}
		}

		/// <summary>
		/// Path to a thumbnail image (e.g. for use as a download indicator).
		/// </summary>
		public virtual bool HasTransparentRange {
			get {
				return (ColorKeyMax != 0);
			}
		}

		/// <summary>
		/// Source blend when rendering non-opaque layer
		/// </summary>
		public Blend SourceBlend {
			get {
				return m_sourceBlend;
			}
			set {
				m_sourceBlend = value;
			}
		}

		/// <summary>
		/// Destination blend when rendering non-opaque layer
		/// </summary>
		public Blend DestinationBlend {
			get {
				return m_destinationBlend;
			}
			set {
				m_destinationBlend = value;
			}
		}

		/// <summary>
		/// North bound for this QuadTileSet
		/// </summary>
		public double North {
			get {
				return m_north;
			}
		}

		/// <summary>
		/// West bound for this QuadTileSet
		/// </summary>
		public double West {
			get {
				return m_west;
			}
		}

		/// <summary>
		/// South bound for this QuadTileSet
		/// </summary>
		public double South {
			get {
				return m_south;
			}
		}

		/// <summary>
		/// East bound for this QuadTileSet
		/// </summary>
		public double East {
			get {
				return m_east;
			}
		}

		/// <summary>
		/// Controls if images are rendered using ColorKey (transparent areas)
		/// </summary>
		public bool EnableColorKeying {
			get {
				return m_enableColorKeying;
			}
			set {
				m_enableColorKeying = value;
			}
		}

		public DateTime ConnectionWaitStart {
			get {
				return m_connectionWaitStart;
			}
		}

		public bool IsConnectionWaiting {
			get {
				return m_isConnectionWaiting;
			}
		}

		public double LayerRadius {
			get {
				return m_layerRadius;
			}
			set {
				m_layerRadius = value;
			}
		}

		public bool AlwaysRenderBaseTiles {
			get {
				return m_alwaysRenderBaseTiles;
			}
			set {
				m_alwaysRenderBaseTiles = value;
			}
		}

		public float TileDrawSpread {
			get {
				return m_tileDrawSpread;
			}
			set {
				m_tileDrawSpread = value;
			}
		}

		public float TileDrawDistance {
			get {
				return m_tileDrawDistance;
			}
			set {
				m_tileDrawDistance = value;
			}
		}

		public bool IsDownloadingElevation {
			get {
				return m_isDownloadingElevation;
			}
			set {
				m_isDownloadingElevation = value;
			}
		}

		public int NumberRetries {
			get {
				return m_numberRetries;
			}
			set {
				m_numberRetries = value;
			}
		}

		/// <summary>
		/// Controls rendering (flat or terrain mapped)
		/// </summary>
		public bool TerrainMapped {
			get {
				return m_terrainMapped;
			}
			set {
				m_terrainMapped = value;
			}
		}

		public ImageStore ImageStore {
			get {
				return m_imageStore;
			}
		}

		/// <summary>
		/// Tiles in the request for download queue
		/// </summary>
		public Hashtable DownloadRequests {
			get {
				return m_downloadRequests;
			}
		}

		/// <summary>
		/// The camera controlling the layers update logic
		/// </summary>
		public CameraBase Camera {
			get {
				return m_camera;
			}
			set {
				m_camera = value;
			}
		}
		#endregion

		public override void Initialize(DrawArgs drawArgs) {
			Camera = DrawArgs.Camera;

			// Initialize download rectangles
			if (DownloadInProgressTexture == null) {
				DownloadInProgressTexture = CreateDownloadRectangle(DrawArgs.StaticDevice, World.Settings.DownloadProgressColor, 0);
			}
			if (DownloadQueuedTexture == null) {
				DownloadQueuedTexture = CreateDownloadRectangle(DrawArgs.StaticDevice, World.Settings.DownloadQueuedColor, 0);
			}
			if (DownloadTerrainTexture == null) {
				DownloadTerrainTexture = CreateDownloadRectangle(DrawArgs.StaticDevice, World.Settings.DownloadTerrainRectangleColor, 0);
			}

			try {
				lock (m_topmostTiles.SyncRoot) {
					foreach (QuadTile qt in m_topmostTiles.Values) {
						qt.Initialize();
					}
				}
			}
			catch {}
			Inited = true;
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}

		public override void Update(DrawArgs drawArgs) {
			if (!Inited) {
				Initialize(drawArgs);
			}

			if (ImageStore.LevelZeroTileSizeDegrees < 180) {
				// Check for layer outside view
				double vrd = DrawArgs.Camera.ViewRange.Degrees;
				double latitudeMax = DrawArgs.Camera.Latitude.Degrees + vrd;
				double latitudeMin = DrawArgs.Camera.Latitude.Degrees - vrd;
				double longitudeMax = DrawArgs.Camera.Longitude.Degrees + vrd;
				double longitudeMin = DrawArgs.Camera.Longitude.Degrees - vrd;
				if (latitudeMax < m_south || latitudeMin > m_north || longitudeMax < m_west
				    || longitudeMin > m_east) {
					return;
				}
			}

			if (DrawArgs.Camera.ViewRange*0.5f
			    > Angle.FromDegrees(TileDrawDistance*ImageStore.LevelZeroTileSizeDegrees)) {
				lock (m_topmostTiles.SyncRoot) {
					foreach (QuadTile qt in m_topmostTiles.Values) {
						qt.Dispose();
					}
					m_topmostTiles.Clear();
					ClearDownloadRequests();
				}

				return;
			}

			RemoveInvisibleTiles(DrawArgs.Camera);
			try {
				int middleRow = MathEngine.GetRowFromLatitude(DrawArgs.Camera.Latitude, ImageStore.LevelZeroTileSizeDegrees);
				int middleCol = MathEngine.GetColFromLongitude(DrawArgs.Camera.Longitude, ImageStore.LevelZeroTileSizeDegrees);

				double middleSouth = -90.0f + middleRow*ImageStore.LevelZeroTileSizeDegrees;
				double middleNorth = -90.0f + middleRow*ImageStore.LevelZeroTileSizeDegrees + ImageStore.LevelZeroTileSizeDegrees;
				double middleWest = -180.0f + middleCol*ImageStore.LevelZeroTileSizeDegrees;
				double middleEast = -180.0f + middleCol*ImageStore.LevelZeroTileSizeDegrees + ImageStore.LevelZeroTileSizeDegrees;

				double middleCenterLat = 0.5f*(middleNorth + middleSouth);
				double middleCenterLon = 0.5f*(middleWest + middleEast);

				int tileSpread = 4;
				for (int i = 0; i < tileSpread; i++) {
					for (double j = middleCenterLat - i*ImageStore.LevelZeroTileSizeDegrees; j < middleCenterLat + i*ImageStore.LevelZeroTileSizeDegrees; j += ImageStore.LevelZeroTileSizeDegrees) {
						for (double k = middleCenterLon - i*ImageStore.LevelZeroTileSizeDegrees; k < middleCenterLon + i*ImageStore.LevelZeroTileSizeDegrees; k += ImageStore.LevelZeroTileSizeDegrees) {
							int curRow = MathEngine.GetRowFromLatitude(Angle.FromDegrees(j), ImageStore.LevelZeroTileSizeDegrees);
							int curCol = MathEngine.GetColFromLongitude(Angle.FromDegrees(k), ImageStore.LevelZeroTileSizeDegrees);
							long key = ((long) curRow << 32) + curCol;

							QuadTile qt = (QuadTile) m_topmostTiles[key];
							if (qt != null) {
								qt.Update(drawArgs);
								continue;
							}

							// Check for tile outside layer boundaries
							double west = -180.0f + curCol*ImageStore.LevelZeroTileSizeDegrees;
							if (west > m_east) {
								continue;
							}

							double east = west + ImageStore.LevelZeroTileSizeDegrees;
							if (east < m_west) {
								continue;
							}

							double south = -90.0f + curRow*ImageStore.LevelZeroTileSizeDegrees;
							if (south > m_north) {
								continue;
							}

							double north = south + ImageStore.LevelZeroTileSizeDegrees;
							if (north < m_south) {
								continue;
							}

							qt = new QuadTile(south, north, west, east, 0, this);
							if (DrawArgs.Camera.ViewFrustum.Intersects(qt.BoundingBox)) {
								lock (m_topmostTiles.SyncRoot)
									m_topmostTiles.Add(key, qt);
								qt.Update(drawArgs);
							}
						}
					}
				}
			}
			catch (ThreadAbortException) {}
			catch (Exception caught) {
				Log.Write(caught);
			}
		}

		private void RemoveInvisibleTiles(CameraBase camera) {
			ArrayList deletionList = new ArrayList();

			lock (m_topmostTiles.SyncRoot) {
				foreach (long key in m_topmostTiles.Keys) {
					QuadTile qt = (QuadTile) m_topmostTiles[key];
					if (!camera.ViewFrustum.Intersects(qt.BoundingBox)) {
						deletionList.Add(key);
					}
				}

				foreach (long deleteThis in deletionList) {
					QuadTile qt = (QuadTile) m_topmostTiles[deleteThis];
					if (qt != null) {
						m_topmostTiles.Remove(deleteThis);
						qt.Dispose();
					}
				}
			}
		}

		public override void Render(DrawArgs drawArgs) {
			try {
				lock (m_topmostTiles.SyncRoot) {
					if (m_topmostTiles.Count <= 0) {
						return;
					}

					Device device = DrawArgs.StaticDevice;

					// Temporary fix: Clear Z buffer between rendering 
					// terrain mapped layers to avoid Z buffer fighting
					//if (lastRenderTime == DrawArgs.CurrentFrameStartTicks)
					device.Clear(ClearFlags.ZBuffer, 0, 1.0f, 0);
					device.RenderState.ZBufferEnable = true;
					lastRenderTime = DrawArgs.CurrentFrameStartTicks;

					//				if (m_renderPriority < RenderPriority.TerrainMappedImages)
					//					// No Z buffering needed for "flat" layers
					//					device.RenderState.ZBufferEnable = false;

					if (m_opacity < 255
					    && device.DeviceCaps.DestinationBlendCaps.SupportsBlendFactor) {
						// Blend
						device.RenderState.AlphaBlendEnable = true;
						device.RenderState.SourceBlend = m_sourceBlend;
						device.RenderState.DestinationBlend = m_destinationBlend;
						// Set Red, Green and Blue = opacity
						device.RenderState.BlendFactorColor = (m_opacity << 16) | (m_opacity << 8) | m_opacity;
					}
					else if (EnableColorKeying && device.DeviceCaps.TextureCaps.SupportsAlpha) {
						device.RenderState.AlphaBlendEnable = true;
						device.RenderState.SourceBlend = Blend.SourceAlpha;
						device.RenderState.DestinationBlend = Blend.InvSourceAlpha;
					}

					// Set the render states for rendering of quad tiles.  
					// Any quad tile rendering code that adjusts the state should restore it to below values afterwards.
					device.VertexFormat = CustomVertex.PositionTextured.Format;
					device.SetTextureStageState(0, TextureStageStates.ColorOperation, (int) TextureOperation.SelectArg1);
					device.SetTextureStageState(0, TextureStageStates.AlphaArgument1, (int) TextureArgument.TextureColor);
					device.SetTextureStageState(0, TextureStageStates.AlphaOperation, (int) TextureOperation.SelectArg1);

					// Be prepared for multi-texturing
					device.SetTextureStageState(1, TextureStageStates.ColorArgument2, (int) TextureArgument.Current);
					device.SetTextureStageState(1, TextureStageStates.ColorArgument1, (int) TextureArgument.TextureColor);
					device.SetTextureStageState(1, TextureStageStates.TextureCoordinateIndex, 0);

					foreach (QuadTile qt in m_topmostTiles.Values) {
						qt.Render(drawArgs);
					}

					// Restore device states
					if (m_renderPriority < RenderPriority.TerrainMappedImages) {
						device.RenderState.ZBufferEnable = true;
					}

					if (m_opacity < 255 || EnableColorKeying) {
						// Restore alpha blend state
						device.RenderState.SourceBlend = Blend.SourceAlpha;
						device.RenderState.DestinationBlend = Blend.InvSourceAlpha;
					}
				}
			}
			catch {}
			finally {
				if (IsConnectionWaiting) {
					if (DateTime.Now.Subtract(TimeSpan.FromSeconds(15)) < ConnectionWaitStart) {
						string s = "Problem connecting to server... Trying again in 2 minutes.\n";
						drawArgs.UpperLeftCornerText += s;
					}
				}

				foreach (GeoSpatialDownloadRequest request in m_activeDownloads) {
					if (request != null
					    && !request.IsComplete) {
						RenderDownloadProgress(drawArgs, request);
						// Only render the first
						break;
					}
				}
			}
		}

		public void RenderDownloadProgress(DrawArgs drawArgs, GeoSpatialDownloadRequest request) {
			int halfIconHeight = 24;
			int halfIconWidth = 24;

			Vector3 projectedPoint = new Vector3(DrawArgs.StaticParentControl.Width - halfIconWidth - 10, DrawArgs.StaticParentControl.Height - 34, 0.5f);

			// Render progress bar
			if (progressBar == null) {
				progressBar = new ProgressBar(40, 4);
			}
			progressBar.Draw(drawArgs, projectedPoint.X, projectedPoint.Y + 24, request.ProgressPercent, World.Settings.DownloadProgressColor.ToArgb());
			DrawArgs.StaticDevice.RenderState.ZBufferEnable = true;

			// Render server logo
			if (ServerLogoFilePath == null) {
				return;
			}

			if (m_iconTexture == null) {
				m_iconTexture = ImageHelper.LoadIconTexture(ServerLogoFilePath);
			}

			if (sprite == null) {
				using (Surface s = m_iconTexture.GetSurfaceLevel(0)) {
					SurfaceDescription desc = s.Description;
					m_spriteSize = new Rectangle(0, 0, desc.Width, desc.Height);
				}

				this.sprite = new Sprite(DrawArgs.StaticDevice);
			}

			float scaleWidth = (float) 2.0f*halfIconWidth/m_spriteSize.Width;
			float scaleHeight = (float) 2.0f*halfIconHeight/m_spriteSize.Height;

			this.sprite.Begin(SpriteFlags.AlphaBlend);
			this.sprite.Transform = Matrix.Transformation2D(new Vector2(0.0f, 0.0f), 0.0f, new Vector2(scaleWidth, scaleHeight), new Vector2(0, 0), 0.0f, new Vector2(projectedPoint.X, projectedPoint.Y));

			this.sprite.Draw(m_iconTexture, m_spriteSize, new Vector3(1.32f*48, 1.32f*48, 0), new Vector3(0, 0, 0), World.Settings.DownloadLogoColor);
			this.sprite.End();
		}

		public override void Dispose() {
			Inited = false;

			// flush downloads
			for (int i = 0; i < World.Settings.MaxSimultaneousDownloads; i++) {
				if (m_activeDownloads[i] != null) {
					m_activeDownloads[i].Dispose();
					m_activeDownloads[i] = null;
				}
			}

			foreach (QuadTile qt in m_topmostTiles.Values) {
				qt.Dispose();
			}

			if (m_iconTexture != null) {
				m_iconTexture.Dispose();
				m_iconTexture = null;
			}

			if (this.sprite != null) {
				this.sprite.Dispose();
				this.sprite = null;
			}
		}

		public virtual void ResetCacheForCurrentView(CameraBase camera) {
			if (!ImageStore.IsDownloadableLayer) {
				return;
			}

			ArrayList deletionList = new ArrayList();
			//reset "root" tiles that intersect current view
			lock (m_topmostTiles.SyncRoot) {
				foreach (long key in m_topmostTiles.Keys) {
					QuadTile qt = (QuadTile) m_topmostTiles[key];
					if (camera.ViewFrustum.Intersects(qt.BoundingBox)) {
						qt.ResetCache();
						deletionList.Add(key);
					}
				}

				foreach (long deletionKey in deletionList) {
					m_topmostTiles.Remove(deletionKey);
				}
			}
		}

		public void ClearDownloadRequests() {
			lock (m_downloadRequests.SyncRoot) {
				m_downloadRequests.Clear();
			}
		}

		public virtual void AddToDownloadQueue(CameraBase camera, GeoSpatialDownloadRequest newRequest) {
			QuadTile key = newRequest.QuadTile;
			key.WaitingForDownload = true;
			lock (m_downloadRequests.SyncRoot) {
				if (m_downloadRequests.Contains(key)) {
					return;
				}

				m_downloadRequests.Add(key, newRequest);

				if (m_downloadRequests.Count >= m_maxQueueSize) {
					//remove spatially farthest request
					GeoSpatialDownloadRequest farthestRequest = null;
					Angle curDistance = Angle.Zero;
					Angle farthestDistance = Angle.Zero;
					foreach (GeoSpatialDownloadRequest curRequest in m_downloadRequests.Values) {
						curDistance = MathEngine.SphericalDistance(curRequest.QuadTile.CenterLatitude, curRequest.QuadTile.CenterLongitude, camera.Latitude, camera.Longitude);

						if (curDistance > farthestDistance) {
							farthestRequest = curRequest;
							farthestDistance = curDistance;
						}
					}

					farthestRequest.Dispose();
					farthestRequest.QuadTile.DownloadRequest = null;
					m_downloadRequests.Remove(farthestRequest.QuadTile);
				}
			}

			ServiceDownloadQueue();
		}

		/// <summary>
		/// Removes a request from the download queue.
		/// </summary>
		public virtual void RemoveFromDownloadQueue(GeoSpatialDownloadRequest removeRequest) {
			lock (m_downloadRequests.SyncRoot) {
				QuadTile key = removeRequest.QuadTile;
				GeoSpatialDownloadRequest request = (GeoSpatialDownloadRequest) m_downloadRequests[key];
				if (request != null) {
					m_downloadRequests.Remove(key);
					request.QuadTile.DownloadRequest = null;
				}
			}
		}

		/// <summary>
		/// Starts downloads when there are threads available
		/// </summary>
		public virtual void ServiceDownloadQueue() {
			lock (m_downloadRequests.SyncRoot) {
				for (int i = 0; i < World.Settings.MaxSimultaneousDownloads; i++) {
					if (m_activeDownloads[i] == null) {
						continue;
					}

					if (!m_activeDownloads[i].IsComplete) {
						continue;
					}

					m_activeDownloads[i].Cancel();
					m_activeDownloads[i].Dispose();
					m_activeDownloads[i] = null;
				}

				if (NumberRetries >= 5 || m_isConnectionWaiting) {
					// Anti hammer in effect
					if (!m_isConnectionWaiting) {
						m_connectionWaitStart = DateTime.Now;
						m_isConnectionWaiting = true;
					}

					if (DateTime.Now.Subtract(m_connectionWaitTime) > m_connectionWaitStart) {
						NumberRetries = 0;
						m_isConnectionWaiting = false;
					}
					return;
				}

				// Queue new downloads
				for (int i = 0; i < World.Settings.MaxSimultaneousDownloads; i++) {
					if (m_activeDownloads[i] != null) {
						continue;
					}

					if (m_downloadRequests.Count <= 0) {
						continue;
					}

					m_activeDownloads[i] = GetClosestDownloadRequest();
					if (m_activeDownloads[i] != null) {
						m_downloadStarted[i] = DateTime.Now;
						m_activeDownloads[i].StartDownload();
					}
				}
			}
		}

		/// <summary>
		/// Finds the "best" tile from queue
		/// </summary>
		public virtual GeoSpatialDownloadRequest GetClosestDownloadRequest() {
			GeoSpatialDownloadRequest closestRequest = null;
			float largestArea = float.MinValue;

			lock (m_downloadRequests.SyncRoot) {
				foreach (GeoSpatialDownloadRequest curRequest in m_downloadRequests.Values) {
					if (curRequest.IsDownloading) {
						continue;
					}

					QuadTile qt = curRequest.QuadTile;
					if (!m_camera.ViewFrustum.Intersects(qt.BoundingBox)) {
						continue;
					}

					float screenArea = qt.BoundingBox.CalcRelativeScreenArea(m_camera);
					if (screenArea > largestArea) {
						largestArea = screenArea;
						closestRequest = curRequest;
					}
				}
			}

			return closestRequest;
		}

		/// <summary>
		/// Creates a tile download indication texture 
		/// </summary>
		private static Texture CreateDownloadRectangle(Device device, Color color, int padding) {
			int mid = 128;
			using (Bitmap i = new Bitmap(2*mid, 2*mid)) {
				using (Graphics g = Graphics.FromImage(i)) {
					using (Pen pen = new Pen(color)) {
						int width = mid - 1 - 2*padding;
						g.DrawRectangle(pen, padding, padding, width, width);
						g.DrawRectangle(pen, mid + padding, padding, width, width);
						g.DrawRectangle(pen, padding, mid + padding, width, width);
						g.DrawRectangle(pen, mid + padding, mid + padding, width, width);

						Texture texture = new Texture(device, i, Usage.None, Pool.Managed);
						return texture;
					}
				}
			}
		}
	}
}