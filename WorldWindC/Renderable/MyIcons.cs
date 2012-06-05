using System;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using D3Font = Microsoft.DirectX.Direct3D.Font;

namespace WorldWind.Renderable {
	/// <summary>
	/// Summary description for MyIcons.
	/// </summary>
	public class MyIcons : Icons {
		private static int hotColor = Color.Yellow.ToArgb();
		private static int normalColor = Color.White.ToArgb();

		private static int descriptionColor = Color.White.ToArgb();

		public MyIcons(string name) : base(name) {}

		public MyIcons(string name, string dataSource, TimeSpan refreshInterval, World parentWorld, Cache cache) : base(name, dataSource, refreshInterval, parentWorld, cache) {}

		public override void Render(DrawArgs drawArgs) {
			if (!base.IsOn) {
				return;
			}
			if (!base.Initialized) {
				return;
			}

			int closestIconDistanceSquared = int.MaxValue;
			Icon closestIcon = null;
			Icon icon = null;

			base.m_sprite.Begin(SpriteFlags.AlphaBlend);
			foreach (RenderableObject ro in base.m_children) {
				if (!ro.IsOn) {
					continue;
				}
				if (!ro.Initialized) {
					continue;
				}
				icon = ro as Icon;
				if (icon == null) {
					ro.Dispose();
					continue;
				}

				icon.Render(drawArgs);

				// now ro is an icon and is ready to be rendered.
				// first find closest mouse-over icon

				Vector3 translationVector = new Vector3((float) (icon.PositionD.X - drawArgs.WorldCamera.ReferenceCenter.X), (float) (icon.PositionD.Y - drawArgs.WorldCamera.ReferenceCenter.Y), (float) (icon.PositionD.Z - drawArgs.WorldCamera.ReferenceCenter.Z));

				Vector3 projPoint = drawArgs.WorldCamera.Project(translationVector);
				int dx = DrawArgs.LastMousePosition.X - (int) projPoint.X;
				int dy = DrawArgs.LastMousePosition.Y - (int) projPoint.Y;
				if (icon.SelectionRectangle.Contains(dx, dy)) {
					int distanceSquared = dx*dx + dy*dy;
					if (distanceSquared < closestIconDistanceSquared) {
						closestIconDistanceSquared = distanceSquared;
						closestIcon = icon;
					}
				}

				// if icon is not mouse over icon, render it normally
				if (icon != base.mouseOverIcon) {
					this.Render(drawArgs, icon, projPoint);
				}
			}

			// Render the mouse over icon last (on top)
			if (base.mouseOverIcon != null) {
				Vector3 translationVector = new Vector3((float) (base.mouseOverIcon.PositionD.X - drawArgs.WorldCamera.ReferenceCenter.X), (float) (base.mouseOverIcon.PositionD.Y - drawArgs.WorldCamera.ReferenceCenter.Y), (float) (base.mouseOverIcon.PositionD.Z - drawArgs.WorldCamera.ReferenceCenter.Z));
				this.Render(drawArgs, base.mouseOverIcon, drawArgs.WorldCamera.Project(translationVector));
			}
			if (drawArgs.ParentControl.Bounds.Contains(DrawArgs.LastMousePosition)) {
				string localName = this.name; //Substring(this.name.IndexOf("_") + 1);
				if (closestIcon != null) {
					if (closestIcon != base.mouseOverIcon) {
						base.OnMouseEnterItem(localName, closestIcon.Name);
					}
				}
				if (base.mouseOverIcon != null) {
					if (closestIcon != base.mouseOverIcon) {
						base.OnMouseLeaveItem(localName, this.mouseOverIcon.Name);
					}
				}
				mouseOverIcon = closestIcon;
			}
			base.m_sprite.End();
		}

