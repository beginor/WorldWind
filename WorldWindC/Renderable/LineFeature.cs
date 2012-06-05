using System;
using System.Drawing;
using System.IO;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Configuration;
using WorldWind.Net;
using WorldWind.Renderable;
using WorldWind.Utilities;

namespace WorldWind.Renderable {
	/// <summary>
	/// Summary description for LineFeature.
	/// </summary>
	public class LineFeature : RenderableObject {
		#region Private Members
		private double m_distanceAboveSurface = 0;
		private double m_extrudeHeight = 0;
		private Point3d[] m_points = null;
		private CustomVertex.PositionColored[] m_vertices = null;
		private CustomVertex.PositionColored[] m_outlineTopVertices = null;
		private CustomVertex.PositionColored[] m_outlineBottomVertices = null;
		private CustomVertex.PositionColored[] m_outlineSideVertices = null;
		private CustomVertex.PositionColoredTextured[] m_texturedVertices = null;
		private Color m_color = Color.Black;
		private float m_verticalExaggeration = World.Settings.VerticalExaggeration;
		private double m_minimumDisplayAltitude = 0;
		private double m_maximumDisplayAltitude = double.MaxValue;
		private string m_imageUri = null;
		private Texture m_texture = null;
		private Color m_outlineColor = Color.Black;
		private bool m_outline = false;
		private bool m_extrudeUpwards = true;
		#endregion

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

