using System;
using System.Runtime.Serialization;

namespace BoulderReserver
{
	public class GroupAmountNotAvailableException : Exception
	{
		public GroupAmountNotAvailableException()
		{
		}

		public GroupAmountNotAvailableException(string message) : base(message)
		{
		}

		public GroupAmountNotAvailableException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected GroupAmountNotAvailableException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}
