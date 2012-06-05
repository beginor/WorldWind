using System;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace WorldWind.Camera {
	/// <summary>
	/// Normal camera with MomentumCamera. (perhaps merge with the normal camera)
	/// </summary>
	public class MomentumCamera : WorldCamera {
		protected Angle _latitudeMomentum;
		protected Angle _longitudeMomentum;
		protected Angle _headingMomentum;
		//protected Angle _bankMomentum;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Camera.MomentumCamera"/> class.
		/// </summary>
		/// <param name="targetPosition"></param>
		/// <param name="radius"></param>
		public MomentumCamera(Vector3 targetPosition, double radius) : base(targetPosition, radius) {
			this._targetOrientation = m_Orientation;
			this._targetDistance = this._distance;
			this._targetAltitude = this._altitude;
			this._targetTilt = this._tilt;
		}

		public bool HasMomentum {
			get {
				return World.Settings.cameraHasMomentum;
			}
			set {
				World.Settings.cameraHasMomentum = value;
				this._latitudeMomentum.Radians = 0;
				this._longitudeMomentum.Radians = 0;
				this._headingMomentum.Radians = 0;
			}
		}

		public override void RotationYawPitchRoll(Angle yaw, Angle pitch, Angle roll) {
			if (World.Settings.cameraHasMomentum) {
				_latitudeMomentum += pitch/100;
				_longitudeMomentum += yaw/100;
				_headingMomentum += roll/100;
			}

			this._targetOrientation = Quaternion4d.EulerToQuaternion(yaw.Radians, pitch.Radians, roll.Radians)*_targetOrientation;
			Point3d v = Quaternion4d.QuaternionToEuler(_targetOrientation);
			if (!double.IsNaN(v.Y)) {
				this._targetLatitude.Radians = v.Y;
				this._targetLongitude.Radians = v.X;
				if (!World.Settings.cameraTwistLock) {
					_targetHeading.Radians = v.Z;
				}
			}

			base.RotationYawPitchRoll(yaw, pitch, roll);
		}

		/// <summary>
		/// Pan the camera using delta values
		/// </summary>
		/// <param name="lat">Latitude offset</param>
		/// <param name="lon">Longitude offset</param>
		public override void Pan(Angle lat, Angle lon) {
			// by zzm
			//CheckLatLon(ref lat, ref lon);

			if (World.Settings.cameraHasMomentum) {
				_latitudeMomentum += lat/100;
				_longitudeMomentum += lon/100;
			}

			if (Angle.IsNaN(lat)) {
				lat = this._targetLatitude;
			}
			if (Angle.IsNaN(lon)) {
				lon = this._targetLongitude;
			}
			lat += _targetLatitude;
			lon += _targetLongitude;
			//CheckLatLon(ref lat, ref lon); // by zzm
			if (Math.Abs(lat.Radians)
			    > Math.PI/2 - 1e-3) {
				lat.Radians = Math.Sign(lat.Radians)*(Math.PI/2 - 1e-3);
			}

			this._targetOrientation = Quaternion4d.EulerToQuaternion(lon.Radians, lat.Radians, _targetHeading.Radians);

			Point3d v = Quaternion4d.QuaternionToEuler(this._targetOrientation);
			if (!double.IsNaN(v.Y)) {
				_targetLatitude.Radians = v.Y;
				_targetLongitude.Radians = v.X;

				_targetHeading.Radians = v.Z;

				if (!World.Settings.cameraSmooth) {
					_latitude = _targetLatitude;
					_longitude = _targetLongitude;
					_heading = _targetHeading;
					m_Orientation = _targetOrientation;
				}
			}
		}

		public override void Update(Device device) {
			if (World.Settings.cameraHasMomentum) {
				base.RotationYawPitchRoll(_longitudeMomentum, _latitudeMomentum, _headingMomentum);
			}

			base.Update(device);
		}

		public override void SetPosition(double lat, double lon, double heading, double altitude, double tilt, double bank) {
			_latitudeMomentum.Radians = 0;
			_longitudeMomentum.Radians = 0;
			_headingMomentum.Radians = 0;

			base.SetPosition(lat, lon, heading, altitude, tilt, bank);
		}

		public override string ToString() {
			string res = base.ToString() + string.Format("\nMomentum: {0}, {1}, {2}", _latitudeMomentum, _longitudeMomentum, _headingMomentum);
			return res;
		}
	}
}