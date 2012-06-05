using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX.Direct3D;
using WorldWind.Camera;
using WorldWind.Net;
using WorldWind.Utilities;
using WorldWind.Widgets;
using Font=Microsoft.DirectX.Direct3D.Font;

namespace WorldWind {
	/// <summary>
	/// ��ͼ�豸�����࣬WW�ĺ��Ļ�ͼ��
	/// </summary>
	public class DrawArgs : IDisposable {
		/// <summary>
		/// ��Camera�ľ�̬����
		/// </summary>
		public static CameraBase Camera = null;
		/// <summary>
		/// ��ǰ֡��ʼ����ʱ�ľ���ʱ���
		/// (Absolute time of current frame render start (ticks))
		/// </summary>
		public static long CurrentFrameStartTicks;
		/// <summary>
		/// һ����̬�����ض���
		/// </summary>
		public static DownloadQueue DownloadQueue = new DownloadQueue();
		/// <summary>
		/// �Ƿ�����������
		/// </summary>
		public static bool IsLeftMouseButtonDown = false;
		/// �Ƿ�����Ҽ�����
		public static bool IsRightMouseButtonDown = false;
		/// <summary>
		/// ���Ĺ������
		/// </summary>
		private static CursorType lastCursor;
		/// <summary>
		/// ��Ⱦ��һ֡���õ�����
		/// </summary>
		public static float LastFrameSecondsElapsed;
		/// <summary>
		/// �������λ��
		/// </summary>
		public static Point LastMousePosition;
		/// <summary>
		/// �������
		/// </summary>
		private static CursorType mouseCursor;
		/// <summary>
		/// ���Ĳ���
		/// </summary>
		public static RootWidget RootWidget = null;
		/// <summary>
		/// �Ի�ͼ�豸�ľ�̬���ã���ʾ��ǰ�Ļ�ͼ�豸��ͨ����D3D����ʾ�豸
		/// </summary>
		public static Device StaticDevice = null;
		/// <summary>
		/// �Գ���WW�Ŀؼ��ľ�̬����
		/// </summary>
		public static Control StaticParentControl = null;
		/// <summary>
		/// ��ǰ�Ĺ��״̬
		/// </summary>
		public Point CurrentMousePosition;
		/// <summary>
		/// Ĭ�ϵĻ�ͼ����
		/// </summary>
		public Font DefaultDrawingFont;
		/// <summary>
		/// ��ǰ��ͼ�豸(D3D Device)
		/// </summary>
		public Device Device;
		/// <summary>
		/// �Ѿ�����������б�
		/// </summary>
		private Hashtable _fontList = new Hashtable();
		/// <summary>
		/// �Ƿ����ڻ�ͼ
		/// </summary>
		private bool _isPainting;
		/// <summary>
		/// ��ǰ���������
		/// </summary>
		private World _CurrentWorld = null;
		/// <summary>
		/// �����豸����ʲô�ã�
		/// </summary>
		//private Device m_Device3dReference = null;
		//private Control m_ReferenceForm;
		/// <summary>
		/// ��ǰ���������ͷ(�ο����D3D֪ʶ)
		/// </summary>
		private CameraBase _worldCamera;
		/// <summary>
		/// ���ʱ�Ĺ��
		/// </summary>
		private Cursor _measureCursor;
		/// <summary>
		/// ��ǰ�Ѿ����Ƴ���ͼ�飨Tile������Ŀ
		/// </summary>
		public int NumberTilesDrawn;
		/// <summary>
		/// ��ǰ�Ѿ����Ƴ��ߵ���Ŀ
		/// </summary>
		public int NumBoundariesDrawn;
		/// <summary>
		/// ��ǰ�Ѿ����Ƴ��ı��ϵĵ���
		/// </summary>
		public int NumBoundaryPointsRendered;
		/// <summary>
		/// ���ϵĵ������
		/// </summary>
		public int NumBoundaryPointsTotal;
		/// <summary>
		/// ����WW��WinForm�ؼ�
		/// </summary>
		public Control ParentControl;
		/// <summary>
		/// �Ƿ������״֡
		/// </summary>
		public bool RenderWireFrame = false;
		/// <summary>
		/// �Ƿ����»���
		/// </summary>
		private bool _repaint = true;
		/// <summary>
		/// ��Ļ�߶�
		/// </summary>
		public int ScreenHeight;
		/// <summary>
		/// ��Ļ���
		/// </summary>
		public int ScreenWidth;
		/// <summary>
		/// ��ǰ֡�Ѿ����ص���ͼ������
		/// </summary>
		public int TexturesLoadedThisFrame = 0;
		/// <summary>
		/// ��Ļ���Ͻǵ�����
		/// </summary>
		public string UpperLeftCornerText = string.Empty;

