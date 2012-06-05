using System;
using System.Collections.Generic;
using System.Text;

namespace WorldWind.Configuration {
	// types of location supported
	public enum LocationType {
		User = 0, // regular, roaming user - settings will move
		UserLocal, // local user - settings will be stored on local machine
		UserCommon, // location common to all users
		Application, // application - settings will be saved in appdir
	}
}
