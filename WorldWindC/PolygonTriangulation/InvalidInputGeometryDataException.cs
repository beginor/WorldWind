using System;

namespace WorldWind.PolygonTriangulation {
	public class InvalidInputGeometryDataException : ApplicationException {
		public InvalidInputGeometryDataException() : base() {}
		public InvalidInputGeometryDataException(string msg) : base(msg) {}
		public InvalidInputGeometryDataException(string msg, Exception inner) : base(msg, inner) {}
	}
}