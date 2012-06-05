using System;
using System.Collections;
using System.Drawing;
using System.IO;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Configuration;
using WorldWind.Renderable;
using WorldWind.Terrain;
using WorldWind.Utilities;

namespace WorldWind {
	/// <summary>
	/// 一个虚拟的世界
	/// </summary>
	public class World : RenderableObject {
		
		public static WorldSettings _settings;

		private double _equatorialRadius;
		private TerrainAccessor _terrainAccessor;
		private RenderableObjectList _renderableObjects;
		private IList _onScreenMessages;
		private DateTime _lastElevationUpdate = DateTime.Now;
		private WorldSurfaceRenderer _WorldSurfaceRenderer = null;

		/// <summary>
		/// 用户设置
		/// </summary>
		public static WorldSettings Settings {
			get {
				if (_settings == null) {
					_settings = new WorldSettings();
				}
				return _settings;
			}
			set {
				_settings = value;
			}
		}
		/// <summary>
		/// 在屏幕上显示的消息列表
		/// </summary>
		public IList OnScreenMessages {
			get {
				return this._onScreenMessages;
			}
			set {
				this._onScreenMessages = value;
			}
		}

		public WorldSurfaceRenderer WorldSurfaceRenderer {
			get {
				return _WorldSurfaceRenderer;
			}
		}

		/// <summary>
		/// 是否是地球
		/// </summary>
		public bool IsEarth {
			get {
				return this.Name == "Earth";
			}
		}

		/// <summary>
		/// 初始化一个 <see cref= "T:WorldWind.World"/> 新实例.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="position"></param>
		/// <param name="orientation"></param>
		/// <param name="equatorialRadius"></param>
		/// <param name="cacheDirectory"></param>
		/// <param name="terrainAccessor"></param>
		public World(string name, Vector3 position, Quaternion orientation, double equatorialRadius, string cacheDirectory, TerrainAccessor terrainAccessor) : base(name, position, orientation) {
			this._equatorialRadius = equatorialRadius;
			this._terrainAccessor = terrainAccessor;
			this._renderableObjects = new RenderableObjectList(this.Name);
			this.MetaData.Add("CacheDirectory", cacheDirectory);
		}

		public void SetLayerOpacity(string category, string name, float opacity) {
			this.SetLayerOpacity(category, name, opacity, this._renderableObjects);
		}

		private static string GetRenderablePathString(RenderableObject renderable) {
			if (renderable.ParentList == null) {
				return renderable.Name;
			}
			else {
				return GetRenderablePathString(renderable.ParentList) + Path.DirectorySeparatorChar + renderable.Name;
			}
		}

		private void SetLayerOpacity(string category, string name, float opacity, RenderableObject ro) {
			foreach (string key in ro.MetaData.Keys) {
				if (String.Compare(key, category, true) == 0) {
					if (ro.MetaData[key].GetType() == typeof (String)) {
						string curValue = ro.MetaData[key] as string;
						if (String.Compare(curValue, name, true) == 0) {
							ro.Opacity = (byte) (255*opacity);
						}
					}
					break;
				}
			}

			RenderableObjectList rol = ro as RenderableObjectList;
			if (rol != null) {
				foreach (RenderableObject childRo in rol.ChildObjects) {
					SetLayerOpacity(category, name, opacity, childRo);
				}
			}
		}

		/// <summary>
		/// Deserializes settings from default location
		/// </summary>
		public static void LoadSettings() {
			try {
				Settings = (WorldSettings) SettingsBase.Load(Settings);
			}
			catch (Exception caught) {
				Log.Write(caught);
			}
		}

		/// <summary>
		/// Deserializes settings from specified location
		/// </summary>
		public static void LoadSettings(string directory) {
			try {
				Settings = (WorldSettings) SettingsBase.LoadFromPath(Settings, directory);
			}
			catch (Exception caught) {
				Log.Write(caught);
			}
		}

		public TerrainAccessor TerrainAccessor {
			get {
				return this._terrainAccessor;
			}
			set {
				this._terrainAccessor = value;
			}
		}

		public double EquatorialRadius {
			get {
				return this._equatorialRadius;
			}
		}

		public RenderableObjectList RenderableObjects {
			get {
				return this._renderableObjects;
			}
			set {
				this._renderableObjects = value;
			}
		}

