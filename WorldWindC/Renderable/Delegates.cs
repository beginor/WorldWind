using System.IO;

namespace WorldWind.Renderable {
	/// <summary>
	/// ������Iconʱ���¼�
	/// </summary>
	public delegate void MouseEnterHandler(string strLayerName, string strIconName);

	/// <summary>
	/// ����뿪Iconʱ���¼�
	/// </summary>
	public delegate void MouseLeaveHandler(string strLayerName, string strIconName);

	/// <summary>
	/// �������ʱ���õĺ���ί��
	/// </summary>
	public delegate void WWPDownloadCompleteHandler(Stream sData);
}