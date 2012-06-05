using System.IO;

namespace WorldWind.Renderable {
	/// <summary>
	/// 鼠标进入Icon时的事件
	/// </summary>
	public delegate void MouseEnterHandler(string strLayerName, string strIconName);

	/// <summary>
	/// 鼠标离开Icon时的事件
	/// </summary>
	public delegate void MouseLeaveHandler(string strLayerName, string strIconName);

	/// <summary>
	/// 下载完成时调用的函数委托
	/// </summary>
	public delegate void WWPDownloadCompleteHandler(Stream sData);
}