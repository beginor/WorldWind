using System.Drawing;
using Microsoft.DirectX.Direct3D;
//using WorldWind.Menu;

namespace WorldWind {
	/// <summary>
	/// Summary description for LineGraph.
	/// </summary>
	public class LineGraph {
		private float m_Min = 0f;
		private float m_Max = 50.0f;

		private float[] m_Values = new float[0];

		private Point m_Location = new Point(100, 100);
		private Size m_Size = new Size(300, 100);
		private Color m_BackgroundColor = Color.FromArgb(100, 0, 0, 0);
		private Color m_LineColor = Color.Red;

		private bool m_Visible = false;
		private bool m_ResetVerts = true;

		public bool Visible {
			get {
				return m_Visible;
			}
			set {
				m_Visible = value;
			}
		}

		public float[] Values {
			get {
				return m_Values;
			}
			set {
				m_Values = value;
				m_ResetVerts = true;
			}
		}

		public Color BackgroundColor {
			get {
				return m_BackgroundColor;
			}
			set {
				m_BackgroundColor = value;
			}
		}

		public Color LineColor {
			get {
				return m_LineColor;
			}
			set {
				m_LineColor = value;
				m_ResetVerts = true;
			}
		}

		public Point Location {
			get {
				return m_Location;
			}
			set {
				if (m_Location != value) {
					m_Location = value;
					m_ResetVerts = true;
				}
			}
		}

		public Size Size {
			get {
				return m_Size;
			}
			set {
				if (m_Size != value) {
					m_Size = value;
					m_ResetVerts = true;
				}
			}
		}

		private CustomVertex.TransformedColored[] m_Verts = new CustomVertex.TransformedColored[0];

		public void Render(DrawArgs drawArgs) {
			if (!this.m_Visible) {
				return;
			}

			Widgets.WidgetUtil.DrawBox(m_Location.X, m_Location.Y, m_Size.Width, m_Size.Height, 0.0f, m_BackgroundColor.ToArgb(), drawArgs.Device);

			if (m_Values == null
			    || m_Values.Length == 0) {
				return;
			}

			float xIncr = (float) m_Size.Width/(float) m_Values.Length;

			m_Verts = new CustomVertex.TransformedColored[m_Values.Length];

			if (m_ResetVerts) {
				for (int i = 0; i < m_Values.Length; i++) {
					if (m_Values[i] < m_Min) {
						m_Verts[i].Y = m_Location.Y + m_Size.Height;
					}
					else if (m_Values[i] > m_Max) {
						m_Verts[i].Y = m_Location.Y;
					}
					else {
						float p = (m_Values[i] - m_Min)/(m_Max - m_Min);
						m_Verts[i].Y = m_Location.Y + m_Size.Height - m_Size.Height*p;
					}

					m_Verts[i].X = m_Location.X + i*xIncr;
					m_Verts[i].Z = 0.0f;
					m_Verts[i].Color = m_LineColor.ToArgb();
				}
			}

			drawArgs.Device.TextureState[0].ColorOperation = TextureOperation.Disable;
			drawArgs.Device.VertexFormat = CustomVertex.TransformedColored.Format;

			drawArgs.Device.VertexFormat = CustomVertex.TransformedColored.Format;
			drawArgs.Device.DrawUserPrimitives(PrimitiveType.LineStrip, m_Verts.Length - 1, m_Verts);
		}
	}
}