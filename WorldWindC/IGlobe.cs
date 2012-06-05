using System;
using System.Collections;
using WorldWind.Net.Wms;

namespace WorldWind {
	/// <summary>
	/// ÇòÌå½Ó¿Ú
	/// </summary>
	public interface IGlobe {
		void SetDisplayMessages(IList messages);
		void SetLatLonGridShow(bool show);
		void SetLayers(IList layers);
		void SetVerticalExaggeration(double exageration);
		void SetViewDirection(String type, double horiz, double vert, double elev);
		void SetViewPosition(double degreesLatitude, double degreesLongitude, double metersElevation);
		void SetWmsImage(WmsDescriptor imageA, WmsDescriptor imageB, double alpha);
	}

}