		/// <summary>
		/// ��ʼ��һ���µ�<see cref= "T:WorldWind.DrawArgs"/>ʵ��
		/// </summary>
		/// <param name="device">һ���Ѿ���ʼ���õ�D3D Device</param>
		/// <param name="parentForm">Ҫ���ֵ�WW�Ŀؼ�</param>
		public DrawArgs(Device device, Control parentForm) {
			ParentControl = parentForm;
			StaticParentControl = parentForm;
			StaticDevice = device;
			Device = device;
			DefaultDrawingFont = CreateFont(World.Settings.DefaultFontName, World.Settings.DefaultFontSize);
			if (DefaultDrawingFont == null) {
				DefaultDrawingFont = CreateFont("", World.Settings.DefaultFontSize);
			}
		}
		/// <summary>
		/// ��ȡ������Camera
		/// </summary>
		public CameraBase WorldCamera {
			get { return _worldCamera; }
			set {
				_worldCamera = value;
				Camera = value;
			}
		}
		/// <summary>
		/// ��ȡ�����õ�ǰ������
		/// </summary>
		public World CurrentWorld {
			get { return _CurrentWorld; }
			set { _CurrentWorld = value; }
		}
		/// <summary>
		/// ��ȡ�����û���
		/// </summary>
		public static CursorType MouseCursor {
			get { return mouseCursor; }
			set { mouseCursor = value; }
		}
		/// <summary>
		/// ��ȡ�����һ֡��ʼ���ֵ����ڵ�������
		/// </summary>
		public static float SecondsSinceLastFrame {
			get {
				long curTicks = 0;
				PerformanceTimer.QueryPerformanceCounter(ref curTicks);
				float elapsedSeconds = (curTicks - CurrentFrameStartTicks)/(float) PerformanceTimer.TicksPerSecond;
				return elapsedSeconds;
			}
		}
		/// <summary>
		/// ��ȡһ��ֵ����ʾ��ǰ�Ƿ�������Ⱦ
		/// </summary>
		public bool IsPainting {
			get { return this._isPainting; }
		}
		/// <summary>
		/// ��ȡһ��ֵ����ʾ�Ƿ���Ҫ���»���WW����
		/// </summary>
		public bool Repaint {
			get { return this._repaint; }
			set { this._repaint = value; }
		}

		#region IDisposable Members
		/// <summary>
		/// dispose���պ���
		/// </summary>
		public void Dispose() {
			foreach (IDisposable font in _fontList.Values) {
				if (font != null) {
					font.Dispose();
				}
			}
			_fontList.Clear();
			if (_measureCursor != null) {
				_measureCursor.Dispose();
				_measureCursor = null;
			}
			if (DownloadQueue != null) {
				DownloadQueue.Dispose();
				DownloadQueue = null;
			}
			GC.SuppressFinalize(this);
		}
		#endregion
		/// <summary>
		/// ֪ͨD3D�豸��������ʼ������Ⱦ
		/// </summary>
		public void BeginRender() {
			this.NumberTilesDrawn = 0;
			this.TexturesLoadedThisFrame = 0;
			this.UpperLeftCornerText = "";
			this.NumBoundaryPointsRendered = 0;
			this.NumBoundaryPointsTotal = 0;
			this.NumBoundariesDrawn = 0;
			this._isPainting = true;
		}
		/// <summary>
		/// ����ֹͣ��Ⱦ
		/// </summary>
		public void EndRender() {
			Debug.Assert(_isPainting);
			this._isPainting = false;
		}

