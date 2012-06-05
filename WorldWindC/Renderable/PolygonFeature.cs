using System;
using System.Collections;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.PolygonTriangulation;
using WorldWind.Renderable;
using WorldWind.Utilities;

namespace WorldWind.Renderable {
	/// <summary>
	/// Summary description for PolygonFeature.
	/// </summary>
	public class PolygonFeature : RenderableObject {
		private Point3d[] m_polygonPoints = null;
		private Color m_polygonColor = Color.Yellow;
		private CustomVertex.PositionNormalColored[] m_vertices = null;
		private double m_distanceAboveSurface = 0;
		private float m_verticalExaggeration = World.Settings.VerticalExaggeration;
		private double m_minimumDisplayAltitude = 0;
		private double m_maximumDisplayAltitude = double.MaxValue;
		private double m_extrudeHeight = 0;
		private Color m_outlineColor = Color.Black;
		private bool m_outline = false;
		private LineFeature m_lineFeature = null;
		private bool m_extrudeUpwards = true;

		public BoundingBox BoundingBox = null;

		public Color OutlineColor {
			get {
				return m_outlineColor;
			}
			set {
				m_outlineColor = value;
				if (m_vertices != null) {
					UpdateVertices();
				}
			}
		}

		public bool Outline {
			get {
				return m_outline;
			}
			set {
				m_outline = value;
				if (m_vertices != null) {
					UpdateVertices();
				}
			}
		}

		public bool ExtrudeUpwards {
			get {
				return m_extrudeUpwards;
			}
			set {
				m_extrudeUpwards = value;
				if (m_vertices != null) {
					UpdateVertices();
				}
			}
		}

		public double DistanceAboveSurface {
			get {
				return m_distanceAboveSurface;
			}
			set {
				m_distanceAboveSurface = value;
				if (m_vertices != null) {
					UpdateVertices();
				}
			}
		}

		public double MinimumDisplayAltitude {
			get {
				return m_minimumDisplayAltitude;
			}
			set {
				m_minimumDisplayAltitude = value;
			}
		}

		public double MaximumDisplayAltitude {
			get {
				return m_maximumDisplayAltitude;
			}
			set {
				m_maximumDisplayAltitude = value;
			}
		}

		public double ExtrudeHeight {
			get {
				return m_extrudeHeight;
			}
			set {
				m_extrudeHeight = value;
				if (m_vertices != null) {
					UpdateVertices();
				}
			}
		}

		public override byte Opacity {
			get {
				return base.Opacity;
			}
			set {
				base.Opacity = value;
				if (m_vertices != null) {
					UpdateVertices();
				}
			}
		}

		public PolygonFeature(string name, World parentWorld, Point3d[] polygonPoints, Color polygonColor) : base(name, parentWorld) {
			m_polygonPoints = polygonPoints;
			m_polygonColor = polygonColor;

			double minY = double.MaxValue;
			double maxY = double.MinValue;
			double minX = double.MaxValue;
			double maxX = double.MinValue;
			double minZ = double.MaxValue;
			double maxZ = double.MinValue;

			for (int i = 0; i < polygonPoints.Length; i++) {
				if (polygonPoints[i].X < minX) {
					minX = polygonPoints[i].X;
				}
				if (polygonPoints[i].X > maxX) {
					maxX = polygonPoints[i].X;
				}

				if (polygonPoints[i].Y < minY) {
					minY = polygonPoints[i].Y;
				}
				if (polygonPoints[i].Y > maxY) {
					maxY = polygonPoints[i].Y;
				}

				if (polygonPoints[i].Z < minZ) {
					minZ = polygonPoints[i].Z;
				}
				if (polygonPoints[i].Z > maxZ) {
					maxZ = polygonPoints[i].Z;
				}
			}

			minZ += parentWorld.EquatorialRadius;
			maxZ += parentWorld.EquatorialRadius;

			BoundingBox = new BoundingBox((float) minY, (float) maxY, (float) minX, (float) maxX, (float) minZ, (float) maxZ);
		}

		public override void Initialize(DrawArgs drawArgs) {
			UpdateVertices();
			Inited = true;
		}