		public override void Initialize(DrawArgs drawArgs) {
			try {
				if (this.Inited) {
					return;
				}
				this.RenderableObjects.Initialize(drawArgs);
			}
			catch (Exception ex) {
				Log.DebugWrite(ex);
			}
			finally {
				this.Inited = true;
			}
		}

		private void DrawAxis(DrawArgs drawArgs) {
			CustomVertex.PositionColored[] axis = new CustomVertex.PositionColored[2];
			Vector3 topV = MathEngine.SphericalToCartesian(90, 0, 1.15f * this.EquatorialRadius);
			axis[0].X = topV.X;
			axis[0].Y = topV.Y;
			axis[0].Z = topV.Z;

			axis[0].Color = Color.Pink.ToArgb();

			Vector3 botV = MathEngine.SphericalToCartesian(-90, 0, 1.15f * this.EquatorialRadius);
			axis[1].X = botV.X;
			axis[1].Y = botV.Y;
			axis[1].Z = botV.Z;
			axis[1].Color = Color.Pink.ToArgb();

			drawArgs.Device.VertexFormat = CustomVertex.PositionColored.Format;
			drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
			drawArgs.Device.Transform.World = Matrix.Translation((float) -drawArgs.WorldCamera.ReferenceCenter.X, (float) -drawArgs.WorldCamera.ReferenceCenter.Y, (float) -drawArgs.WorldCamera.ReferenceCenter.Z);

			drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, 1, axis);
			drawArgs.Device.Transform.World = drawArgs.WorldCamera.WorldMatrix;
		}

		public override void Update(DrawArgs drawArgs) {
			if (!this.Inited) {
				this.Initialize(drawArgs);
			}

			if (this.RenderableObjects != null) {
				this.RenderableObjects.Update(drawArgs);
			}
			if (this._WorldSurfaceRenderer != null) {
				this._WorldSurfaceRenderer.Update(drawArgs);
			}

			if (this.TerrainAccessor != null) {
				if (drawArgs.WorldCamera.Altitude < 300000) {
					if (DateTime.Now - this._lastElevationUpdate > TimeSpan.FromMilliseconds(500)) {
						drawArgs.WorldCamera.TerrainElevation = (short) this.TerrainAccessor.GetElevationAt(drawArgs.WorldCamera.Latitude.Degrees, drawArgs.WorldCamera.Longitude.Degrees, 100.0/drawArgs.WorldCamera.ViewRange.Degrees);
						this._lastElevationUpdate = DateTime.Now;
					}
				}
				else {
					drawArgs.WorldCamera.TerrainElevation = 0;
				}
			}
			else {
				drawArgs.WorldCamera.TerrainElevation = 0;
			}
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return this._renderableObjects.PerformSelectionAction(drawArgs);
		}

		public override void Render(DrawArgs drawArgs) {
			if (_WorldSurfaceRenderer != null && Settings.UseWorldSurfaceRenderer) {
				_WorldSurfaceRenderer.RenderSurfaceImages(drawArgs);
			}
			RenderableObjects.Render(drawArgs);

			if (Settings.ShowPlanetAxis) {
				this.DrawAxis(drawArgs);
			}
		}

		private void SaveRenderableState(RenderableObject ro) {
			string path = GetRenderablePathString(ro);
			bool found = false;
			for (int i = 0; i < Settings.loadedLayers.Count; i++) {
				string s = (string) Settings.loadedLayers[i];
				if (s.Equals(path)) {
					if (!ro.IsOn) {
						Settings.loadedLayers.RemoveAt(i);
						break;
					}
					else {
						found = true;
					}
				}
			}

			if (!found && ro.IsOn) {
				Settings.loadedLayers.Add(path);
			}
		}

		private void SaveRenderableStates(RenderableObjectList rol) {
			SaveRenderableState(rol);

			foreach (RenderableObject ro in rol.ChildObjects) {
				if (ro is RenderableObjectList) {
					RenderableObjectList childRol = (RenderableObjectList) ro;
					SaveRenderableStates(childRol);
				}
				else {
					SaveRenderableState(ro);
				}
			}
		}

		public override void Dispose() {
			SaveRenderableStates(RenderableObjects);

			if (this.RenderableObjects != null) {
				this.RenderableObjects.Dispose();
				this.RenderableObjects = null;
			}

			if (_WorldSurfaceRenderer != null) {
				_WorldSurfaceRenderer.Dispose();
			}
		}

