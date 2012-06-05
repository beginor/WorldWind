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
	/// 绘图设备参数类，WW的核心绘图类
	/// </summary>
	public class DrawArgs : IDisposable {
		/// <summary>
		/// 对Camera的静态引用
		/// </summary>
		public static CameraBase Camera = null;
		/// <summary>
		/// 当前帧开始呈现时的绝对时间点
		/// (Absolute time of current frame render start (ticks))
		/// </summary>
		public static long CurrentFrameStartTicks;
		/// <summary>
		/// 一个静态的下载队列
		/// </summary>
		public static DownloadQueue DownloadQueue = new DownloadQueue();
		/// <summary>
		/// 是否鼠标左键按下
		/// </summary>
		public static bool IsLeftMouseButtonDown = false;
		/// 是否鼠标右键按下
		public static bool IsRightMouseButtonDown = false;
		/// <summary>
		/// 最后的光标类型
		/// </summary>
		private static CursorType lastCursor;
		/// <summary>
		/// 渲染上一帧所用的秒数
		/// </summary>
		public static float LastFrameSecondsElapsed;
		/// <summary>
		/// 光标的最后位置
		/// </summary>
		public static Point LastMousePosition;
		/// <summary>
		/// 光标类型
		/// </summary>
		private static CursorType mouseCursor;
		/// <summary>
		/// 最顶层的部件
		/// </summary>
		public static RootWidget RootWidget = null;
		/// <summary>
		/// 对绘图设备的静态引用，表示当前的绘图设备，通常是D3D的显示设备
		/// </summary>
		public static Device StaticDevice = null;
		/// <summary>
		/// 对呈现WW的控件的静态引用
		/// </summary>
		public static Control StaticParentControl = null;
		/// <summary>
		/// 当前的光标状态
		/// </summary>
		public Point CurrentMousePosition;
		/// <summary>
		/// 默认的绘图字体
		/// </summary>
		public Font DefaultDrawingFont;
		/// <summary>
		/// 当前绘图设备(D3D Device)
		/// </summary>
		public Device Device;
		/// <summary>
		/// 已经载入的字体列表
		/// </summary>
		private Hashtable _fontList = new Hashtable();
		/// <summary>
		/// 是否正在绘图
		/// </summary>
		private bool _isPainting;
		/// <summary>
		/// 当前载入的世界
		/// </summary>
		private World _CurrentWorld = null;
		/// <summary>
		/// 引用设备？做什么用？
		/// </summary>
		//private Device m_Device3dReference = null;
		//private Control m_ReferenceForm;
		/// <summary>
		/// 当前世界的摄像头(参考相关D3D知识)
		/// </summary>
		private CameraBase _worldCamera;
		/// <summary>
		/// 测距时的光标
		/// </summary>
		private Cursor _measureCursor;
		/// <summary>
		/// 当前已经绘制出块图块（Tile）的数目
		/// </summary>
		public int NumberTilesDrawn;
		/// <summary>
		/// 当前已经绘制出边的数目
		/// </summary>
		public int NumBoundariesDrawn;
		/// <summary>
		/// 当前已经绘制出的边上的点数
		/// </summary>
		public int NumBoundaryPointsRendered;
		/// <summary>
		/// 边上的点的总数
		/// </summary>
		public int NumBoundaryPointsTotal;
		/// <summary>
		/// 呈现WW的WinForm控件
		/// </summary>
		public Control ParentControl;
		/// <summary>
		/// 是否呈现网状帧
		/// </summary>
		public bool RenderWireFrame = false;
		/// <summary>
		/// 是否重新绘制
		/// </summary>
		private bool _repaint = true;
		/// <summary>
		/// 屏幕高度
		/// </summary>
		public int ScreenHeight;
		/// <summary>
		/// 屏幕宽度
		/// </summary>
		public int ScreenWidth;
		/// <summary>
		/// 当前帧已经加载的贴图的数量
		/// </summary>
		public int TexturesLoadedThisFrame = 0;
		/// <summary>
		/// 屏幕左上角的文字
		/// </summary>
		public string UpperLeftCornerText = string.Empty;

		/// <summary>
		/// 初始化一个新的<see cref= "T:WorldWind.DrawArgs"/>实例
		/// </summary>
		/// <param name="device">一个已经初始化好的D3D Device</param>
		/// <param name="parentForm">要呈现的WW的控件</param>
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
		/// 获取或设置Camera
		/// </summary>
		public CameraBase WorldCamera {
			get { return _worldCamera; }
			set {
				_worldCamera = value;
				Camera = value;
			}
		}
		/// <summary>
		/// 获取或设置当前的世界
		/// </summary>
		public World CurrentWorld {
			get { return _CurrentWorld; }
			set { _CurrentWorld = value; }
		}
		/// <summary>
		/// 获取或设置活动光标
		/// </summary>
		public static CursorType MouseCursor {
			get { return mouseCursor; }
			set { mouseCursor = value; }
		}
		/// <summary>
		/// 获取从最后一帧开始呈现到现在的秒数。
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
		/// 获取一个值，表示当前是否正在渲染
		/// </summary>
		public bool IsPainting {
			get { return this._isPainting; }
		}
		/// <summary>
		/// 获取一个值，表示是否需要重新绘制WW窗口
		/// </summary>
		public bool Repaint {
			get { return this._repaint; }
			set { this._repaint = value; }
		}

		#region IDisposable Members
		/// <summary>
		/// dispose回收函数
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
		/// 通知D3D设备，即将开始进行渲染
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
		/// 即将停止渲染
		/// </summary>
		public void EndRender() {
			Debug.Assert(_isPainting);
			this._isPainting = false;
		}

		/// <summary>
		/// 显示渲染好的图像,必须在EndRender之后调用
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
		/// 创建字体
		/// </summary>
		public Font CreateFont(string familyName, float emSize) {
			return CreateFont(familyName, emSize, FontStyle.Regular);
		}

		/// <summary>
		/// 创建字体
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
		/// 创建字体
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
		/// 更新呈现WW控件的光标
		/// </summary>
		/// <param name="parent">呈现WW的控件</param>
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