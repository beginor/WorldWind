using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Utilities;

namespace WorldWind.Renderable {
	/// <summary>
	/// Summary description for MeshLayer.
	/// </summary>
	public class MeshLayer : RenderableObject {
		private Mesh mesh;
		private string meshFilePath;
		private ExtendedMaterial[] materials;
		private Material[] meshMaterials;

		private float lat;
		private float lon;
		private float layerRadius;
		private float scaleFactor;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.MeshLayer"/> class.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="latitude"></param>
		/// <param name="longitude"></param>
		/// <param name="layerRadius"></param>
		/// <param name="scaleFactor"></param>
		/// <param name="meshFilePath"></param>
		/// <param name="orientation"></param>
		public MeshLayer(string name, float latitude, float longitude, float layerRadius, float scaleFactor, string meshFilePath, Quaternion orientation) : base(name, MathEngine.SphericalToCartesian(latitude, longitude, layerRadius), orientation) {
			this.meshFilePath = Path.Combine(Application.StartupPath, meshFilePath);
			this.lat = latitude;
			this.lon = longitude;
			this.layerRadius = layerRadius;
			this.scaleFactor = scaleFactor;
		}

		public override void Initialize(DrawArgs drawArgs) {
			try {
				GraphicsStream adj;
				this.mesh = Mesh.FromFile(this.meshFilePath, MeshFlags.Managed, drawArgs.Device, out adj, out this.materials);
				this.meshMaterials = new Material[this.materials.Length];
				//using(StreamWriter sw = new StreamWriter("mat.txt", true, System.Text.Encoding.ASCII))
				{
					//sw.WriteLine(this.meshMaterials.Length.ToString());
					for (int i = 0; i < this.materials.Length; i++) {
						this.meshMaterials[i] = this.materials[i].Material3D;
						this.meshMaterials[i].Ambient = this.meshMaterials[i].Diffuse;
					}
				}

				//this.mesh.ComputeNormals();
			}
			catch (Exception caught) {
				Log.Write(caught);
			}
			this.Inited = true;
		}

		public override void Dispose() {
			// Don't dispose the mesh!
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			return false;
		}

		public override void Update(DrawArgs drawArgs) {
			if (!this.Inited) {
				this.Initialize(drawArgs);
			}
		}

		public override void Render(DrawArgs drawArgs) {
//			Vector3 here = MathEngine.SphericalToCartesian(drawArgs.WorldCamera.Latitude, drawArgs.WorldCamera.Longitude, this.layerRadius);
			Matrix currentWorld = drawArgs.Device.Transform.World;
			drawArgs.Device.RenderState.Lighting = true;
			drawArgs.Device.RenderState.ZBufferEnable = true;
			drawArgs.Device.Lights[0].Diffuse = Color.White;
			drawArgs.Device.Lights[0].Type = LightType.Point;
			drawArgs.Device.Lights[0].Range = 100000;
			drawArgs.Device.Lights[0].Position = new Vector3(this.layerRadius, 0, 0);
			drawArgs.Device.Lights[0].Enabled = true;

			drawArgs.Device.RenderState.CullMode = Cull.None;
			drawArgs.Device.Transform.World = Matrix.Identity;
			drawArgs.Device.Transform.World *= Matrix.Scaling(this.scaleFactor, this.scaleFactor, this.scaleFactor);
			//drawArgs.device.Transform.World *= Matrix.RotationX(MathEngine.RadiansToDegrees(90));
			drawArgs.Device.Transform.World *= Matrix.Translation(0, 0, -this.layerRadius);

			drawArgs.Device.Transform.World *= Matrix.RotationY((float) MathEngine.DegreesToRadians(90 - this.lat));
			drawArgs.Device.Transform.World *= Matrix.RotationZ((float) MathEngine.DegreesToRadians(180 + this.lon));

			//drawArgs.device.Transform.World *= Matrix.RotationQuaternion(drawArgs.WorldCamera.CurrentOrientation);

			drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
			drawArgs.Device.RenderState.NormalizeNormals = true;

			for (int i = 0; i < this.meshMaterials.Length; i++) {
				drawArgs.Device.Material = this.meshMaterials[i];
				this.mesh.DrawSubset(i);
			}

			drawArgs.Device.Transform.World = currentWorld;
			drawArgs.Device.RenderState.CullMode = Cull.Clockwise;
			drawArgs.Device.RenderState.Lighting = false;
		}
	}
}