		private void UpdateVertices() {
			m_verticalExaggeration = World.Settings.VerticalExaggeration;

			CPoint2D[] polygonVertices = new CPoint2D[m_polygonPoints.Length];
			for (int i = 0; i < polygonVertices.Length; i++) {
				polygonVertices[i] = new CPoint2D(m_polygonPoints[i].X, m_polygonPoints[i].Y);
			}

			CPolygonShape cutPolygon = new CPolygonShape(polygonVertices);
			cutPolygon.CutEar();

			ArrayList vertexList = new ArrayList();

			for (int i = 0; i < cutPolygon.NumberOfPolygons; i++) {
				int nPoints = cutPolygon.Polygons(i).Length;

				for (int j = 0; j < nPoints; j++) {
					Point3d point = new Point3d(cutPolygon.Polygons(i)[j].X, cutPolygon.Polygons(i)[j].Y, 0);

					foreach (Point3d sourcePoint in m_polygonPoints) {
						if (sourcePoint.X.Equals(point.X)
						    && sourcePoint.Y.Equals(point.Y)) {
							point.Z = sourcePoint.Z;
							break;
						}
					}

					vertexList.Add(point);
				}
			}

			if (m_extrudeHeight > 0) {
				CustomVertex.PositionNormalColored[] vertices = new CustomVertex.PositionNormalColored[vertexList.Count*2];
				int polygonColor = Color.FromArgb(Opacity, m_polygonColor.R, m_polygonColor.G, m_polygonColor.B).ToArgb();
				int vertexOffset = vertices.Length/2;

				// build bottom vertices
				for (int i = 0; i < vertices.Length/2; i++) {
					Point3d sphericalPoint = (Point3d) vertexList[i];
					Vector3 xyzVector = MathEngine.SphericalToCartesian(sphericalPoint.Y, sphericalPoint.X, World.EquatorialRadius + m_verticalExaggeration*(sphericalPoint.Z + m_distanceAboveSurface));

					vertices[i].Color = polygonColor;
					vertices[i].X = xyzVector.X;
					vertices[i].Y = xyzVector.Y;
					vertices[i].Z = xyzVector.Z;
				}

				//build top vertices
				for (int i = vertexOffset; i < vertices.Length; i++) {
					Point3d sphericalPoint = (Point3d) vertexList[i - vertexOffset];
					Vector3 xyzVector = MathEngine.SphericalToCartesian(sphericalPoint.Y, sphericalPoint.X, World.EquatorialRadius + m_verticalExaggeration*(sphericalPoint.Z + m_distanceAboveSurface + m_extrudeHeight));

					vertices[i].Color = polygonColor;
					vertices[i].X = xyzVector.X;
					vertices[i].Y = xyzVector.Y;
					vertices[i].Z = xyzVector.Z;
				}

				m_vertices = vertices;
			}
			else {
				CustomVertex.PositionNormalColored[] vertices = new CustomVertex.PositionNormalColored[vertexList.Count];
				int polygonColor = Color.FromArgb(Opacity, m_polygonColor.R, m_polygonColor.G, m_polygonColor.B).ToArgb();

				for (int i = 0; i < vertices.Length; i++) {
					Point3d sphericalPoint = (Point3d) vertexList[i];
					Vector3 xyzVector = MathEngine.SphericalToCartesian(sphericalPoint.Y, sphericalPoint.X, World.EquatorialRadius + m_verticalExaggeration*(sphericalPoint.Z + m_distanceAboveSurface));

					vertices[i].Color = polygonColor;
					vertices[i].X = xyzVector.X;
					vertices[i].Y = xyzVector.Y;
					vertices[i].Z = xyzVector.Z;
				}

				m_vertices = vertices;
			}

			if (m_extrudeHeight > 0 || m_outline) {
				Point3d[] linePoints = new Point3d[m_polygonPoints.Length + 1];
				for (int i = 0; i < m_polygonPoints.Length; i++) {
					linePoints[i] = m_polygonPoints[i];
				}

				linePoints[linePoints.Length - 1] = m_polygonPoints[0];

				m_lineFeature = new LineFeature(Name, World, linePoints, m_polygonColor);
				m_lineFeature.ExtrudeHeight = m_extrudeHeight;
				m_lineFeature.DistanceAboveSurface = m_distanceAboveSurface;
				m_lineFeature.ExtrudeUpwards = m_extrudeUpwards;
				m_lineFeature.MinimumDisplayAltitude = m_minimumDisplayAltitude;
				m_lineFeature.MaximumDisplayAltitude = m_maximumDisplayAltitude;
				m_lineFeature.Opacity = Opacity;
				m_lineFeature.Outline = m_outline;
				m_lineFeature.OutlineColor = m_outlineColor;
			}
			else {
				if (m_lineFeature != null) {
					m_lineFeature.Dispose();
					m_lineFeature = null;
				}
			}
		}

		public override void Dispose() {}

		public override void Update(DrawArgs drawArgs) {
			try {
				if (drawArgs.WorldCamera.Altitude >= m_minimumDisplayAltitude
				    && drawArgs.WorldCamera.Altitude <= m_maximumDisplayAltitude) {
					if (!Inited) {
						Initialize(drawArgs);
					}

					if (m_verticalExaggeration != World.Settings.VerticalExaggeration) {
						UpdateVertices();
						m_lineFeature.Initialize(drawArgs);
					}

					if (m_lineFeature != null) {
						m_lineFeature.Update(drawArgs);
					}
				}
			}
			catch (Exception ex) {
				Log.Write(ex);
			}
		}

		public override void Render(DrawArgs drawArgs) {
			if (!Inited || m_vertices == null || drawArgs.WorldCamera.Altitude < m_minimumDisplayAltitude
			    || drawArgs.WorldCamera.Altitude > m_maximumDisplayAltitude) {
				return;
			}

			if (!drawArgs.WorldCamera.ViewFrustum.Intersects(BoundingBox)) {
				return;
			}

			try {
				if (m_lineFeature != null) {
					m_lineFeature.Render(drawArgs);
				}

				Cull currentCull = drawArgs.Device.RenderState.CullMode;
				drawArgs.Device.RenderState.CullMode = Cull.CounterClockwise;
				//	drawArgs.device.RenderState.ZBufferEnable = false;
				drawArgs.Device.Transform.World = Matrix.Translation((float) -drawArgs.WorldCamera.ReferenceCenter.X, (float) -drawArgs.WorldCamera.ReferenceCenter.Y, (float) -drawArgs.WorldCamera.ReferenceCenter.Z);
				if (m_vertices != null) {
					drawArgs.Device.VertexFormat = CustomVertex.PositionNormalColored.Format;
					drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
					drawArgs.Device.DrawUserPrimitives(PrimitiveType.TriangleList, m_vertices.Length/3, m_vertices);
				}
				//	drawArgs.device.RenderState.ZBufferEnable = true;
				drawArgs.Device.Transform.World = drawArgs.WorldCamera.WorldMatrix;
				drawArgs.Device.RenderState.CullMode = currentCull;
			}
			catch (Exception ex) {
				Log.Write(ex);
			}
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}
	}
}