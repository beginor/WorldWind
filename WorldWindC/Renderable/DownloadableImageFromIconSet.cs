using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Threading;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Net;
using WorldWind.Terrain;
using WorldWind.Utilities;

namespace WorldWind.Renderable {
	/// <summary>
	/// Displays images on the globe (used by Rapid Fire MODIS)
	/// </summary>
	public class DownloadableImageFromIconSet : RenderableObject {
		#region Private Members
		private World m_ParentWorld;
		private float layerRadius;
		private DrawArgs drawArgs;
		private DownloadableIcon currentlyDisplayed;
		private Hashtable textureHash = new Hashtable();
		private TerrainAccessor _terrainAccessor;
		private Hashtable downloadableIconList = new Hashtable();
		private DownloadableIcon[] returnList = new DownloadableIcon[0];
		#endregion

		#region Properties
		public DownloadableIcon[] DownloadableIcons {
			get {
				return returnList;
			}
		}
		#endregion

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.DownloadableImageFromIconSet"/> class.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="drawArgs"></param>
		/// <param name="terrainAccessor"></param>
		public DownloadableImageFromIconSet(string name, World parentWorld, float distanceAboveSurface, DrawArgs drawArgs, TerrainAccessor terrainAccessor) : base(name, parentWorld.Position, parentWorld.Orientation) {
			this.m_ParentWorld = parentWorld;
			this.layerRadius = (float) parentWorld.EquatorialRadius + distanceAboveSurface;
			this.IsSelectable = true;
			this.drawArgs = drawArgs;
			this._terrainAccessor = terrainAccessor;
		}

		public override void Initialize(DrawArgs drawArgs) {
			this.Inited = true;
		}

		public void AddDownloadableIcon(string name, float altitude, float west, float south, float east, float north, string imageUrl, string saveTexturePath, string iconFilePath, int iconSize, string caption) {
			Texture t = null;

			if (!this.textureHash.Contains(iconFilePath)) {
				t = ImageHelper.LoadTexture(iconFilePath);
				lock (this.textureHash.SyncRoot) {
					this.textureHash.Add(iconFilePath, t);
				}
			}
			else {
				t = (Texture) this.textureHash[iconFilePath];
			}
			DownloadableIcon di = new DownloadableIcon(name, m_ParentWorld, this.layerRadius - (float) m_ParentWorld.EquatorialRadius + altitude, west, south, east, north, imageUrl, saveTexturePath, t, iconSize, caption, this._terrainAccessor);
			di.IsOn = true;
			di.IconFilePath = iconFilePath;

			lock (this.downloadableIconList.SyncRoot) {
				if (!this.downloadableIconList.Contains(di.Name)) {
					this.downloadableIconList.Add(di.Name, di);
				}

				this.returnList = new DownloadableIcon[this.downloadableIconList.Count];
				int c = 0;
				foreach (DownloadableIcon dicon in this.downloadableIconList.Values) {
					this.returnList[c++] = dicon;
				}
			}
		}

		public void LoadDownloadableIcon(string name) {
			DownloadableIcon di = null;
			lock (this.downloadableIconList.SyncRoot) {
				if (this.downloadableIconList.Contains(name)) {
					di = (DownloadableIcon) this.downloadableIconList[name];
				}
			}
			if (di != null) {
				if (this.currentlyDisplayed != null) {
					this.currentlyDisplayed.Dispose();
				}

				di.DownLoadImage(drawArgs);
				this.currentlyDisplayed = di;
			}
		}

		public void RemoveDownloadableIcon(string name) {
			try {
				DownloadableIcon di = null;
				lock (this.downloadableIconList.SyncRoot) {
					if (this.downloadableIconList.Contains(name)) {
						di = (DownloadableIcon) this.downloadableIconList[name];
						this.downloadableIconList.Remove(name);
					}
					this.returnList = new DownloadableIcon[this.downloadableIconList.Count];
					int c = 0;
					foreach (DownloadableIcon dicon in this.downloadableIconList.Values) {
						this.returnList[c++] = dicon;
					}
				}

				if (this.currentlyDisplayed != null
				    && name == this.currentlyDisplayed.Name) {
					this.currentlyDisplayed = null;
				}

				if (di != null) {
					di.Dispose();
				}
			}
			catch (Exception caught) {
				Log.Write(caught);
			}
		}

		public override void Dispose() {
			this.Inited = false;
			lock (this.downloadableIconList.SyncRoot) {
				foreach (string key in this.downloadableIconList.Keys) {
					DownloadableIcon di = (DownloadableIcon) this.downloadableIconList[key];
					di.Dispose();
				}
			}
		}

		public override bool PerformSelectionAction(DrawArgs drawArgs) {
			if (this.ShowOnlyCurrentlySelected) {
				return false;
			}

			lock (this.downloadableIconList.SyncRoot) {
				foreach (string key in this.downloadableIconList.Keys) {
					if (this.currentlyDisplayed != null
					    && key == this.currentlyDisplayed.Name) {
						continue;
					}
					DownloadableIcon di = (DownloadableIcon) this.downloadableIconList[key];
					if (di.WasClicked(drawArgs)) {
						if (this.currentlyDisplayed != null) {
							this.currentlyDisplayed.LoadImage = false;
						}
						di.LoadImage = true;
						di.PerformSelectionAction(drawArgs);
						this.currentlyDisplayed = di;
						return true;
					}
				}
			}
			return false;
		}

		public bool ShowOnlyCurrentlySelected = false;

		public override void Update(DrawArgs drawArgs) {
			try {
				if (!this.Inited) {
					this.Initialize(drawArgs);
				}

				lock (this.downloadableIconList.SyncRoot) {
					foreach (DownloadableIcon di in this.downloadableIconList.Values) {
						if (!di.Inited) {
							di.Initialize(drawArgs);
						}
						di.Update(drawArgs);
					}
				}
			}
			catch {}
		}

		public override void Render(DrawArgs drawArgs) {
			if (!this.ShowOnlyCurrentlySelected) {
				lock (this.downloadableIconList.SyncRoot) {
					foreach (DownloadableIcon di in this.downloadableIconList.Values) {
						di.Render(drawArgs);
					}
				}
			}
			else {
				if (this.currentlyDisplayed != null) {
					this.currentlyDisplayed.Render(drawArgs);
				}
			}
		}
	}
	
}