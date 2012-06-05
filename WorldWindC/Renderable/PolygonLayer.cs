using System.Drawing;
using System.IO;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace WorldWind.Renderable {
	/// <summary>
	/// Summary description for PolygonLayer.
	/// </summary>
	public class PolygonLayer : RenderableObject {
		#region Private Members
		private World _parentWorld;
		private double _minDisplayAltitude;
		private double _maxDisplayAltitude;
		private string _polygonFilePath;
		private Color _color;
		private CustomVertex.PositionColored[] _vertices;
		private int[] _indices;
		private double _distanceAboveSurface;
		#endregion

		#region Public Methods
		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.PolygonLayer"/> class.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="parentWorld"></param>
		/// <param name="minDisplayAltitude"></param>
		/// <param name="maxDisplayAltitude"></param>
		/// <param name="distanceAboveSurface"></param>
		/// <param name="polygonFilePath"></param>
		/// <param name="color"></param>
		public PolygonLayer(string name, World parentWorld, double minDisplayAltitude, double maxDisplayAltitude, double distanceAboveSurface, string polygonFilePath, Color color) : base(name, parentWorld.Position, Quaternion.RotationYawPitchRoll(0, 0, 0)) {
			this._parentWorld = parentWorld;
			this._distanceAboveSurface = distanceAboveSurface;
			this._minDisplayAltitude = minDisplayAltitude;
			this._maxDisplayAltitude = maxDisplayAltitude;
			this._polygonFilePath = polygonFilePath;
			this._color = color;
		}

		public override void Initialize(DrawArgs drawArgs) {
			FileInfo polygonFileInfo = new FileInfo(this._polygonFilePath);
			if (!polygonFileInfo.Exists) {
				this.Inited = true;
				return;
			}

			using (FileStream polygonFileStream = polygonFileInfo.OpenRead()) {
				using (BinaryReader polygonFileReader = new BinaryReader(polygonFileStream, Encoding.ASCII)) {
					int nodeCount = polygonFileReader.ReadInt32();
					int edgeCount = polygonFileReader.ReadInt32();

					this._vertices = new CustomVertex.PositionColored[nodeCount];
					this._indices = new int[edgeCount*3];

					for (int i = 0; i < nodeCount; i++) {
						double lat = polygonFileReader.ReadDouble();
						double lon = polygonFileReader.ReadDouble();

						Vector3 curNode = MathEngine.SphericalToCartesian((float) lat, (float) lon, (float) (this._parentWorld.EquatorialRadius + this._distanceAboveSurface));
						this._vertices[i].X = curNode.X;
						this._vertices[i].Y = curNode.Y;
						this._vertices[i].Z = curNode.Z;
						this._vertices[i].Color = this._color.ToArgb();
					}

					for (int i = 0; i < edgeCount; i++) {
						int e0 = polygonFileReader.ReadInt32();
						int e1 = polygonFileReader.ReadInt32();
						int e2 = polygonFileReader.ReadInt32();

						this._indices[i*3] = e0;
						this._indices[i*3 + 1] = e1;
						this._indices[i*3 + 2] = e2;
					}
				}
			}
			this.Inited = true;
		}

		public override void Dispose() {}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}

		public override void Update(DrawArgs drawArgs) {
			if (!this.Inited) {
				this.Initialize(drawArgs);
			}
		}

		public override void Render(DrawArgs drawArgs) {
			if (this.Inited) {
				drawArgs.Device.VertexFormat = CustomVertex.PositionColored.Format;
				drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
				drawArgs.Device.DrawIndexedUserPrimitives(PrimitiveType.TriangleList, 0, this._vertices.Length, this._indices.Length/3, this._indices, false, this._vertices);
			}
		}
		#endregion
	}
}