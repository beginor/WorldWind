using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Permissions;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Camera;
using WorldWind.Configuration;
using WorldWind.Interop;
using WorldWind.Net;
using WorldWind.Net.Wms;
using WorldWind.Renderable;
using WorldWind.Utilities;
using WorldWind.Widgets;
using Timer = System.Timers.Timer;
using System.Text;

namespace WorldWind {

	/// <summary>
	/// WorldWindow, ��ʾWW��D3D����
	/// </summary>
	public class WorldWindow : Control, IGlobe {

		private Device _device;
		private PresentParameters _presentParams;
		private DrawArgs _drawArgs;
		private World _world;
		private Cache _cache;
		private Thread _workerThread;
		private bool _showDiagnosticInfo;
		private string _caption = "";
		private long _lastFpsUpdateTime;
		private int _frameCounter;
		private float _fps;
		private string _saveScreenShotFilePath;
		private ImageFileFormat _saveScreenShotImageFileFormat = ImageFileFormat.Bmp;
		private bool _workerThreadRunning;
		private bool _renderDisabled; // True when WW isn't active - CPU saver
		private bool _mouseDragging;
		private Point _mouseDownStartPosition = Point.Empty;
		private bool _renderWireFrame;
		private Timer _fpsTimer = new Timer(250);
		private bool _isDoubleClick = false;
		private ArrayList _frameTimes = new ArrayList();
		private RootWidget _rootWidget = null;
		private LineGraph _fpsGraph = new LineGraph();
		private const int _positionAlphaStep = 20;
		private int _positionAlpha = 255;
		private int _positionAlphaMin = 40;
		private int _positionAlphaMax = 205;
		private Line _crossHairs;
		private int _crossHairColor = Color.GhostWhite.ToArgb();
		private Angle _cLat, _cLon;
		private bool _fpsUpdate = false;

		private static WorldWindSettings settings;

