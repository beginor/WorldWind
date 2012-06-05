namespace WorldWind.Widgets {
	/// <summary>
	/// Collection of IWidgets
	/// </summary>
	public interface IWidgetCollection {
		#region Methods
		void BringToFront(int index);
		void BringToFront(IWidget widget);
		void Add(IWidget widget);
		void Clear();
		void Insert(IWidget widget, int index);
		IWidget RemoveAt(int index);
		#endregion

		#region Properties
		int Count { get; }
		#endregion

		#region Indexers
		IWidget this[int index] { get; set; }
		#endregion
	}
}