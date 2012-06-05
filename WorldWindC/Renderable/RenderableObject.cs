using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using Microsoft.DirectX;
//using WorldWind.Menu;
using WorldWind.VisualControl;

namespace WorldWind.Renderable {
	/// <summary>
	/// The base class for objects to be rendered as part of the scene.
	/// </summary>
	public abstract class RenderableObject : IRenderable, IComparable {
		
		private bool _inited;
		/// <summary>
		/// True when object is ready to be rendered.
		/// </summary>
		public bool Inited {
			get {
				return _inited;
			}
			set {
				_inited = value;
			}
		}

		private bool isSelectable;
		/// <summary>
		/// True for objects the user can interact with.
		/// </summary>
		public bool IsSelectable {
			get {
				return isSelectable;
			}
			set {
				isSelectable = value;
			}
		}

		public RenderableObjectList ParentList;

		protected string name;
		protected Hashtable _metaData = new Hashtable();
		protected Vector3 position;
		protected Quaternion orientation;
		protected bool isOn = true;
		protected byte m_opacity = 255;
		protected RenderPriority m_renderPriority = RenderPriority.SurfaceImages;
		protected Form m_propertyBrowser;

		protected Image m_thumbnailImage;
		protected string m_iconImagePath;
		protected Image m_iconImage;
		protected World m_world;
		private string m_thumbnail;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.RenderableObject"/> class.
		/// </summary>
		/// <param name="name">Object description</param>
		protected RenderableObject(string name) {
			this.name = name;
		}