		public Point3d[] Points {
			get {
				return m_points;
			}
			set {
				m_points = value;
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

		public LineFeature(string name, World parentWorld, Point3d[] points, Color color) : base(name, parentWorld) {
			m_points = points;
			m_color = color;

			RenderPriority = RenderPriority.LinePaths;
		}

		public LineFeature(string name, World parentWorld, Point3d[] points, string imageUri) : base(name, parentWorld) {
			m_points = points;
			m_imageUri = imageUri;

			RenderPriority = RenderPriority.LinePaths;
		}

		public override void Dispose() {}

		public override void Initialize(DrawArgs drawArgs) {
			if (m_points == null) {
				Inited = true;
				return;
			}

			if (m_imageUri != null) {
				//load image
				if (m_imageUri.ToLower().StartsWith("http://")) {
					string savePath = string.Format("{0}\\image", ConfigurationLoader.GetRenderablePathString(this));
					FileInfo file = new FileInfo(savePath);
					if (!file.Exists) {
						WebDownload download = new WebDownload(m_imageUri);

						if (!file.Directory.Exists) {
							file.Directory.Create();
						}

						download.DownloadFile(file.FullName, DownloadType.Unspecified);
					}

					m_texture = ImageHelper.LoadTexture(file.FullName);
				}
				else {
					m_texture = ImageHelper.LoadTexture(m_imageUri);
				}
			}

			UpdateVertices();

			Inited = true;
		}

		private void UpdateVertices() {
			try {
				m_verticalExaggeration = World.Settings.VerticalExaggeration;

				if (m_imageUri != null) {
					UpdateTexturedVertices();
				}
				else {
					UpdateColoredVertices();
				}

				if (m_outline) {
					UpdateOutlineVertices();
				}
			}
			catch (Exception ex) {
				Log.Write(ex);
			}
		}

		private void UpdateOutlineVertices() {
			Color outlineColor = Color.FromArgb(Opacity, m_outlineColor.R, m_outlineColor.G, m_outlineColor.B);

			m_outlineTopVertices = new CustomVertex.PositionColored[m_points.Length];
			m_outlineBottomVertices = new CustomVertex.PositionColored[m_points.Length];
			if (m_extrudeHeight > 0) {
				m_outlineSideVertices = new CustomVertex.PositionColored[m_points.Length*2];
			}

			for (int i = 0; i < m_points.Length; i++) {
				Vector3 xyzVertex = MathEngine.SphericalToCartesian(m_points[i].Y, m_points[i].X, m_verticalExaggeration*(m_points[i].Z + m_distanceAboveSurface) + World.EquatorialRadius);

				m_outlineTopVertices[i].X = xyzVertex.X;
				m_outlineTopVertices[i].Y = xyzVertex.Y;
				m_outlineTopVertices[i].Z = xyzVertex.Z;
				m_outlineTopVertices[i].Color = outlineColor.ToArgb();

				if (m_extrudeHeight > 0) {
					m_outlineSideVertices[2*i] = m_outlineTopVertices[i];

					xyzVertex = MathEngine.SphericalToCartesian(m_points[i].Y, m_points[i].X, m_verticalExaggeration*(m_points[i].Z + m_distanceAboveSurface + (ExtrudeUpwards ? m_extrudeHeight : -1.0*m_extrudeHeight)) + World.EquatorialRadius);

					m_outlineBottomVertices[i].X = xyzVertex.X;
					m_outlineBottomVertices[i].Y = xyzVertex.Y;
					m_outlineBottomVertices[i].Z = xyzVertex.Z;
					m_outlineBottomVertices[i].Color = outlineColor.ToArgb();

					m_outlineSideVertices[2*i + 1] = m_outlineBottomVertices[i];
				}
			}
		}

		private void UpdateColoredVertices() {
			m_vertices = new CustomVertex.PositionColored[(m_extrudeHeight > 0 ? m_points.Length*2 : m_points.Length)];

			Color vertexColor = Color.FromArgb(Opacity, m_color.R, m_color.G, m_color.B);

			for (int i = 0; i < m_points.Length; i++) {
				Vector3 xyzVertex = MathEngine.SphericalToCartesian(m_points[i].Y, m_points[i].X, m_verticalExaggeration*(m_points[i].Z + m_distanceAboveSurface) + World.EquatorialRadius);

				if (m_extrudeHeight > 0) {
					m_vertices[2*i].X = xyzVertex.X;
					m_vertices[2*i].Y = xyzVertex.Y;
					m_vertices[2*i].Z = xyzVertex.Z;
					m_vertices[2*i].Color = vertexColor.ToArgb();

					xyzVertex = MathEngine.SphericalToCartesian(m_points[i].Y, m_points[i].X, m_verticalExaggeration*(m_points[i].Z + m_distanceAboveSurface + (ExtrudeUpwards ? m_extrudeHeight : -1.0*m_extrudeHeight)) + World.EquatorialRadius);

					m_vertices[2*i + 1].X = xyzVertex.X;
					m_vertices[2*i + 1].Y = xyzVertex.Y;
					m_vertices[2*i + 1].Z = xyzVertex.Z;
					m_vertices[2*i + 1].Color = vertexColor.ToArgb();
				}
				else {
					m_vertices[i].X = xyzVertex.X;
					m_vertices[i].Y = xyzVertex.Y;
					m_vertices[i].Z = xyzVertex.Z;
					m_vertices[i].Color = vertexColor.ToArgb();
				}
			}
		}

		private void UpdateTexturedVertices() {
			m_texturedVertices = new CustomVertex.PositionColoredTextured[m_points.Length*2];

			float textureCoordIncrement = 1.0f/(float) (m_points.Length - 1);
			m_verticalExaggeration = World.Settings.VerticalExaggeration;
			Color vertexColor = Color.FromArgb(Opacity, m_color.R, m_color.G, m_color.B);

			for (int i = 0; i < m_points.Length; i++) {
				Vector3 xyzVertex = MathEngine.SphericalToCartesian(m_points[i].Y, m_points[i].X, m_verticalExaggeration*(m_points[i].Z + m_distanceAboveSurface) + World.EquatorialRadius);

				m_texturedVertices[2*i].X = xyzVertex.X;
				m_texturedVertices[2*i].Y = xyzVertex.Y;
				m_texturedVertices[2*i].Z = xyzVertex.Z;
				m_texturedVertices[2*i].Color = vertexColor.ToArgb();
				m_texturedVertices[2*i].Tu = i*textureCoordIncrement;
				m_texturedVertices[2*i].Tv = 1.0f;

				xyzVertex = MathEngine.SphericalToCartesian(m_points[i].Y, m_points[i].X, m_verticalExaggeration*(m_points[i].Z + m_distanceAboveSurface + (ExtrudeUpwards ? m_extrudeHeight : -1.0*m_extrudeHeight)) + World.EquatorialRadius);

				m_texturedVertices[2*i + 1].X = xyzVertex.X;
				m_texturedVertices[2*i + 1].Y = xyzVertex.Y;
				m_texturedVertices[2*i + 1].Z = xyzVertex.Z;
				m_texturedVertices[2*i + 1].Color = vertexColor.ToArgb();
				m_texturedVertices[2*i + 1].Tu = i*textureCoordIncrement;
				m_texturedVertices[2*i + 1].Tv = 0.0f;
			}
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}

		public override void Update(DrawArgs drawArgs) {
			if (drawArgs.WorldCamera.Altitude >= m_minimumDisplayAltitude
			    && drawArgs.WorldCamera.Altitude <= m_maximumDisplayAltitude) {
				if (!Inited) {
					Initialize(drawArgs);
				}

				if (m_verticalExaggeration != World.Settings.VerticalExaggeration) {
					UpdateVertices();
				}
			}
		}

		public override void Render(DrawArgs drawArgs) {
			if (!Inited || drawArgs.WorldCamera.Altitude < m_minimumDisplayAltitude
			    || drawArgs.WorldCamera.Altitude > m_maximumDisplayAltitude) {
				return;
			}

			try {
				Cull currentCull = drawArgs.Device.RenderState.CullMode;
				drawArgs.Device.RenderState.CullMode = Cull.None;

				drawArgs.Device.Transform.World = Matrix.Translation((float) -drawArgs.WorldCamera.ReferenceCenter.X, (float) -drawArgs.WorldCamera.ReferenceCenter.Y, (float) -drawArgs.WorldCamera.ReferenceCenter.Z);
				if (m_extrudeHeight > 0) {
					drawArgs.Device.RenderState.ZBufferEnable = true;
					if (m_vertices != null) {
						drawArgs.Device.VertexFormat = CustomVertex.PositionColored.Format;
						drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
						drawArgs.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, m_vertices.Length - 2, m_vertices);
					}
					if (m_texturedVertices != null && m_texture != null
					    && !m_texture.Disposed) {
						drawArgs.Device.VertexFormat = CustomVertex.PositionColoredTextured.Format;
						drawArgs.Device.SetTexture(0, m_texture);
						drawArgs.Device.TextureState[0].AlphaOperation = TextureOperation.Modulate;
						drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Add;
						drawArgs.Device.TextureState[0].AlphaArgument1 = TextureArgument.TextureColor;
						drawArgs.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, m_texturedVertices.Length - 2, m_texturedVertices);
					}
					//	drawArgs.device.RenderState.ZBufferEnable = true;
				}
				else {
					//	drawArgs.device.RenderState.ZBufferEnable = false;
					drawArgs.Device.VertexFormat = CustomVertex.PositionColored.Format;
					drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
					drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, m_vertices.Length - 1, m_vertices);
					//	drawArgs.device.RenderState.ZBufferEnable = true;
				}

				if (m_outline) {
					drawArgs.Device.VertexFormat = CustomVertex.PositionColored.Format;
					drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
					if (m_outlineTopVertices != null) {
						drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, m_outlineTopVertices.Length - 1, m_outlineTopVertices);
					}

					if (m_extrudeHeight > 0) {
						if (m_outlineBottomVertices != null) {
							drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, m_outlineBottomVertices.Length - 1, m_outlineBottomVertices);
						}

						if (m_outlineSideVertices != null) {
							drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineList, m_outlineSideVertices.Length/2, m_outlineSideVertices);
						}
					}
				}

				drawArgs.Device.Transform.World = drawArgs.WorldCamera.WorldMatrix;
				drawArgs.Device.RenderState.CullMode = currentCull;
			}
			catch (Exception ex) {
				Log.Write(ex);
			}
		}
	}
}