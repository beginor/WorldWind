using System;
using System.Collections.Generic;
using System.Text;

namespace WorldWind.Renderable {

	/// <summary>
	/// The render priority determines in what order objects are rendered.
	/// Objects with higher priority number are rendered over lower priority objects.
	/// </summary>
	public enum RenderPriority {
		SurfaceImages = 0,
		TerrainMappedImages = 100,
		AtmosphericImages = 200,
		LinePaths = 300,
		Icons = 400,
		Placenames = 500,
		Custom = 600
	}

}
