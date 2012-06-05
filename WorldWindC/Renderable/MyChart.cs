using System;
using System.Diagnostics;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Camera;
using WinFont = System.Drawing.Font;
using D3dFont = Microsoft.DirectX.Direct3D.Font;

namespace WorldWind.Renderable {
	/// <summary>
	/// Chart designed for ww.
	/// </summary>
	public class MyChart : RenderableObject {
		private float latitude = 0F;
		private float longitude = 0F;
		private float distanceAboveSurface = 0F;
		private float diameter = 20F;
		private float height = 0.0F;
		private Color chartColor = Color.Blue;
		private string chartType = "8";
		private string caption = string.Empty;

		private WinFont captionFont = new WinFont("ËÎÌו", 36F);

		private Color captionColor = Color.Blue;
		private string clickableUrl = string.Empty;
		private float minViewRange = 0.0F;
		private float maxViewRange = float.MaxValue;

		private float scale = 500.0F;
		private float elevation = 0.0F;
		private World parentWorld = null;
		private Mesh chartMesh;
		private Mesh captionMesh;
		private SizeF captionBounds;
		private Material chartMaterial;
		private Material captionMaterial;
		private bool highLighted = false;
		private bool isMouseOverChart = false;
		private bool isMouseOverCaption = false;
		private bool mouseEntered = false;

		private const float offset = 0.2F;
		private const float itemRadius = 1.0F;
		private const float maxHeight = 20.0F;
		private const float minHeight = 0.0F;
		private const float captionScaleTimesItemScale = 2.0F;
		private const byte opacity = 150;

		#region properties
		/// <summary>
		/// Caption's color
		/// </summary>
		public Color CaptionColor {
			get {
				return this.captionColor;
			}
			set {
				this.captionColor = value;
			}
		}

		/// <summary>
		/// Chart Type, not used now.
		/// </summary>
		public string ChartType {
			get {
				return this.chartType;
			}
			set {
				this.chartType = value;
			}
		}

		/// <summary>
		/// ChartItem latitude.
		/// </summary>
		public float Latitude {
			get {
				return this.latitude;
			}
			set {
				this.latitude = value;
			}
		}

		/// <summary>
		/// ChartItem longitude.
		/// </summary>
		public float Longitude {
			get {
				return this.longitude;
			}
			set {
				this.longitude = value;
			}
		}

		/// <summary>
		/// Chart distance above surface
		/// </summary>
		public float DistanceAboveSurface {
			get {
				return this.distanceAboveSurface;
			}
			set {
				this.distanceAboveSurface = value;
			}
		}

		public float Diameter {
			get {
				return this.diameter;
			}
			set {
				this.diameter = value;
			}
		}

		/// <summary>
		/// ChartItem scale
		/// </summary>
		public float Scale {
			get {
				return this.scale;
			}
			set {
				this.scale = value;
			}
		}

		public string Caption {
			get {
				return this.caption;
			}
			set {
				this.caption = value;
			}
		}

		/// <summary>
		/// ChartItem caption font
		/// </summary>
		public WinFont CaptionFont {
			get {
				return this.captionFont;
			}
			set {
				this.captionFont = value;
			}
		}

		/// <summary>
		/// ChartItem Height, between 0 and 10.
		/// </summary>
		public float Height {
			get {
				return this.height;
			}
			set {
				if (value > maxHeight) {
					this.height = value;
				}
				else if (value < minHeight) {
					this.height = minHeight;
				}
				else {
					this.height = value;
				}
			}
		}

		/// <summary>
		/// ChartItem color, default is blue.
		/// </summary>
		public Color ChartColor {
			get {
				return this.chartColor;
			}
			set {
				this.chartColor = value;
			}
		}

		/// <summary>
		/// chart's parent world.
		/// </summary>
		public World ParentWorld {
			get {
				return parentWorld;
			}
			set {
				parentWorld = value;
			}
		}

		public string ClickableUrl {
			get {
				return this.clickableUrl;
			}
			set {
				this.clickableUrl = value;
			}
		}

		/// <summary>
		/// chart 's min view range
		/// </summary>
		public float MinViewRange {
			get {
				return this.minViewRange;
			}
			set {
				this.minViewRange = value;
			}
		}