		/// <summary>
		/// ��ʾ��Ⱦ�õ�ͼ��,������EndRender֮�����
		/// </summary>
		public void Present() {
			// Calculate frame time
			long previousFrameStartTicks = CurrentFrameStartTicks;
			PerformanceTimer.QueryPerformanceCounter(ref CurrentFrameStartTicks);
			LastFrameSecondsElapsed = (CurrentFrameStartTicks - previousFrameStartTicks)/(float) PerformanceTimer.TicksPerSecond;
			// Display the render
			Device.Present();
		}

		/// <summary>
		/// ��������
		/// </summary>
		public Font CreateFont(string familyName, float emSize) {
			return CreateFont(familyName, emSize, FontStyle.Regular);
		}

		/// <summary>
		/// ��������
		/// </summary>
		public Font CreateFont(string familyName, float emSize, FontStyle style) {
			try {
				FontDescription description = new FontDescription();
				description.FaceName = familyName;
				description.Height = (int) (1.9*emSize);

				if (style == FontStyle.Regular) {
					return CreateFont(description);
				}
				if ((style & FontStyle.Italic) != 0) {
					description.IsItalic = true;
				}
				if ((style & FontStyle.Bold) != 0) {
					description.Weight = FontWeight.Heavy;
				}

				return CreateFont(description);
			}
			catch {
				Log.Write("FONT", string.Format("Unable to load '{0}' {2} ({1}em)", familyName, emSize, style));
				return DefaultDrawingFont;
			}
		}

		/// <summary>
		/// ��������
		/// </summary>
		public Font CreateFont(FontDescription description) {
			try {
				if (World.Settings.AntiAliasedText) {
					description.Quality = FontQuality.ClearTypeNatural;
				}
				else {
					description.Quality = FontQuality.Default;
				}

				string hash = description.ToString(); //.GetHashCode(); returned hash codes are not correct

				Font font = _fontList[hash] as Font;
				if (font != null) {
					return font;
				}

				font = new Font(this.Device, description);
				_fontList.Add(hash, font);
				return font;
			}
			catch {
				Log.Write("FONT", string.Format("Unable to load '{0}' (Height: {1})", description.FaceName, description.Height));
				return DefaultDrawingFont;
			}
		}
		/// <summary>
		/// ���³���WW�ؼ��Ĺ��
		/// </summary>
		/// <param name="parent">����WW�Ŀؼ�</param>
		public void UpdateMouseCursor(Control parent) {
			if (lastCursor == mouseCursor) {
				return;
			}

			switch (mouseCursor) {
				case CursorType.Hand:
					parent.Cursor = Cursors.Hand;
					break;
				case CursorType.Cross:
					parent.Cursor = Cursors.Cross;
					break;
				case CursorType.Measure:
					if (_measureCursor == null) {
						_measureCursor = ImageHelper.LoadCursor("measure.cur");
					}
					parent.Cursor = _measureCursor;
					break;
				case CursorType.SizeWE:
					parent.Cursor = Cursors.SizeWE;
					break;
				case CursorType.SizeNS:
					parent.Cursor = Cursors.SizeNS;
					break;
				case CursorType.SizeNESW:
					parent.Cursor = Cursors.SizeNESW;
					break;
				case CursorType.SizeNWSE:
					parent.Cursor = Cursors.SizeNWSE;
					break;
				default:
					parent.Cursor = Cursors.Arrow;
					break;
			}
			lastCursor = mouseCursor;
		}
	}
}