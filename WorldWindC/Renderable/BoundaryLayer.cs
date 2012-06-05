using System.IO;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace WorldWind.Renderable {
	public class BoundaryLayer : RenderableObject {
		#region Private Members
		private World _parentWorld;
		private double _distanceAboveSurface;
		private string _boundaryFilePath;
		private int _color;
		private CustomVertex.PositionColored[] vertices;
		#endregion

		#region Public Methods
		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.BoundaryLayer"/> class.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="parentWorld"></param>
		/// <param name="distanceAboveSurface"></param>
		/// <param name="boundaryFilePath"></param>
		/// <param name="color"></param>
		public BoundaryLayer(string name, World parentWorld, double distanceAboveSurface, string boundaryFilePath, int color) : base(name, parentWorld.Position, Quaternion.RotationYawPitchRoll(0, 0, 0)) {
			this._parentWorld = parentWorld;
			this._distanceAboveSurface = distanceAboveSurface;
			this._boundaryFilePath = boundaryFilePath;
			this._color = color;
		}

		public override void Initialize(DrawArgs drawArgs) {
			FileInfo boundaryFileInfo = new FileInfo(this._boundaryFilePath);
			if (!boundaryFileInfo.Exists) {
				this.Inited = true;
				return;
			}

			using (FileStream boundaryFileStream = boundaryFileInfo.OpenRead()) {
				using (BinaryReader boundaryFileReader = new BinaryReader(boundaryFileStream, Encoding.ASCII)) {
					int count = boundaryFileReader.ReadInt32();
					this.vertices = new CustomVertex.PositionColored[count];

					for (int i = 0; i < count; i++) {
						double lat = boundaryFileReader.ReadDouble();
						double lon = boundaryFileReader.ReadDouble();
						Vector3 v = MathEngine.SphericalToCartesian((float) lat, (float) lon, (float) (this._parentWorld.EquatorialRadius + this._distanceAboveSurface));
						this.vertices[i].X = v.X;
						this.vertices[i].Y = v.Y;
						this.vertices[i].Z = v.Z;
						this.vertices[i].Color = this._color;
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
				drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, this.vertices.Length - 1, this.vertices);
			}
		}
		#endregion
	}
}