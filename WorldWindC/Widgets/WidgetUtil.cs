using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace WorldWind.Widgets {
	public sealed class WidgetUtil {
		private WidgetUtil() {}

		public static void DrawLine(Vector2[] linePoints, int color, Device device) {
			CustomVertex.TransformedColored[] lineVerts = new CustomVertex.TransformedColored[linePoints.Length];

			for (int i = 0; i < linePoints.Length; i++) {
				lineVerts[i].X = linePoints[i].X;
				lineVerts[i].Y = linePoints[i].Y;
				lineVerts[i].Z = 0.0f;

				lineVerts[i].Color = color;
			}

			device.TextureState[0].ColorOperation = TextureOperation.Disable;
			device.VertexFormat = CustomVertex.TransformedColored.Format;

			device.DrawUserPrimitives(PrimitiveType.LineStrip, lineVerts.Length - 1, lineVerts);
		}

		public static void DrawBox(int ulx, int uly, int width, int height, float z, int color, Device device) {
			CustomVertex.TransformedColored[] verts = new CustomVertex.TransformedColored[4];
			verts[0].X = (float) ulx;
			verts[0].Y = (float) uly;
			verts[0].Z = z;
			verts[0].Color = color;

			verts[1].X = (float) ulx;
			verts[1].Y = (float) uly + height;
			verts[1].Z = z;
			verts[1].Color = color;

			verts[2].X = (float) ulx + width;
			verts[2].Y = (float) uly;
			verts[2].Z = z;
			verts[2].Color = color;

			verts[3].X = (float) ulx + width;
			verts[3].Y = (float) uly + height;
			verts[3].Z = z;
			verts[3].Color = color;

			device.VertexFormat = CustomVertex.TransformedColored.Format;
			device.TextureState[0].ColorOperation = TextureOperation.Disable;
			device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts.Length - 2, verts);
		}

		public static void DrawSector(double startAngle, double endAngle, int centerX, int centerY, int radius, float z, int color, Device device) {
			int prec = 7;

			CustomVertex.TransformedColored[] verts = new CustomVertex.TransformedColored[prec + 2];
			verts[0].X = centerX;
			verts[0].Y = centerY;
			verts[0].Z = z;
			verts[0].Color = color;
			double angleInc = (double) (endAngle - startAngle)/prec;

			for (int i = 0; i <= prec; i++) {
				verts[i + 1].X = (float) Math.Cos((double) (startAngle + angleInc*i))*radius + centerX;
				verts[i + 1].Y = (float) Math.Sin((double) (startAngle + angleInc*i))*radius*(-1.0f) + centerY;
				verts[i + 1].Z = z;
				verts[i + 1].Color = color;
			}

			device.VertexFormat = CustomVertex.TransformedColored.Format;
			device.TextureState[0].ColorOperation = TextureOperation.Disable;
			device.DrawUserPrimitives(PrimitiveType.TriangleFan, verts.Length - 2, verts);
		}
	}
}