		protected override void Render(DrawArgs drawArgs, Icon icon, Vector3 projectedPoint) {
			if (!icon.Initialized) {
				icon.Initialize(drawArgs);
				return;
			}
			if (!drawArgs.WorldCamera.ViewFrustum.ContainsPoint(icon.Position)) {
				return;
			}

			// check whether in icon's visual range.
			float distanceToIcon = Vector3.Length(icon.Position - drawArgs.WorldCamera.Position);
			if (distanceToIcon > icon.MaximumDisplayDistance) {
				return;
			}
			if (distanceToIcon < icon.MinimumDisplayDistance) {
				return;
			}

			IconTexture iconTexture = this.GetTexture(icon);
			// check is mouse over.
			bool isMouseOver = (icon == this.mouseOverIcon);
			if (isMouseOver) {
				if (icon.IsSelectable) {
					DrawArgs.MouseCursor = CursorType.Hand;
				}

				// get icon's description.
				string description = icon.Description;
				if (description == null) {
					description = icon.ClickableActionURL;
				}

				if (description != null) {
					// Render the description field.
					DrawTextFormat descTextFormat = DrawTextFormat.NoClip;
					descTextFormat |= DrawTextFormat.WordBreak;
					descTextFormat |= DrawTextFormat.Bottom;

					int left = 10;
					//if (World.Settings.ShowLayerManager) {
					//   left += World.Settings.LayerManagerWidth;
					//}
					Rectangle descRectangle = Rectangle.FromLTRB(left, 10, drawArgs.ScreenWidth - 10, drawArgs.ScreenHeight - 10);

					// Draw outline
					this.DrawOutline(drawArgs.DefaultDrawingFont, this.m_sprite, description, ref descRectangle, descTextFormat);

					// Draw description
					drawArgs.DefaultDrawingFont.DrawText(this.m_sprite, description, descRectangle, descTextFormat, descriptionColor);
				}
			}

			int color = isMouseOver ? hotColor : normalColor;
			// calculate scale
			double scale = (drawArgs.WorldCamera.WorldRadius + icon.Altitude)/(drawArgs.WorldCamera.WorldRadius + distanceToIcon);
			scale *= drawArgs.WorldCamera.TargetDistance/distanceToIcon;
			//
			// render name field.
			if (icon.Name != null) {
				Rectangle nameRectangle = drawArgs.DefaultDrawingFont.MeasureString(this.m_sprite, icon.Name, DrawTextFormat.Center, color);
				nameRectangle.X = (int) projectedPoint.X - (nameRectangle.Width >> 1);
				if (iconTexture == null) {
					// by zzm start
					nameRectangle.Y = (int) projectedPoint.Y - (drawArgs.DefaultDrawingFont.Description.Height >> 1);
					// by zzm end
					this.DrawOutline(drawArgs.DefaultDrawingFont, this.m_sprite, icon.Name, ref nameRectangle, DrawTextFormat.Center);
					drawArgs.DefaultDrawingFont.DrawText(this.m_sprite, icon.Name, nameRectangle, DrawTextFormat.Center, color);
				}
				else {
					// adjust text to make room for icon.
					int spacing = 10;
					int offsetForIcon = (int) (icon.Height*scale) + spacing;
					nameRectangle.Y = (int) projectedPoint.Y - offsetForIcon - (drawArgs.DefaultDrawingFont.Description.Height >> 1);
					this.DrawOutline(drawArgs.DefaultDrawingFont, this.m_sprite, icon.Name, ref nameRectangle, DrawTextFormat.Center);
					drawArgs.DefaultDrawingFont.DrawText(this.m_sprite, icon.Name, nameRectangle, DrawTextFormat.Center, color);
				}
			}

			if (iconTexture != null) {
				// render icon
				// get icon's current scale
				float xScale = (float) scale; //icon.Width / iconTexture.Width;
				float yScale = (float) scale; //icon.Height / iconTexture.Height;
				this.m_sprite.Transform = Matrix.Scaling(xScale, yScale, 0);
				this.m_sprite.Transform *= Matrix.Translation(projectedPoint.X, projectedPoint.Y, 0);
				this.m_sprite.Draw(iconTexture.Texture, new Vector3(iconTexture.Width, iconTexture.Height, 0), Vector3.Empty, color);
				this.m_sprite.Transform = Matrix.Identity;
			}
		}

		public override void Highlight(string itemName, bool highlight) {
			if (highlight) {
				base.mouseOverIcon = base.GetObject(itemName) as Icon;
			}
			else {
				base.mouseOverIcon = null;
			}
		}

		/// <summary>
		/// Draw text outline
		/// </summary>
		/// <param name="font"></param>
		/// <param name="sprite"></param>
		/// <param name="text"></param>
		/// <param name="rect"></param>
		/// <param name="format"></param>
		/// by zzm
		private void DrawOutline(D3Font font, Sprite sprite, string text, ref Rectangle rect, DrawTextFormat format) {
			int color = 0xB0 << 24;
			font.DrawText(sprite, text, rect, format, color);
			rect.Offset(2, 0);
			font.DrawText(sprite, text, rect, format, color);
			rect.Offset(0, 2);
			font.DrawText(sprite, text, rect, format, color);
			rect.Offset(-2, 0);
			font.DrawText(sprite, text, rect, format, color);
			rect.Offset(1, -1);
		}
	}
}