		protected RenderableObject(string name, World parentWorld) {
			this.name = name;
			this.m_world = parentWorld;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="name">Object description</param>
		/// <param name="position">Object position (XYZ world coordinates)</param>
		/// <param name="orientation">Object rotation (Quaternion)</param>
		protected RenderableObject(string name, Vector3 position, Quaternion orientation) {
			this.name = name;
			this.position = position;
			this.orientation = orientation;
		}

		public abstract void Initialize(DrawArgs drawArgs);

		public abstract void Update(DrawArgs drawArgs);

		public abstract void Render(DrawArgs drawArgs);

		public virtual bool Initialized {
			get {
				return _inited;
			}
		}

		/// <summary>
		/// The planet this layer is a part of.
		/// </summary>
		[TypeConverter(typeof (ExpandableObjectConverter))]
		public virtual World World {
			get {
				return m_world;
			}
		}

		/// <summary>
		/// Path to a Thumbnail image(e.g. for use as a Toolbar button).
		/// </summary>
		public virtual string Thumbnail {
			get {
				return m_thumbnail;
			}
			set {
				m_thumbnail = ImageHelper.FindResource(value);
			}
		}

		/// <summary>
		/// The image referenced by Thumbnail. 
		/// </summary>
		public virtual Image ThumbnailImage {
			get {
				if (m_thumbnailImage == null) {
					if (m_thumbnail == null) {
						return null;
					}
					try {
						if (File.Exists(m_thumbnail)) {
							m_thumbnailImage = ImageHelper.LoadImage(m_thumbnail);
						}
					}
					catch {}
				}
				return m_thumbnailImage;
			}
		}

		/// <summary>
		/// Path for an icon for the object, such as an image to be used in the Active Layer window.
		/// This can be different than the Thumbnail(e.g. an ImageLayer can have an IconImage, and no Thumbnail).
		/// </summary>
		public string IconImagePath {
			get {
				return m_iconImagePath;
			}
			set {
				m_iconImagePath = value;
			}
		}

		/// <summary>
		/// The icon image referenced by IconImagePath. 
		/// </summary>
		public Image IconImage {
			get {
				if (m_iconImage == null) {
					if (m_iconImagePath == null) {
						return null;
					}
					try {
						if (File.Exists(m_iconImagePath)) {
							m_iconImage = ImageHelper.LoadImage(m_iconImagePath);
						}
					}
					catch {}
				}
				return m_iconImage;
			}
		}

		/// <summary>
		/// Called when object is disabled.
		/// </summary>
		public abstract void Dispose();

		/// <summary>
		/// User interaction (mouse click)
		/// </summary>
		public abstract bool PerformSelectionAction(DrawArgs drawArgs);

		public int CompareTo(object obj) {
			RenderableObject robj = obj as RenderableObject;
			if (obj == null) {
				return 1;
			}

			return this.m_renderPriority.CompareTo(robj.RenderPriority);
		}

		/// <summary>
		/// Permanently delete the layer
		/// </summary>
		public virtual void Delete() {
			string namestring = name;
			//TODO: Make the string absolute from the XML root so that we don't delete stuff we aren't supposed to...
			string xmlConfigFile = (string) MetaData["XML Configuration File"];
			if (xmlConfigFile == null) {
				xmlConfigFile = (string) ParentList.MetaData["XML Configuration File"];
			}

			if (xmlConfigFile == null
			    || !File.Exists(xmlConfigFile)) {
				throw new ApplicationException("Error deleting layer.");
			}

			XmlDocument doc = new XmlDocument();
			doc.Load(xmlConfigFile);

			XmlNodeList list;
			XmlElement root = doc.DocumentElement;
			list = root.SelectNodes("//*[Name='" + namestring + "']");

			ParentList.Remove(Name);

			list[0].ParentNode.RemoveChild(list[0]);
			if (File.Exists(xmlConfigFile.Replace(".xml", ".bak"))) {
				File.Delete(xmlConfigFile.Replace(".xml", ".bak"));
			}
			File.Move(xmlConfigFile, xmlConfigFile.Replace(".xml", ".bak"));
			doc.Save(xmlConfigFile);
		}

		/// <summary>
		/// Fills the context menu with menu items specific to the layer.
		/// </summary>
		/// <param name="menu">Pre-initialized context menu.</param>
		public virtual void BuildContextMenu(ContextMenu menu) {
			menu.MenuItems.Add("Goto Extent", new EventHandler(OnGotoExtentClick));
			menu.MenuItems.Add("Properties", new EventHandler(OnPropertiesClick));
			menu.MenuItems.Add("Info", new EventHandler(OnInfoClick));
			menu.MenuItems.Add("Delete", new EventHandler(OnDeleteClick));
		}

		/// <summary>
		/// Returns a String that represents the current SelectedObject.
		/// </summary>
		public override string ToString() {
			return name;
		}

		#region Properties
		/// <summary>
		/// The object's render priority determining in what order it will be rendered
		/// compared to the other objects.
		/// </summary>
		[Description("The object's render priority determining in what order it will be rendered compared to the other objects.")]
		public virtual RenderPriority RenderPriority {
			get {
				return this.m_renderPriority;
			}
			set {
				this.m_renderPriority = value;
				if (ParentList != null) {
					ParentList.SortChildren();
				}
			}
		}

		/// <summary>
		/// How transparent this object should appear (0=invisible, 255=opaque)
		/// </summary>
		[Description("Controls the amount of light allowed to pass through this object. (0=invisible, 255=opaque).")]
		public virtual byte Opacity {
			get {
				return this.m_opacity;
			}
			set {
				this.m_opacity = value;
				if (value == 0) {
					// invisible - turn off
					if (this.isOn) {
						this.IsOn = false;
					}
				}
				else {
					// visible - turn back on
					if (!this.isOn) {
						this.IsOn = true;
					}
				}
			}
		}

		[Browsable(false)]
		public virtual Hashtable MetaData {
			get {
				return this._metaData;
			}
		}

		/// <summary>
		/// Hide/Show this object.
		/// </summary>
		[Description("This layer's enabled status.")]
		public virtual bool IsOn {
			get {
				return this.isOn;
			}
			set {
				if (isOn && !value) {
					this.Dispose();
				}
				this.isOn = value;
			}
		}

		/// <summary>
		/// Describes this object
		/// </summary>
		[Description("This layer's name.")]
		public virtual string Name {
			get {
				return this.name;
			}
			set {
				this.name = value;
			}
		}

		/// <summary>
		/// Object position (XYZ world coordinates)
		/// </summary>
		[Browsable(false)]
		public virtual Vector3 Position {
			get {
				return this.position;
			}
			set {
				this.position = value;
			}
		}

		/// <summary>
		/// Object rotation (Quaternion)
		/// </summary>
		[Browsable(false)]
		public virtual Quaternion Orientation {
			get {
				return this.orientation;
			}
			set {
				this.orientation = value;
			}
		}
		#endregion

		#region Menu items
		///<summary>
		///  Goes to the extent specified by the bounding box for the QTS layer
		/// </summary>
		protected virtual void OnGotoExtentClick(object sender, EventArgs e) {
			lock (this.ParentList.ChildObjects.SyncRoot) {
				for (int i = 0; i < this.ParentList.ChildObjects.Count; i++) {
					RenderableObject ro = (RenderableObject) this.ParentList.ChildObjects[i];
					if (ro.Name.Equals(name)) {
						if (ro is QuadTileSet) {
							QuadTileSet qts = (QuadTileSet) ro;
							DrawArgs.Camera.SetPosition((qts.North + qts.South)/2, (qts.East + qts.West)/2);
							double perpendicularViewRange = (qts.North - qts.South > qts.East - qts.West ? qts.North - qts.South : qts.East - qts.West);
							double altitude = qts.LayerRadius*Math.Sin(MathEngine.DegreesToRadians(perpendicularViewRange*0.5));

							DrawArgs.Camera.Altitude = altitude;

							break;
						}
						if (ro is ShapeFileLayer) {
							ShapeFileLayer slayer = (ShapeFileLayer) ro;
							DrawArgs.Camera.SetPosition((slayer.North + slayer.South)/2, (slayer.East + slayer.West)/2);
							double perpendicularViewRange = (slayer.North - slayer.South > slayer.East - slayer.West ? slayer.North - slayer.South : slayer.East - slayer.West);
							double altitude = slayer.MaxAltitude;

							DrawArgs.Camera.Altitude = altitude;

							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Layer info context menu item
		/// </summary>
		protected virtual void OnInfoClick(object sender, EventArgs e) {
			//LayerManagerItemInfo lmii = new LayerManagerItemInfo(MetaData);
			//lmii.ShowDialog();
		}

		/// <summary>
		/// Layer properties context menu item
		/// </summary>
		protected virtual void OnPropertiesClick(object sender, EventArgs e) {
			if (m_propertyBrowser != null) {
				m_propertyBrowser.Dispose();
			}

			m_propertyBrowser = new PropertyBrowser(this);
			m_propertyBrowser.Show();
		}

		/// <summary>
		/// Delete layer context menu item
		/// </summary>
		protected virtual void OnDeleteClick(object sender, EventArgs e) {
			if (ParentList == null
			    || ParentList.ParentList == null) {
				MessageBox.Show("Unable to delete root layer list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}

			string message = "Permanently Delete Layer '" + name + "'?";
			if (DialogResult.Yes
			    != MessageBox.Show(message, "Delete layer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)) {
				return;
			}

			try {
				Delete();
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}
		#endregion

		#region by zzm
		/// <summary>
		/// Fired when mouse enter a renderableobject.
		/// </summary>
		public event MouseEnterHandler MouseEnter;

		/// <summary>
		/// Fired when mouse leave a renderableobject.
		/// </summary>
		public event MouseLeaveHandler MouseLeave;

		public virtual void OnMouseEnter() {
			if (this.MouseEnter != null) {
				this.MouseEnter(this.ParentList.Name, this.Name);
			}
		}

		public virtual void OnMouseLeave() {
			if (this.MouseLeave != null) {
				this.MouseEnter(this.ParentList.Name, this.Name);
			}
		}

		/// <summary>
		/// How to set up camera view for this object, includes altitude/heading/tilt, default is NaN.
		/// </summary>
		public CameraView View = new CameraView();

		/// <summary>
		/// Goes to the extent specified by the bounding box for the QTS layer
		/// </summary>
		public virtual void GoToExtent() {
			this.OnGotoExtentClick(null, EventArgs.Empty);
		}
		#endregion
	}
}