		/// <summary>
		/// ����һ�� <see cref= "T:WorldWind.WorldWindow"/> ��ʵ��
		/// </summary>
		public WorldWindow() {
			this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

			// The m_Device3d can't be created unless the control is at least 1 x 1 pixels in size
			this.Size = new Size(1, 1);

			try {
				// Now perform the rendering m_Device3d initialization
				// Skip DirectX initialization in design mode
				if (!IsInDesignMode()) {
					this.InitializeGraphics();
				}

				//Post m_Device3d creation initialization
				this._drawArgs = new DrawArgs(_device, this);
				this._rootWidget = new RootWidget(this);
				DrawArgs.RootWidget = this._rootWidget;
				// load default settings
				this.Initialize();

				_fpsTimer.Elapsed += new ElapsedEventHandler(FpsTimer_Elapsed);
				_fpsTimer.Start();
			}
			catch (InvalidCallException ex) {
				MessageBox.Show(ex.Message, "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}
			catch (NotAvailableException ex) {
				MessageBox.Show(ex.Message, "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}
		}

		/// <summary>
		/// ʹ��Ĭ�ϵĳ�ʼ��
		/// </summary>
		private void Initialize() {
			// ע����Ⱦѭ��
			Application.Idle += this.OnApplicationIdle;

			settings = new WorldWindSettings();
			// ��ȡ������Ϣ���Ժ����ȥ����ֱ��ʹ��ϵͳ����
			//DataProtector dp = new DataProtector(DataProtector.Store.USE_USER_STORE);
			// load settings;
			World.LoadSettings();

			this.Cache = new Cache(settings.CachePath, settings.CacheCleanupInterval, settings.TotalRunTime);
			DirectoryInfo configDirInfo = new DirectoryInfo(settings.ConfigPath);
			if (!configDirInfo.Exists) {
				MessageBox.Show("����Ŀ¼������","����", MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}
			FileInfo[] configFiles = configDirInfo.GetFiles("*.xml", SearchOption.TopDirectoryOnly);
			this.CurrentWorld = ConfigurationLoader.Load(configFiles[0].FullName, this.Cache);
		}

		public World CurrentWorld {
			get {
				return _world;
			}
			set {
				_world = value;
				if (_world != null) {
					MomentumCamera camera = new MomentumCamera(_world.Position, _world.EquatorialRadius);
					if (!World.Settings.CameraResetsAtStartup) {
						camera.SetPosition(World.Settings.CameraLatitude.Degrees, World.Settings.CameraLongitude.Degrees, World.Settings.CameraHeading.Degrees, World.Settings.CameraAltitude, World.Settings.CameraTilt.Degrees, 0);
					}
					this._drawArgs.WorldCamera = camera;

					this._drawArgs.CurrentWorld = value;
					if (World.Settings.ShowLatLonLines) {
						_world.RenderableObjects.Add(new LatLongGrid(_world));
					}
				}
			}
		}

		/// <summary>
		/// ���û��ȡ����
		/// </summary>
		public string Caption {
			get {
				return this._caption;
			}
			set {
				this._caption = value;
			}
		}
		/// <summary>
		/// ��ȡ��ǰʹ�õĻ�ͼ������
		/// </summary>
		public DrawArgs DrawArgs {
			get {
				return this._drawArgs;
			}
		}
		/// <summary>
		/// ��ȡ�����û���
		/// </summary>
		public Cache Cache {
			get {
				return _cache;
			}
			set {
				_cache = value;
			}
		}

		/// <summary>
		/// ��ȡ������һ��ֵ����ʾ�Ƿ�ֹͣ��Ⱦ����ԼCPU��Դ��
		/// </summary>
		public bool RenderDisabled {
			get {
				return _renderDisabled;
			}
			set {
				_renderDisabled = value;
			}
		}

		/// <summary>
		/// �ƶ������ָ����λ��
		/// </summary>
		/// <param name="latitude">Ŀ���γ�� (-90 ~ 90)��</param>
		/// <param name="longitude">Ŀ��ľ��� (-180 - 180)��</param>
		/// <param name="heading">����ķ��� (0-360) �Ȼ��� double.NaN ��ʾ����</param>
		/// <param name="altitude">����ĸ߶ȣ�����Ϊ��λ�� double.NaN ��ʾ����</param>
		/// <param name="perpendicularViewRange">��ֱ����ʱ�Ŀ��ԽǶȣ����ڼ��������صĸ߶�, �����������ġ�������Ĺ�ȹ̶�90�ȣ�</param>
		/// <param name="tilt">�������б�� (-90 - 90)�� double.NaN ��ʾ���� </param>
		public void GotoLatLon(double latitude, double longitude, double heading, double altitude, double perpendicularViewRange, double tilt) {
			if (!double.IsNaN(perpendicularViewRange)) {
				altitude = _world.EquatorialRadius * Math.Sin(MathEngine.DegreesToRadians(perpendicularViewRange*0.5));
			}
			if (altitude < 1) {
				altitude = 1;
			}
			this._drawArgs.WorldCamera.SetPosition(latitude, longitude, heading, altitude, tilt);
		}
		/// <summary>
		/// �ƶ������ָ����Ŀ��
		/// </summary>
		/// <param name="latitude">Ŀ��γ��</param>
		/// <param name="longitude">Ŀ�꾭��</param>
		public void GotoLatLon(double latitude, double longitude) {
			this._drawArgs.WorldCamera.SetPosition(latitude, longitude, this._drawArgs.WorldCamera.Heading.Degrees, this._drawArgs.WorldCamera.Altitude, this._drawArgs.WorldCamera.Tilt.Degrees);
		}
		/// <summary>
		/// ת�������ָ����Ŀ��
		/// </summary>
		/// <param name="latitude">Ŀ��γ��</param>
		/// <param name="longitude">Ŀ�꾭��</param>
		/// <param name="altitude">Ŀ��߶�</param>
		public void GotoLatLonAltitude(double latitude, double longitude, double altitude) {
			this._drawArgs.WorldCamera.SetPosition(latitude, longitude, this._drawArgs.WorldCamera.Heading.Degrees, altitude, this._drawArgs.WorldCamera.Tilt.Degrees);
		}
		/// <summary>
		/// ת�������ָ����Ŀ��
		/// </summary>
		/// <param name="latitude">Ŀ��γ��</param>
		/// <param name="longitude">Ŀ�꾭��</param>
		/// <param name="heading">�������</param>
		/// <param name="perpendicularViewRange">��ֱ����ʱ�Ŀ��ӽǶȣ����ڼ��������صĸ߶�</param>
		public void GotoLatLonHeadingViewRange(double latitude, double longitude, double heading, double perpendicularViewRange) {
			double altitude = _world.EquatorialRadius*Math.Sin(MathEngine.DegreesToRadians(perpendicularViewRange*0.5));
			this.GotoLatLonHeadingAltitude(latitude, longitude, heading, altitude);
		}

		/// <summary>
		/// ת�������ָ����Ŀ��
		/// </summary>
		/// <param name="latitude">Ŀ��γ��</param>
		/// <param name="longitude">Ŀ�꾭��</param>
		/// <param name="perpendicularViewRange">��ֱ����ʱ�Ŀ��ӽǶȣ����ڼ��������صĸ߶�</param>
		public void GotoLatLonViewRange(double latitude, double longitude, double perpendicularViewRange) {
			double altitude = _world.EquatorialRadius*Math.Sin(MathEngine.DegreesToRadians(perpendicularViewRange*0.5));
			this.GotoLatLonHeadingAltitude(latitude, longitude, this._drawArgs.WorldCamera.Heading.Degrees, altitude);
		}
		/// <summary>
		/// ת�������ָ����Ŀ��
		/// </summary>
		/// <param name="latitude">Ŀ��γ��</param>
		/// <param name="longitude">Ŀ�꾭��</param>
		/// <param name="heading">����ĳ���</param>
		/// <param name="altitude">��������ĸ߶�</param>
		public void GotoLatLonHeadingAltitude(double latitude, double longitude, double heading, double altitude) {
			this._drawArgs.WorldCamera.SetPosition(latitude, longitude, heading, altitude, this._drawArgs.WorldCamera.Tilt.Degrees);
		}

		/// <summary>
		/// ����ǰ��ͼ����ΪͼƬ
		/// </summary>
		/// <param name="filePath">ͼƬ����·��</param>
		public void SaveScreenshot(string filePath) {
			if (_device == null) {
				return;
			}

			FileInfo saveFileInfo = new FileInfo(filePath);
			string ext = saveFileInfo.Extension.Replace(".", "");
			try {
				this._saveScreenShotImageFileFormat = (ImageFileFormat) Enum.Parse(typeof (ImageFileFormat), ext, true);
			}
			catch (ArgumentException) {
				throw new ApplicationException("Unknown file type/file extension for file '" + filePath + "'.  Unable to save.");
			}

			if (!saveFileInfo.Directory.Exists) {
				saveFileInfo.Directory.Create();
			}

			this._saveScreenShotFilePath = filePath;
		}

		/// <summary>
		/// world����Ⱦѭ��
		/// Borrowed from FlightGear and Tom Miller's blog
		/// </summary>
		public void OnApplicationIdle(object sender, EventArgs e) {
			// Sleep will always overshoot by a bit so under-sleep by
			// 2ms in the hopes of never oversleeping.
			const float SleepOverHeadSeconds = 2e-3f;

			try {
				while (IsAppStillIdle) {
					if (!World.Settings.AlwaysRenderWindow && _renderDisabled && !World.Settings.CameraHasMomentum) {
						return;
					}

					Render();

					if (World.Settings.ThrottleFpsHz > 0) {
						// optionally throttle the frame rate (to get consistent frame
						// rates or reduce CPU usage.
						float frameSeconds = 1.0f/World.Settings.ThrottleFpsHz;

						// Sleep for remaining period of time until next render
						float sleepSeconds = frameSeconds - SleepOverHeadSeconds - DrawArgs.SecondsSinceLastFrame;
						if (sleepSeconds > 0) {
							// Don't sleep too long. We don't know the accuracy of Thread.Sleep
							Thread.Sleep((int) (1000*sleepSeconds));
						}
					}
					// Flip
					_drawArgs.Present();
				}
			}
			catch (DeviceLostException) {
				AttemptRecovery();
			}
			catch (Exception caught) {
				Log.Write(caught);
			}
		}

		/// <summary>
		/// ȷ��Ӧ�ó����Ƿ����
		/// </summary>
		private static bool IsAppStillIdle {
			get {
				NativeMethods.Message msg;
				return !NativeMethods.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);
			}
		}

		/// <summary>
		/// Occurs when the control is redrawn and m_isRenderDisabled=true.
		/// All other painting is handled in WndProc.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnPaint(PaintEventArgs e) {
			// Paint the last active scene if rendering is disabled to keep the ui responsive
			try {
				if (_device == null) {
					e.Graphics.Clear(SystemColors.Control);
					return;
				}

				_device.Present();
			}
			catch (DeviceLostException) {
				try {
					AttemptRecovery();

					// Our surface was lost, force re-render
					Render();

					_device.Present();
				}
				catch (DirectXException) {
					// Ignore a 2nd failure
				}
			}
		}

		/// <summary>
		/// ��Ⱦ����
		/// </summary>
		public void Render() {
			long startTicks = 0;
			PerformanceTimer.QueryPerformanceCounter(ref startTicks);

			try {
				this._drawArgs.BeginRender();

				// Render the sky according to view - example, close to earth, render sky blue, render space as black
				Color backgroundColor = Color.Black;

				if (_drawArgs.WorldCamera != null && _drawArgs.WorldCamera.Altitude < 1000000f && _world != null && _world.Name.IndexOf("Earth") >= 0) {
					float percent = 1 - (float) (_drawArgs.WorldCamera.Altitude/1000000);
					if (percent > 1.0f) {
						percent = 1.0f;
					}
					else if (percent < 0.0f) {
						percent = 0.0f;
					}

					backgroundColor = Color.FromArgb((int) (World.Settings.SkyColor.R*percent), (int) (World.Settings.SkyColor.G*percent), (int) (World.Settings.SkyColor.B*percent));
				}

				_device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, backgroundColor, 1.0f, 0);

				if (_world == null) {
					_device.BeginScene();
					_device.EndScene();
					_device.Present();
					Thread.Sleep(25);
					return;
				}

				if (_workerThread == null) {
					_workerThreadRunning = true;
					_workerThread = new Thread(new ThreadStart(WorkerThreadFunc));
					_workerThread.Name = "WorldWindow.WorkerThreadFunc";
					_workerThread.IsBackground = true;
					if (World.Settings.UseBelowNormalPriorityUpdateThread) {
						_workerThread.Priority = ThreadPriority.BelowNormal;
					}
					else {
						_workerThread.Priority = ThreadPriority.Normal;
					}
					_workerThread.Start();
				}

				this._drawArgs.WorldCamera.Update(_device);

				_device.BeginScene();

				// Set fill mode
				if (_renderWireFrame) {
					_device.RenderState.FillMode = FillMode.WireFrame;
				}
				else {
					_device.RenderState.FillMode = FillMode.Solid;
				}

				_drawArgs.RenderWireFrame = _renderWireFrame;

				// Render the current planet
				_world.Render(this._drawArgs);

				if (World.Settings.ShowCrosshairs) {
					this.DrawCrossHairs();
				}

				_frameCounter++;
				if (_frameCounter == 30) {
					_fps = _frameCounter/(float) (DrawArgs.CurrentFrameStartTicks - _lastFpsUpdateTime)*PerformanceTimer.TicksPerSecond;
					_frameCounter = 0;
					_lastFpsUpdateTime = DrawArgs.CurrentFrameStartTicks;
				}

				if (_saveScreenShotFilePath != null) {
					SaveScreenShot();
				}

				_drawArgs.Device.RenderState.ZBufferEnable = false;

				// 3D rendering complete, switch to 2D for UI rendering

				// Restore normal fill mode
				if (_renderWireFrame) {
					_device.RenderState.FillMode = FillMode.Solid;
				}

				// Disable fog for UI
				_device.RenderState.FogEnable = false;

				RenderPositionInfo();

				_fpsGraph.Render(_drawArgs);
				if (_rootWidget != null) {
					try {
						_rootWidget.Render(_drawArgs);
					}
					catch (Exception ex) {
						Log.Write(ex);
					}
				}
				if (_world.OnScreenMessages != null) {
					try {
						foreach (OnScreenMessage dm in _world.OnScreenMessages) {
							int xPos = (int) Math.Round(dm.X*this.Width);
							int yPos = (int) Math.Round(dm.Y*this.Height);
							Rectangle posRect = new Rectangle(xPos, yPos, this.Width, this.Height);
							this._drawArgs.DefaultDrawingFont.DrawText(null, dm.Message, posRect, DrawTextFormat.NoClip | DrawTextFormat.WordBreak, Color.White);
						}
					}
					catch (Exception ex) {
						// Don't let a script error cancel the frame.
						Log.Write(ex);
					}
				}

				_device.EndScene();
			}
			finally {
				if (World.Settings.ShowFpsGraph) {
					long endTicks = 0;
					PerformanceTimer.QueryPerformanceCounter(ref endTicks);
					float elapsedMilliSeconds = 1000.0f/(1000.0f*(float) (endTicks - startTicks)/PerformanceTimer.TicksPerSecond);
					_frameTimes.Add(elapsedMilliSeconds);
				}
				this._drawArgs.EndRender();
			}
			_drawArgs.UpdateMouseCursor(this);
		}
		/// <summary>
		/// ��Ⱦλ����Ϣ
		/// </summary>
		protected void RenderPositionInfo() {
			// Render some Development information to screen
			//string captionText = _caption;
			StringBuilder msg = new StringBuilder();
			if (!string.IsNullOrEmpty(this.Caption)) {
				msg.AppendLine(this.Caption);
			}
			if (!string.IsNullOrEmpty(this.DrawArgs.UpperLeftCornerText)) {
				msg.AppendLine(this._drawArgs.UpperLeftCornerText);
			}

			if (World.Settings.ShowPosition) {
				string alt = null;
				float agl = (float) _drawArgs.WorldCamera.AltitudeAboveTerrain;
				if (agl > 100000) {
					alt = string.Format("{0:f2}ǧ��", agl/1000);
				}
				else {
					alt = string.Format("{0:f0}��", agl);
				}
				string dist = null;
				float dgl = (float) _drawArgs.WorldCamera.Distance;
				if (dgl > 100000) {
					dist = string.Format("{0:f2}ǧ��", dgl/1000);
				}
				else {
					dist = string.Format("{0:f0}��", dgl);
				}

				// Heading from 0 - 360
				double heading = _drawArgs.WorldCamera.Heading.Degrees;
				if (heading < 0) {
					heading += 360;
				}
				msg.AppendFormat("γ��: {0:}\n����: {1}\n����: {2:f2}��\n��б: {3}\n�߶�: {4:f3}\n����: {5:f4}\n��Ұ: {6:f2}", _drawArgs.WorldCamera.Latitude.ToStringDms(), _drawArgs.WorldCamera.Longitude.ToStringDms(), heading, _drawArgs.WorldCamera.Tilt.ToStringDms(), alt, dist, _drawArgs.WorldCamera.Fov);

				if (agl < 300000) {
					msg.AppendFormat("\n���溣�θ߶�: {0:n} ��\n", _drawArgs.WorldCamera.TerrainElevation);
				}
			}

			if (this._showDiagnosticInfo) {
				msg.Append("\nAvailable Texture Memory: " + (_device.AvailableTextureMemory/1024).ToString("N0") + " kB" + "\nBoundary Points: " + this._drawArgs.NumBoundaryPointsRendered.ToString() + " / " + this._drawArgs.NumBoundaryPointsTotal.ToString() + " : " + this._drawArgs.NumBoundariesDrawn.ToString() + "\nTiles Drawn: " + (this._drawArgs.NumberTilesDrawn*0.25f).ToString() + "\n" + this._drawArgs.WorldCamera + "\nFPS: " + this._fps.ToString("f1") + "\nRO: " + _world.RenderableObjects.Count.ToString("f0") + "\nmLat: " + this._cLat.Degrees.ToString() + "\nmLon: " + this._cLon.Degrees.ToString());
			}

			
			DrawTextFormat dtf = DrawTextFormat.NoClip | DrawTextFormat.WordBreak | DrawTextFormat.Right;
			int x = 7;
			int y = 7;
			Rectangle textRect = Rectangle.FromLTRB(x, y, this.Width - 8, this.Height - 8);

			_positionAlpha += _positionAlphaStep;
			if (_positionAlpha > _positionAlphaMax) {
				_positionAlpha = _positionAlphaMax;
			}

			int positionBackColor = _positionAlpha << 24;
			int positionForeColor = (int) ((uint) (_positionAlpha << 24) + 0xffffffu);
			this._drawArgs.DefaultDrawingFont.DrawText(null, msg.ToString(), textRect, dtf, positionBackColor);
			textRect.Offset(-1, -1);
			this._drawArgs.DefaultDrawingFont.DrawText(null, msg.ToString(), textRect, dtf, positionForeColor);
		}
		/// <summary>
		/// ����Ļ���Ļ���ʮ����
		/// </summary>
		protected void DrawCrossHairs() {
			int crossHairSize = 10;

			if (this._crossHairs == null) {
				_crossHairs = new Line(_device);
			}

			Vector2[] vertical = new Vector2[2];
			Vector2[] horizontal = new Vector2[2];

			horizontal[0].X = this.Width/2 - crossHairSize;
			horizontal[0].Y = this.Height/2;
			horizontal[1].X = this.Width/2 + crossHairSize;
			horizontal[1].Y = this.Height/2;

			vertical[0].X = this.Width/2;
			vertical[0].Y = this.Height/2 - crossHairSize;
			vertical[1].X = this.Width/2;
			vertical[1].Y = this.Height/2 + crossHairSize;

			_crossHairs.Begin();
			_crossHairs.Draw(horizontal, _crossHairColor);
			_crossHairs.Draw(vertical, _crossHairColor);
			_crossHairs.End();
		}

		/// <summary>
		/// Attempt to restore the 3D m_Device3d
		/// </summary>
		protected void AttemptRecovery() {
			try {
				_device.TestCooperativeLevel();
			}
			catch (DeviceLostException) {}
			catch (DeviceNotResetException) {
				try {
					_device.Reset(_presentParams);
				}
				catch (DeviceLostException) {
					// If it's still lost or lost again, just do
					// nothing
				}
			}
		}

		/// <summary>
		/// Occurs when the mouse wheel moves while the control has focus.
		/// </summary>
		protected override void OnMouseWheel(MouseEventArgs e) {
			try {
				this._drawArgs.WorldCamera.ZoomStepped(e.Delta/120.0f);
			}
			finally {
				// Call the base class's OnMouseWheel method so that registered delegates receive the event.
				base.OnMouseWheel(e);
			}
		}

		/// <summary>
		/// Occurs when a key is pressed while the control has focus.
		/// </summary>
		protected override void OnKeyDown(KeyEventArgs e) {
			try {
				e.Handled = HandleKeyDown(e);
				base.OnKeyDown(e);
			}
			catch (Exception caught) {
				MessageBox.Show(caught.Message, "Operation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Occurs when a key is released while the control has focus.
		/// </summary>
		protected override void OnKeyUp(KeyEventArgs e) {
			try {
				e.Handled = HandleKeyUp(e);
				base.OnKeyUp(e);
			}
			catch (Exception caught) {
				MessageBox.Show(caught.Message, "Operation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		protected override void OnKeyPress(KeyPressEventArgs e) {
			if (_rootWidget != null) {
				bool handled = _rootWidget.OnKeyPress(e);
				e.Handled = handled;
			}
			base.OnKeyPress(e);
		}

		/// <summary>
		/// Preprocess keyboard or input messages within the message loop before they are dispatched.
		/// </summary>
		/// <param name="msg">A Message, passed by reference, that represents the message to process. 
		/// The possible values are WM_KEYDOWN, WM_SYSKEYDOWN, WM_CHAR, and WM_SYSCHAR.</param>
		[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode=true), SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode=true)]
		public override bool PreProcessMessage(ref Message msg) {
			const int WM_KEYDOWN = 0x0100;

			// it's the only way to handle arrow keys in OnKeyDown
			if (msg.Msg == WM_KEYDOWN) {
				Keys key = (Keys) msg.WParam.ToInt32();
				switch (key) {
					case Keys.Left:
					case Keys.Up:
					case Keys.Right:
					case Keys.Down:
						OnKeyDown(new KeyEventArgs(key));
						// mark message as processed
						msg.Result = (IntPtr) 1;
						// When overriding PreProcessMessage, a control should return true to indicate that it has processed the message.
						return true;
				}
			}

			return base.PreProcessMessage(ref msg);
		}

		/// <summary>
		/// Handles key down events.
		/// </summary>
		/// <param name="e"></param>
		/// <returns>Returns true if the key is handled.</returns>
		protected bool HandleKeyDown(KeyEventArgs e) {
			bool handled = this._rootWidget.OnKeyDown(e);
			if (handled) {
				return handled;
			}

			// Alt key down
			if (e.Alt) {
				switch (e.KeyCode) {
					case Keys.C:
						World.Settings.ShowCrosshairs = !World.Settings.ShowCrosshairs;
						return true;
					case Keys.Add:
					case Keys.Oemplus:
					case Keys.Home:
					case Keys.NumPad7:
						this._drawArgs.WorldCamera.Fov -= Angle.FromDegrees(5);
						return true;
					case Keys.Subtract:
					case Keys.OemMinus:
					case Keys.End:
					case Keys.NumPad1:
						this._drawArgs.WorldCamera.Fov += Angle.FromDegrees(5);
						return true;
				}
			}
				// Control key down
			else if (e.Control) {}
				// Other and no control key
			else {
				switch (e.KeyCode) {
						// rotate left
					case Keys.A:
						Angle rotateClockwise = Angle.FromRadians(0.01f);
						this._drawArgs.WorldCamera.Heading += rotateClockwise;
						this._drawArgs.WorldCamera.RotationYawPitchRoll(Angle.Zero, Angle.Zero, rotateClockwise);
						return true;
						// rotate right
					case Keys.D:
						Angle rotateCounterclockwise = Angle.FromRadians(-0.01f);
						this._drawArgs.WorldCamera.Heading += rotateCounterclockwise;
						this._drawArgs.WorldCamera.RotationYawPitchRoll(Angle.Zero, Angle.Zero, rotateCounterclockwise);
						return true;
						// rotate up
					case Keys.W:
						this._drawArgs.WorldCamera.Tilt += Angle.FromDegrees(-1.0f);
						return true;
						// rotate down
					case Keys.S:
						this._drawArgs.WorldCamera.Tilt += Angle.FromDegrees(1.0f);
						return true;
						// pan left
					case Keys.Left:
					case Keys.H:
					case Keys.NumPad4:
						// TODO: pan n pixels
						Angle panLeft = Angle.FromRadians((float) -1*(this._drawArgs.WorldCamera.Altitude)*(1/(300*this.CurrentWorld.EquatorialRadius)));
						this._drawArgs.WorldCamera.RotationYawPitchRoll(panLeft, Angle.Zero, Angle.Zero);
						return true;
						// pan down
					case Keys.Down:
					case Keys.J:
					case Keys.NumPad2:
						Angle panDown = Angle.FromRadians((float) -1*(this._drawArgs.WorldCamera.Altitude)*(1/(300*this.CurrentWorld.EquatorialRadius)));
						this._drawArgs.WorldCamera.RotationYawPitchRoll(Angle.Zero, panDown, Angle.Zero);
						return true;
						// pan right
					case Keys.Right:
					case Keys.K:
					case Keys.NumPad6:
						Angle panRight = Angle.FromRadians((float) 1*(this._drawArgs.WorldCamera.Altitude)*(1/(300*this.CurrentWorld.EquatorialRadius)));
						this._drawArgs.WorldCamera.RotationYawPitchRoll(panRight, Angle.Zero, Angle.Zero);
						return true;
						// pan up
					case Keys.Up:
					case Keys.U:
					case Keys.NumPad8:
						// TODO: Pan n pixels
						Angle panUp = Angle.FromRadians((float) 1*(this._drawArgs.WorldCamera.Altitude)*(1/(300*this.CurrentWorld.EquatorialRadius)));
						this._drawArgs.WorldCamera.RotationYawPitchRoll(Angle.Zero, panUp, Angle.Zero);
						return true;
						// zoom in
					case Keys.Add:
					case Keys.Oemplus:
					case Keys.Home:
					case Keys.NumPad7:
						this._drawArgs.WorldCamera.ZoomStepped(World.Settings.CameraZoomStepKeyboard);
						return true;
						// zoom out
					case Keys.Subtract:
					case Keys.OemMinus:
					case Keys.End:
					case Keys.NumPad1:
						this._drawArgs.WorldCamera.ZoomStepped(-World.Settings.CameraZoomStepKeyboard);
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Handles key up events.
		/// </summary>
		/// <param name="e"></param>
		/// <returns>Returns true if the key is handled.</returns>
		protected bool HandleKeyUp(KeyEventArgs e) {
			bool handled = _rootWidget.OnKeyUp(e);
			if (handled) {
				e.Handled = handled;
				return handled;
			}
			// Alt key down
			if (e.Alt) {}
				// Control key down
			else if (e.Control) {
				switch (e.KeyCode) {
					case Keys.D:
						this._showDiagnosticInfo = !this._showDiagnosticInfo;
						return true;
					case Keys.W:
						_renderWireFrame = !_renderWireFrame;
						return true;
				}
			}
				// Other and no control key
			else {
				switch (e.KeyCode) {
					case Keys.Space:
					case Keys.Clear:
						this._drawArgs.WorldCamera.Reset();
						return true;
				}
			}
			return false;
		}

		protected override void OnMouseDown(MouseEventArgs e) {
			DrawArgs.LastMousePosition.X = e.X;
			DrawArgs.LastMousePosition.Y = e.Y;

			_mouseDownStartPosition.X = e.X;
			_mouseDownStartPosition.Y = e.Y;

			try {
				bool handled = false;
				handled = _rootWidget.OnMouseDown(e);
			}
			finally {
				if (e.Button == MouseButtons.Left) {
					DrawArgs.IsLeftMouseButtonDown = true;
				}

				if (e.Button == MouseButtons.Right) {
					DrawArgs.IsRightMouseButtonDown = true;
				}
				// Call the base class method so that registered delegates receive the event.
				base.Focus();
				base.OnMouseDown(e);
			}
		}

		protected override void OnMouseDoubleClick(MouseEventArgs e) {
			this._isDoubleClick = true;
			base.OnMouseDoubleClick(e);
		}

		protected override void OnMouseUp(MouseEventArgs e) {
			DrawArgs.LastMousePosition.X = e.X;
			DrawArgs.LastMousePosition.Y = e.Y;

			try {
				bool handled = false;

				handled = _rootWidget.OnMouseUp(e);

				if (!handled) {
					// Mouse must have been clicked outside our window and released on us, ignore
					if (_mouseDownStartPosition == Point.Empty) {
						return;
					}

					_mouseDownStartPosition = Point.Empty;

					if (_world == null) {
						return;
					}

					// ��������˫����Room��
					if (this._isDoubleClick) {
						this._isDoubleClick = false;
						if (e.Button == MouseButtons.Left) {
							this._drawArgs.WorldCamera.Zoom(World.Settings.CameraDoubleClickZoomFactor);
						}
						else if (e.Button == MouseButtons.Right) {
							this._drawArgs.WorldCamera.Zoom(-World.Settings.CameraDoubleClickZoomFactor);
						}
					}
					else {
						if (e.Button == MouseButtons.Left) {
							if (this._mouseDragging) {
								this._mouseDragging = false;
							}
							else {
								if (!_world.PerformSelectionAction(this._drawArgs)) {
									Angle targetLatitude;
									Angle targetLongitude;
									//Quaternion targetOrientation = new Quaternion();
									this._drawArgs.WorldCamera.PickingRayIntersection(DrawArgs.LastMousePosition.X, DrawArgs.LastMousePosition.Y, out targetLatitude, out targetLongitude);
									if (!Angle.IsNaN(targetLatitude)) {
										this._drawArgs.WorldCamera.PointGoto(targetLatitude, targetLongitude);
									}
								}
							}
						}
						else if (e.Button == MouseButtons.Right) {
							if (this._mouseDragging) {
								this._mouseDragging = false;
							}
							else {
								if (!_world.PerformSelectionAction(this._drawArgs)) {
									//nothing at the moment
								}
							}
						}
					}
				}
			}
			finally {
				if (e.Button == MouseButtons.Left) {
					DrawArgs.IsLeftMouseButtonDown = false;
				}

				if (e.Button == MouseButtons.Right) {
					DrawArgs.IsRightMouseButtonDown = false;
				}
				// Call the base class method so that registered delegates receive the event.
				base.OnMouseUp(e);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			// Default to default cursor
			DrawArgs.MouseCursor = CursorType.Arrow;

			try {
				bool handled = false;
				handled = _rootWidget.OnMouseMove(e);

				if (!handled) {
					int deltaX = e.X - DrawArgs.LastMousePosition.X;
					int deltaY = e.Y - DrawArgs.LastMousePosition.Y;
					float deltaXNormalized = (float) deltaX/_drawArgs.ScreenWidth;
					float deltaYNormalized = (float) deltaY/_drawArgs.ScreenHeight;

					if (_mouseDownStartPosition == Point.Empty) {
						return;
					}

					bool isMouseLeftButtonDown = ((int) e.Button & (int) MouseButtons.Left) != 0;
					bool isMouseRightButtonDown = ((int) e.Button & (int) MouseButtons.Right) != 0;
					if (isMouseLeftButtonDown || isMouseRightButtonDown) {
						int dx = this._mouseDownStartPosition.X - e.X;
						int dy = this._mouseDownStartPosition.Y - e.Y;
						int distanceSquared = dx*dx + dy*dy;
						if (distanceSquared > 3*3) {
							// Distance > 3 = drag
							this._mouseDragging = true;
						}
					}

					if (isMouseLeftButtonDown && !isMouseRightButtonDown) {
						// Left button (pan)
						// Store start lat/lon for drag
						Angle prevLat, prevLon;
						this._drawArgs.WorldCamera.PickingRayIntersection(DrawArgs.LastMousePosition.X, DrawArgs.LastMousePosition.Y, out prevLat, out prevLon);

						Angle curLat, curLon;
						this._drawArgs.WorldCamera.PickingRayIntersection(e.X, e.Y, out curLat, out curLon);

						if (World.Settings.CameraTwistLock) {
							if (Angle.IsNaN(curLat) || Angle.IsNaN(prevLat)) {
								// Old style pan
								Angle deltaLat = Angle.FromRadians((double) deltaY*(this._drawArgs.WorldCamera.Altitude)/(800*this.CurrentWorld.EquatorialRadius));
								Angle deltaLon = Angle.FromRadians((double) -deltaX*(this._drawArgs.WorldCamera.Altitude)/(800*this.CurrentWorld.EquatorialRadius));
								this._drawArgs.WorldCamera.Pan(deltaLat, deltaLon);
							}
							else {
								//Picking ray pan
								Angle lat = prevLat - curLat;
								Angle lon = prevLon - curLon;
								this._drawArgs.WorldCamera.Pan(lat, lon);
							}
						}
						else {
							double factor = (this._drawArgs.WorldCamera.Altitude)/(1500*this.CurrentWorld.EquatorialRadius);
							_drawArgs.WorldCamera.RotationYawPitchRoll(Angle.FromRadians(DrawArgs.LastMousePosition.X - e.X)*factor, Angle.FromRadians(e.Y - DrawArgs.LastMousePosition.Y)*factor, Angle.Zero);
						}
					}
					else if (!isMouseLeftButtonDown && isMouseRightButtonDown) {
						//Right mouse button

						// Heading
						Angle deltaEyeDirection = Angle.FromRadians(-deltaXNormalized*World.Settings.CameraRotationSpeed);
						this._drawArgs.WorldCamera.RotationYawPitchRoll(Angle.Zero, Angle.Zero, deltaEyeDirection);

						// tilt
						this._drawArgs.WorldCamera.Tilt += Angle.FromRadians(deltaYNormalized*World.Settings.CameraRotationSpeed);
					}
					else if (isMouseLeftButtonDown && isMouseRightButtonDown) {
						// Both buttons (zoom)
						if (Math.Abs(deltaYNormalized) > float.Epsilon) {
							this._drawArgs.WorldCamera.Zoom(-deltaYNormalized*World.Settings.CameraZoomAnalogFactor);
						}

						if (!World.Settings.CameraBankLock) {
							this._drawArgs.WorldCamera.Bank -= Angle.FromRadians(deltaXNormalized*World.Settings.CameraRotationSpeed);
						}
					}
				}
			}
			catch {}
			finally {
				this._drawArgs.WorldCamera.PickingRayIntersection(e.X, e.Y, out _cLat, out _cLon);

				DrawArgs.LastMousePosition.X = e.X;
				DrawArgs.LastMousePosition.Y = e.Y;
				base.OnMouseMove(e);
			}
		}

		protected void SaveScreenShot() {
			try {
				using (Surface backbuffer = _device.GetBackBuffer(0, 0, BackBufferType.Mono)) {
					SurfaceLoader.Save(_saveScreenShotFilePath, _saveScreenShotImageFileFormat, backbuffer);
				}
				_saveScreenShotFilePath = null;
			}
			catch (InvalidCallException caught) {
				MessageBox.Show(caught.Message, "Screenshot save failed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing) {
			if (disposing) {
				if (_workerThread != null && _workerThread.IsAlive) {
					_workerThreadRunning = false;
					_workerThread.Abort();
				}

				_fpsTimer.Stop();
				if (_world != null) {
					_world.Dispose();
					_world = null;
				}
				if (this._drawArgs != null) {
					this._drawArgs.Dispose();
					this._drawArgs = null;
				}

				_device.Dispose();
				// ��������
				//settings.Save();
			}

			base.Dispose(disposing);
			GC.SuppressFinalize(this);
		}

		private void m_Device3d_DeviceResizing(object sender, CancelEventArgs e) {
			if (this.Size.Width == 0 || this.Size.Height == 0) {
				e.Cancel = true;
				return;
			}

			this._drawArgs.ScreenHeight = this.Height;
			this._drawArgs.ScreenWidth = this.Width;
		}

		/// <summary>
		/// Returns true if executing in Design mode (inside IDE)
		/// </summary>
		/// <returns></returns>
		private static bool IsInDesignMode() {
			return Application.ExecutablePath.ToUpper(CultureInfo.InvariantCulture).EndsWith("DEVENV.EXE");
		}

		private void InitializeGraphics() {
			// Set up our presentation parameters
			_presentParams = new PresentParameters();

			_presentParams.Windowed = true;
			_presentParams.SwapEffect = SwapEffect.Discard;
			_presentParams.AutoDepthStencilFormat = DepthFormat.D16;
			_presentParams.EnableAutoDepthStencil = true;

			if (!World.Settings.VSync) {
				// Disable wait for vertical retrace (higher frame rate at the expense of tearing)
				_presentParams.PresentationInterval = PresentInterval.Immediate;
			}

			int adapterOrdinal = 0;
			try {
				// Store the default adapter
				adapterOrdinal = Manager.Adapters.Default.Adapter;
			}
			catch {
				// User probably needs to upgrade DirectX or install a 3D capable graphics adapter
				throw new NotAvailableException();
			}

			DeviceType dType = DeviceType.Hardware;

			foreach (AdapterInformation ai in Manager.Adapters) {
				if (ai.Information.Description.IndexOf("NVPerfHUD") >= 0) {
					adapterOrdinal = ai.Adapter;
					dType = DeviceType.Reference;
				}
			}
			CreateFlags flags = CreateFlags.SoftwareVertexProcessing;

			// Check to see if we can use a pure hardware m_Device3d
			Caps caps = Manager.GetDeviceCaps(adapterOrdinal, DeviceType.Hardware);

			// Do we support hardware vertex processing?
			if (caps.DeviceCaps.SupportsHardwareTransformAndLight) {
				//	// Replace the software vertex processing
				flags = CreateFlags.HardwareVertexProcessing;
			}

			// Use multi-threading for now - TODO: See if the code can be changed such that this isn't necessary (Texture Loading for example)
			flags |= CreateFlags.MultiThreaded | CreateFlags.FpuPreserve;

			try {
				// Create our m_Device3d
				_device = new Device(adapterOrdinal, dType, this, flags, _presentParams);
			}
			catch (DirectXException) {
				throw new NotSupportedException("Unable to create the Direct3D m_Device3d.");
			}

			// Hook the m_Device3d reset event
			_device.DeviceReset += new EventHandler(OnDeviceReset);
			_device.DeviceResizing += new CancelEventHandler(m_Device3d_DeviceResizing);
			OnDeviceReset(_device, null);
		}

		private void OnDeviceReset(object sender, EventArgs e) {
			// Can we use anisotropic texture minify filter?
			if (_device.DeviceCaps.TextureFilterCaps.SupportsMinifyAnisotropic) {
				_device.SamplerState[0].MinFilter = TextureFilter.Anisotropic;
			}
			else if (_device.DeviceCaps.TextureFilterCaps.SupportsMinifyLinear) {
				_device.SamplerState[0].MinFilter = TextureFilter.Linear;
			}

			// What about magnify filter?
			if (_device.DeviceCaps.TextureFilterCaps.SupportsMagnifyAnisotropic) {
				_device.SamplerState[0].MagFilter = TextureFilter.Anisotropic;
			}
			else if (_device.DeviceCaps.TextureFilterCaps.SupportsMagnifyLinear) {
				_device.SamplerState[0].MagFilter = TextureFilter.Linear;
			}

			_device.SamplerState[0].AddressU = TextureAddress.Clamp;
			_device.SamplerState[0].AddressV = TextureAddress.Clamp;

			_device.RenderState.Clipping = true;
			_device.RenderState.CullMode = Cull.Clockwise;
			_device.RenderState.Lighting = false;
			_device.RenderState.Ambient = Color.FromArgb(0x40, 0x40, 0x40);

			_device.RenderState.ZBufferEnable = true;
			_device.RenderState.AlphaBlendEnable = true;
			_device.RenderState.SourceBlend = Blend.SourceAlpha;
			_device.RenderState.DestinationBlend = Blend.InvSourceAlpha;
		}

		/// <summary>
		/// Background worker thread loop (updates UI)
		/// </summary>
		private void WorkerThreadFunc() {
			const int refreshIntervalMs = 150; // Max 6 updates per seconds
			while (_workerThreadRunning) {
				try {
					if (World.Settings.UseBelowNormalPriorityUpdateThread
					    && _workerThread.Priority == ThreadPriority.Normal) {
						_workerThread.Priority = ThreadPriority.BelowNormal;
					}
					else if (!World.Settings.UseBelowNormalPriorityUpdateThread && _workerThread.Priority == ThreadPriority.BelowNormal) {
						_workerThread.Priority = ThreadPriority.Normal;
					}

					long startTicks = 0;
					PerformanceTimer.QueryPerformanceCounter(ref startTicks);

					_world.Update(this._drawArgs);

					long endTicks = 0;
					PerformanceTimer.QueryPerformanceCounter(ref endTicks);
					float elapsedMilliSeconds = 1000*(float) (endTicks - startTicks)/PerformanceTimer.TicksPerSecond;
					float remaining = refreshIntervalMs - elapsedMilliSeconds;
					if (remaining > 0) {
						Thread.Sleep((int) remaining);
					}
				}
				catch (Exception ex) {
					Log.Write(ex);
				}
			}
		}

		public void SetDisplayMessages(IList messages) {
			_world.OnScreenMessages = messages;
		}

		public void SetLatLonGridShow(bool show) {
			World.Settings.ShowLatLonLines = show;
		}

		public void SetLayers(IList layers) {
			if (layers != null) {
				foreach (LayerDescriptor ld in layers) {
					this.CurrentWorld.SetLayerOpacity(ld.Category, ld.Name, (float) ld.Opacity*0.01f);
				}
			}
		}

		public void SetVerticalExaggeration(double exageration) {
			World.Settings.VerticalExaggeration = (float) exageration;
		}

		public void SetViewDirection(String type, double horiz, double vert, double elev) {
			this._drawArgs.WorldCamera.SetPosition(this._drawArgs.WorldCamera.Latitude.Degrees, this._drawArgs.WorldCamera.Longitude.Degrees, horiz, this._drawArgs.WorldCamera.Altitude, vert);
		}

		public void SetViewPosition(double degreesLatitude, double degreesLongitude, double metersElevation) {
			this._drawArgs.WorldCamera.SetPosition(degreesLatitude, degreesLongitude, this._drawArgs.WorldCamera.Heading.Degrees, metersElevation, this._drawArgs.WorldCamera.Tilt.Degrees);
		}

		public void SetWmsImage(WmsDescriptor imageA, WmsDescriptor imageB, double alpha) {
			// TODO:  Add WorldWindow.SetWmsImage implementation
			if (imageA != null) {
				Console.Write(imageA.Url.ToString() + " ");
				Console.WriteLine(imageA.Opacity);
			}
			if (imageB != null) {
				Console.Write(imageB.Url.ToString() + " ");
				Console.Write(imageB.Opacity);
				Console.Write(" alpha = ");
				Console.WriteLine(alpha);
			}
		}

		private void FpsTimer_Elapsed(object sender, ElapsedEventArgs e) {
			if (_fpsUpdate) {
				return;
			}

			_fpsUpdate = true;

			try {
				if (World.Settings.ShowFpsGraph) {
					if (!_fpsGraph.Visible) {
						_fpsGraph.Visible = true;
					}

					if (_frameTimes.Count > World.Settings.FpsFrameCount) {
						_frameTimes.RemoveRange(0, _frameTimes.Count - World.Settings.FpsFrameCount);
					}

					_fpsGraph.Size = new Size((int) (Width*.5), (int) (Height*.1));
					_fpsGraph.Location = new Point((int) (Width*.35), (int) (Height*.895));
					_fpsGraph.Values = (float[]) _frameTimes.ToArray(typeof (float));
				}
				else {
					if (_fpsGraph.Visible) {
						_fpsGraph.Visible = false;
					}
				}
			}
			catch (Exception ex) {
				Log.Write(ex);
			}

			_fpsUpdate = false;
		}
	}
}