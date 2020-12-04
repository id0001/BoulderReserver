
using System;
using System.Collections.Generic;


namespace BoulderReserver
{
	public enum ExitCode : int
	{
		Success = 0,
		TimeslotUnavailable = 1,
		InvalidLogin = 2,
		InvalidArguments =3,
		UnknownError = 4,
		GroupAmountNotAvailable =5
	}
}