		/// <summary>
		/// chart's max view range.
		/// </summary>
		public float MaxViewRange {
			get {
				return this.maxViewRange;
			}
			set {
				this.maxViewRange = value;
			}
		}

		/// <summary>
		/// get or set whether this chart is highlighted.
		/// </summary>
		public bool HighLight {
			get {
				return this.highLighted;
			}
			set {
				this.highLighted = value;
			}
		}

		public bool IsMouseOverChart {
			get {
				return this.isMouseOverChart;
			}
		}

		public bool IsMouseOverCaption {
			get {
				return this.isMouseOverCaption;
			}
		}

		public bool MouseEntered {
			get {
				return this.mouseEntered;
			}
			set {
				this.mouseEntered = value;
			}
		}
		#endregion

		public MyChart(string itemName, float itemLatitude, float itemLongitude, float itemHeightAboveSurface) : base(itemName) {
			this.latitude = itemLatitude;
			this.longitude = itemLongitude;
			this.distanceAboveSurface = itemHeightAboveSurface;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="drawArgs"></param>
		public override void Initialize(DrawArgs drawArgs) {
			if (!this.IsInViewFrustum(drawArgs.WorldCamera)) {
				return;
			}
			this.CreateMesh(drawArgs.Device);
			this.chartMaterial = new Material();
			base.RenderPriority = RenderPriority.Custom;
			if (base.position
			    == Vector3.Empty) {
				base.position = MathEngine.SphericalToCartesian(this.latitude, this.longitude, drawArgs.WorldCamera.WorldRadius + this.elevation + this.distanceAboveSurface);
			}
			base.IsSelectable = true;
			base.Inited = true;
		}

		/// <summary>
		/// update chart data, handled by chart layer.
		/// </summary>
		/// <param name="drawArgs"></param>
		public override void Update(DrawArgs drawArgs) {
			if (!base.Initialized) {
				this.Initialize(drawArgs);
				return;
			}
			if (this.parentWorld != null) {
				float elevate = this.parentWorld.TerrainAccessor.GetElevationAt(this.Latitude, this.Longitude, 100.0F/drawArgs.WorldCamera.ViewRange.Degrees);
				elevate = elevate*World.Settings.VerticalExaggeration;
				this.elevation = elevate;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="drawArgs"></param>
		public override void Render(DrawArgs drawArgs) {
			// if is not in view frustum, dispose and return.
			if (!this.IsInViewFrustum(drawArgs.WorldCamera)) {
				if (base.Initialized) {
					this.Dispose();
				}
				return;
			}
			if (!base.Initialized
			    || !base.IsOn) {
				return;
			}

			Matrix currentWorld = drawArgs.Device.Transform.World;
			Material currentMaterial = drawArgs.Device.Material;
			bool isLighted = drawArgs.Device.RenderState.Lighting;
			bool isSpeculared = drawArgs.Device.RenderState.SpecularEnable;
			bool isAlphaBlended = drawArgs.Device.RenderState.AlphaBlendEnable;
			bool isNormalized = drawArgs.Device.RenderState.NormalizeNormals;

			drawArgs.Device.RenderState.Lighting = true;
			drawArgs.Device.RenderState.Ambient = Color.FromArgb(0x40, 0x40, 0x40);
			drawArgs.Device.RenderState.NormalizeNormals = true;
			drawArgs.Device.RenderState.AlphaBlendEnable = true;

			drawArgs.Device.Lights[0].Diffuse = Color.White;
			drawArgs.Device.Lights[0].Specular = Color.White;
			drawArgs.Device.Lights[0].Type = LightType.Directional;
			drawArgs.Device.Lights[0].Direction = new Vector3(1F, 0F, 0F);
			drawArgs.Device.Lights[0].Enabled = true;
			drawArgs.Device.RenderState.SpecularEnable = true;

			// draw mesh;
			Matrix barMatrix = Matrix.RotationZ((float) Math.PI/2.0F);
			barMatrix *= Matrix.Translation(0F, 0F, this.height/2F);
			barMatrix *= Matrix.Scaling(this.Scale, this.Scale, this.Scale);
			barMatrix *= Matrix.Translation(0F, 0F, (float) (drawArgs.WorldCamera.WorldRadius) + this.DistanceAboveSurface + this.elevation);
			barMatrix *= Matrix.RotationY((float) (Math.PI)*(90F - this.latitude)/180F);
			barMatrix *= Matrix.RotationZ((float) Math.PI*this.longitude/180F);
			barMatrix *= Matrix.Translation(-(float) drawArgs.WorldCamera.ReferenceCenter.X, -(float) drawArgs.WorldCamera.ReferenceCenter.Y, -(float) drawArgs.WorldCamera.ReferenceCenter.Z);

			// draw caption mesh.
			Matrix capMatrix = Matrix.Identity;
			capMatrix *= Matrix.RotationX((float) drawArgs.WorldCamera.Tilt.Radians);
			capMatrix *= Matrix.Translation(0F - this.captionBounds.Width/2F, 0F, this.captionBounds.Height/2F + this.height/2F + offset);
			capMatrix *= Matrix.RotationYawPitchRoll(0F, 0F, 360F - (float) drawArgs.WorldCamera.Heading.Radians);
			capMatrix *= Matrix.Scaling(this.scale*captionScaleTimesItemScale, this.scale*captionScaleTimesItemScale, this.scale*captionScaleTimesItemScale);
			capMatrix *= Matrix.Translation(0.0F, 0.0F, ((float) drawArgs.WorldCamera.WorldRadius) + this.distanceAboveSurface + this.elevation);
			capMatrix *= Matrix.RotationY((float) Math.PI*(90F - this.Latitude)/180F);
			capMatrix *= Matrix.RotationZ(((float) Math.PI)*this.Longitude/180F);
			capMatrix *= Matrix.Translation(-(float) drawArgs.WorldCamera.ReferenceCenter.X, -(float) drawArgs.WorldCamera.ReferenceCenter.Y, -(float) drawArgs.WorldCamera.ReferenceCenter.Z);
			drawArgs.Device.Transform.World = barMatrix;
			// 
			this.isMouseOverChart = this.IsMouseOver(this.chartMesh, drawArgs.Device);
			if (this.isMouseOverCaption
			    || this.isMouseOverChart) {
				this.SetupChartMaterials(true);
				DrawArgs.MouseCursor = CursorType.Hand;
			}
			else if (this.highLighted) {
				this.SetupChartMaterials(true);
			}
			else {
				this.SetupChartMaterials(false);
			}
			// draw chart
			drawArgs.Device.Material = this.chartMaterial;
			this.chartMesh.DrawSubset(0);

			drawArgs.Device.Transform.World = capMatrix;
			// check is mouse over caption
			this.isMouseOverCaption = this.IsMouseOver(this.captionMesh, drawArgs.Device);
			if (this.isMouseOverCaption
			    || this.isMouseOverChart) {
				this.SetupCaptionMaterials(true);
				DrawArgs.MouseCursor = CursorType.Hand;
			}
			else if (this.highLighted) {
				this.SetupCaptionMaterials(true);
			}
			else {
				this.SetupCaptionMaterials(false);
			}
			drawArgs.Device.Material = this.captionMaterial;
			this.captionMesh.DrawSubset(0);
			// disable specular and lighting.
			drawArgs.Device.RenderState.SpecularEnable = isSpeculared;
			drawArgs.Device.RenderState.AlphaBlendEnable = isAlphaBlended;
			drawArgs.Device.RenderState.Lighting = isLighted;
			drawArgs.Device.RenderState.NormalizeNormals = isNormalized;
			drawArgs.Device.Transform.World = currentWorld;
			drawArgs.Device.Material = currentMaterial;
			//         this.OnRendered();
		}

		/// <summary>
		/// Do not dispose .
		/// </summary>
		public override void Dispose() {
			base.Inited = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="drawArgs"></param>
		/// <returns></returns>
		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			if (this.isMouseOverCaption
			    || this.isMouseOverChart) {
				if (this.clickableUrl
				    != string.Empty) {
					Process.Start(this.clickableUrl);
				}
				return true;
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// determine whether this chart item is in visuble.
		/// </summary>
		/// <param name="camera"></param>
		/// <returns></returns>
		private bool IsInViewFrustum(CameraBase camera) {
			if (base.Position
			    == Vector3.Empty) {
				base.position = MathEngine.SphericalToCartesian(this.latitude, this.longitude, camera.WorldRadius);
			}
			return camera.ViewFrustum.ContainsPoint(base.Position);
		}

		private void CreateMesh(Device dev) {
			// create chart bar mesh.
			int slices = Int16.Parse(this.chartType);
			this.chartMesh = Mesh.Cylinder(dev, itemRadius, itemRadius, this.Height, slices, 1);
			this.chartMesh.ComputeNormals();
			GlyphMetricsFloat[] gmfs;
			string text = string.Empty;
			if (this.caption
			    == string.Empty) {
				text = base.Name;
			}
			else {
				text = this.caption;
			}
			this.captionMesh = Mesh.TextFromFont(dev, this.captionFont, text, 0.15F, 0.1F, out gmfs);
			// calculate caption mesh bounds.
			float maxX = 0, maxY = 0, offsetY = 0;
			int gmfsLength = gmfs.Length;
			for (int i = 0; i < gmfsLength; i++) {
				maxX += gmfs[i].CellIncX;
				float y = offsetY + gmfs[i].BlackBoxY;
				if (y > maxY) {
					maxY = y;
				}
				offsetY += gmfs[i].CellIncY;
			}
			this.captionBounds = new SizeF(maxX, maxY);
			this.captionMesh.ComputeNormals();
		}

		/// <summary>
		/// Setup Chart's Materials.
		/// </summary>
		private void SetupChartMaterials(bool isMouseOver) {
			// Setup chart bar material.
			if (!isMouseOver) {
				this.chartMaterial.Diffuse = Color.FromArgb(opacity, this.ChartColor);
				this.chartMaterial.Ambient = Color.FromArgb(opacity, this.ChartColor);
			}
			else {
				this.chartMaterial.Diffuse = this.chartColor;
				this.chartMaterial.Ambient = this.chartColor;
			}
			this.chartMaterial.Specular = Color.LightGray;
			this.chartMaterial.SpecularSharpness = 15F;
		}

		private void SetupCaptionMaterials(bool isMouseOver) {
			// Setup chart caption material.
			this.captionMaterial = new Material();
			if (!isMouseOver) {
				this.captionMaterial.Diffuse = Color.FromArgb(opacity, this.captionColor);
				this.chartMaterial.Ambient = Color.FromArgb(opacity, this.chartColor);
			}
			else {
				this.captionMaterial.Diffuse = this.captionColor;
				this.captionMaterial.Ambient = this.chartColor;
			}
			this.captionMaterial.Specular = Color.LightGray;
			this.captionMaterial.SpecularSharpness = 15F;
		}

		/// <summary>
		/// Check is mouse over a mesh.
		/// </summary>
		/// <param name="mesh">Mesh to check</param>
		/// <param name="dev">device which is ready to render</param>
		private bool IsMouseOver(Mesh mesh, Device dev) {
			Vector3 rayPos = new Vector3(DrawArgs.LastMousePosition.X, DrawArgs.LastMousePosition.Y, dev.Viewport.MinZ);
			Vector3 rayDir = new Vector3(DrawArgs.LastMousePosition.X, DrawArgs.LastMousePosition.Y, dev.Viewport.MaxZ);
			rayPos.Unproject(dev.Viewport, dev.Transform.Projection, dev.Transform.View, dev.Transform.World);
			rayDir.Unproject(dev.Viewport, dev.Transform.Projection, dev.Transform.View, dev.Transform.World);
			return mesh.Intersect(rayPos, rayDir);
		}

		//      private void OnRendered() {
		//         string parentLocalName = base.ParentList.Name.Substring(base.ParentList.Name.IndexOf("_") + 1);
		//         //MyMouseEventArgs e = new MyMouseEventArgs(parentLocalName, base.Name);
		//         if (this.isMouseOverChart || this.isMouseOverCaption) {
		//            if (this.mouseEntered == false) {
		//               base.OnMouseEnter();
		//               this.mouseEntered = true;
		//            }
		//         }
		//         else {
		//            if (this.mouseEntered == true) {
		//               base.OnMouseLeave();
		//               this.mouseEntered = false;
		//            }
		//         }
		//      }
	}
}