		/// <summary>
		/// Computes the great circle distance between two pairs of lat/longs.
		/// </summary>
		public static Angle ApproxAngularDistance(Angle latA, Angle lonA, Angle latB, Angle lonB) {
			Angle dlon = lonB - lonA;
			Angle dlat = latB - latA;
			double k = Math.Sin(dlat.Radians * 0.5);
			double l = Math.Sin(dlon.Radians * 0.5);
			double a = k * k + Math.Cos(latA.Radians) * Math.Cos(latB.Radians) * l * l;
			double c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
			return Angle.FromRadians(c);
		}

		/// <summary>
		/// Computes the distance between two pairs of lat/longs in meters.
		/// </summary>
		public double ApproxDistance(Angle latA, Angle lonA, Angle latB, Angle lonB) {
			double distance = _equatorialRadius * ApproxAngularDistance(latA, lonA, latB, lonB).Radians;
			return distance;
		}

		/// <summary>
		/// Intermediate points on a great circle
		/// In previous sections we have found intermediate points on a great circle given either
		/// the crossing latitude or longitude. Here we find points (lat,lon) a given fraction of the
		/// distance (d) between them. Suppose the starting point is (lat1,lon1) and the final point
		/// (lat2,lon2) and we want the point a fraction f along the great circle route. f=0 is
		/// point 1. f=1 is point 2. The two points cannot be antipodal ( i.e. lat1+lat2=0 and
		/// abs(lon1-lon2)=pi) because then the route is undefined.
		/// </summary>
		/// <param name="f">Fraction of the distance for intermediate point (0..1)</param>
		public static void IntermediateGCPoint(float f, Angle lat1, Angle lon1, Angle lat2, Angle lon2, Angle d, out Angle lat, out Angle lon) {
			double sind = Math.Sin(d.Radians);
			double cosLat1 = Math.Cos(lat1.Radians);
			double cosLat2 = Math.Cos(lat2.Radians);
			double A = Math.Sin((1 - f) * d.Radians) / sind;
			double B = Math.Sin(f * d.Radians) / sind;
			double x = A * cosLat1 * Math.Cos(lon1.Radians) + B * cosLat2 * Math.Cos(lon2.Radians);
			double y = A * cosLat1 * Math.Sin(lon1.Radians) + B * cosLat2 * Math.Sin(lon2.Radians);
			double z = A * Math.Sin(lat1.Radians) + B * Math.Sin(lat2.Radians);
			lat = Angle.FromRadians(Math.Atan2(z, Math.Sqrt(x * x + y * y)));
			lon = Angle.FromRadians(Math.Atan2(y, x));
		}

		/// <summary>
		/// Intermediate points on a great circle
		/// In previous sections we have found intermediate points on a great circle given either
		/// the crossing latitude or longitude. Here we find points (lat,lon) a given fraction of the
		/// distance (d) between them. Suppose the starting point is (lat1,lon1) and the final point
		/// (lat2,lon2) and we want the point a fraction f along the great circle route. f=0 is
		/// point 1. f=1 is point 2. The two points cannot be antipodal ( i.e. lat1+lat2=0 and
		/// abs(lon1-lon2)=pi) because then the route is undefined.
		/// </summary>
		/// <param name="f">Fraction of the distance for intermediate point (0..1)</param>
		public Vector3 IntermediateGCPoint(float f, Angle lat1, Angle lon1, Angle lat2, Angle lon2, Angle d) {
			double sind = Math.Sin(d.Radians);
			double cosLat1 = Math.Cos(lat1.Radians);
			double cosLat2 = Math.Cos(lat2.Radians);
			double A = Math.Sin((1 - f)*d.Radians)/sind;
			double B = Math.Sin(f*d.Radians)/sind;
			double x = A*cosLat1*Math.Cos(lon1.Radians) + B*cosLat2*Math.Cos(lon2.Radians);
			double y = A*cosLat1*Math.Sin(lon1.Radians) + B*cosLat2*Math.Sin(lon2.Radians);
			double z = A*Math.Sin(lat1.Radians) + B*Math.Sin(lat2.Radians);
			Angle lat = Angle.FromRadians(Math.Atan2(z, Math.Sqrt(x*x + y*y)));
			Angle lon = Angle.FromRadians(Math.Atan2(y, x));

			Vector3 v = MathEngine.SphericalToCartesian(lat, lon, _equatorialRadius);
			return v;
		}
	}
}