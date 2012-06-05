using System;
using Microsoft.DirectX;
using WorldWind.Camera;

namespace WorldWind.Renderable {
	/// <summary>
	/// Chart Layer.
	/// </summary>
	public class MyCharts : RenderableObjectList {
		private float east = 0.0F;
		private float south = 0.0F;
		private float west = 0.0F;
		private float north = 0.0F;

		public World ParentWorld;

		/// <summary>
		/// layer's window width, useful for calculate chart item's
		/// width.
		/// </summary>
		public static float WindowWidth = 1024.0F;

		/// <summary>
		/// Init a new chart layer instance.
		/// </summary>
		/// <param name="name"></param>
		public MyCharts(string name) : base(name) {}

		public MyCharts(string name, string dataSource, TimeSpan refreshInterval, World parentWorld, Cache cache) : base(name, dataSource, refreshInterval, parentWorld, cache) {}

		#region RenderableObjectList Members
		/// <summary>
		/// if ro is chart, then add it to list.
		/// </summary>
		/// <param name="ro"></param>
		public override void Add(RenderableObject ro) {
			MyChart c = ro as MyChart;
			if (c == null) {
				ro.Dispose();
				return;
			}
			c.ParentWorld = this.ParentWorld;
			base.m_children.Add(ro);
			// the first chart added to this layer
			if (base.Count == 1) {
				this.east = c.Longitude;
				this.west = c.Longitude;
				this.south = c.Latitude;
				this.north = c.Latitude;
			}
			else {
				if (this.east
				    < c.Longitude) {
					this.east = c.Longitude;
				}
				if (this.west
				    > c.Longitude) {
					this.west = c.Longitude;
				}
				if (this.north
				    < c.Latitude) {
					this.north = c.Latitude;
				}
				if (this.south
				    > c.Latitude) {
					this.south = c.Latitude;
				}
			}
			base.Inited = false;
		}

		public override void Initialize(DrawArgs drawArgs) {
			if (!base.IsOn) {
				return;
			}
			foreach (RenderableObject ro in base.m_children) {
				MyChart c = ro as MyChart;
				// if c is not in current view frustum
				if (!this.IsInViewFrustum(drawArgs.WorldCamera, c)) {
					continue;
				}
				// is c is not initialized , initialize c.
				if (!c.Initialized) {
					//double layerLatitudeRange = this.east - this.west;
					//double chartLatitudeRange = layerLatitudeRange*c.Diameter/MyCharts.WindowWidth;
					//Angle chartAngle = World.ApproxAngularDistance(
					//   Angle.FromDegrees(c.Latitude),
					//   Angle.FromDegrees(c.Longitude),
					//   Angle.FromDegrees(c.Latitude + chartLatitudeRange),
					//   Angle.FromDegrees(c.Longitude)
					//   );
					//double chartDiameter = chartAngle.Radians*
					//                       (drawArgs.WorldCamera.WorldRadius
					//                        + c.DistanceAboveSurface);
					//c.Scale = (float) (chartDiameter/2.0);
					c.Initialize(drawArgs);
				}
			}
			base.RenderPriority = RenderPriority.Custom;
			base.Inited = true;
		}

		public override void Render(DrawArgs drawArgs) {
			if (!base.IsOn) {
				return;
			}
			// if not initialized, initialize and return, wait for next
			// render
			if (!base.Initialized) {
				//this.Initialize(drawArgs);
				return;
			}
			foreach (RenderableObject ro in base.m_children) {
				MyChart c = ro as MyChart;
				if (c == null) {
					continue;
				}
				if (c.Scale == 0) {
					base.Inited = false;
					return;
				}
				if (!this.IsInViewFrustum(drawArgs.WorldCamera, c)) {
					continue;
				}
				c.Render(drawArgs);
				this.OnRendered(c);
			}
		}

		public override void Update(DrawArgs drawArgs) {
			if (!base.IsOn) {
				return;
			}
			// donot do update if not initialized
			if (!base.Initialized) {
				this.Initialize(drawArgs);
			}
			foreach (RenderableObject ro in base.m_children) {
				MyChart c = ro as MyChart;
				//if (c == null) {
				//   ro.Dispose();
				//   base.Remove(ro.Name);
				//   continue;
				//}
				if (!this.IsInViewFrustum(drawArgs.WorldCamera, c)) {
					continue;
				}
				c.Update(drawArgs);
			}
		}

		/// <summary>
		/// HighLight a chart in this layer.
		/// </summary>
		/// <param name="itemName">
		/// name of the chart to operate
		/// </param>
		/// <param name="highLight">
		/// indicates whether highlight the chart.
		/// </param>
		public override void Highlight(string itemName, bool highLight) {
			MyChart chart = base.GetObject(itemName) as MyChart;
			if (chart != null) {
				chart.HighLight = highLight;
			}
		}
		#endregion

		private bool IsInViewFrustum(CameraBase camera, MyChart c) {
			if (c.Position
			    == Vector3.Empty) {
				c.Position = MathEngine.SphericalToCartesian(c.Latitude, c.Longitude, camera.WorldRadius);
			}
			return camera.ViewFrustum.ContainsPoint(c.Position);
		}

		protected virtual void OnRendered(MyChart c) {
			string localLayerName = base.Name.Substring(base.Name.IndexOf("_") + 1);
			if (c.IsMouseOverCaption
			    || c.IsMouseOverChart) {
				if (c.MouseEntered == false) {
					this.OnMouseEnterItem(localLayerName, c.Name);
					c.MouseEntered = true;
				}
			}
			else {
				if (c.MouseEntered == true) {
					this.OnMouseLeaveItem(localLayerName, c.Name);
					c.MouseEntered = false;
				}
			}
		}

		protected override void OnMouseEnterItem(string localLayerName, string itemName) {
			base.OnMouseEnterItem(localLayerName, itemName);
		}

		protected override void OnMouseLeaveItem(string localLayerName, string itemName) {
			base.OnMouseLeaveItem(localLayerName, itemName);
		}
	}
}