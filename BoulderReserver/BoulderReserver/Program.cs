using BoulderReserver.Properties;
using DocoptNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BoulderReserver
{
	public class Program
	{
		public async static Task<int> Main(string[] args)
		{
			var usage = Resources.docopt;

			var arguments = new Docopt().Apply(usage, args, version: "Boulder Reserver 1.0", exit: true);

			string username = arguments["--username"].ToString();
			string password = arguments["--password"].ToString();

			// Validate <gymnr> argument
			if (!arguments["--gym"].IsInt)
			{
				Console.Error.WriteLine("<gymnr> must be an integer.");
				return (int)ExitCode.InvalidArguments;
			}

			int gym = arguments["--gym"].AsInt;

			// Validate <timeslot> argument.
			if (!Regex.IsMatch(arguments["--timeslot"].ToString(), @".+ \d{2}:\d{2}"))
			{
				Console.Error.WriteLine("<timeslot> was not in the correct format.");
				return (int)ExitCode.InvalidArguments;
			}

			var (day, time) = ParseTimeslot(arguments["--timeslot"].ToString());

			// Amount
			if(!arguments["--amount"].IsInt)
			{
				Console.Error.WriteLine("<amount> must be an integer.");
				return (int)ExitCode.InvalidArguments;
			}

			int amount = arguments["--amount"].AsInt;

			try
			{
				using var client = new ReservationClient("http://bouldertour.nl", gym, username, password);
				await client.LoginAsync();

				var timeslots = await client.GetTimeSlotsAsync(amount);

				// Select timeslot if available.
				var selectedTimeslot = timeslots.FirstOrDefault(e => e.Key.DayOfWeek == day && e.Key.TimeOfDay == time);
				if (selectedTimeslot.Equals(default(KeyValuePair<DateTime, string>)))
				{
					Console.Error.WriteLine($"Timeslot unavailable: {arguments["--timeslot"]}");
					return (int)ExitCode.TimeslotUnavailable;
				}

				await client.ReserveTimeslot(selectedTimeslot.Value);
				Console.WriteLine("Timeslot reserved.");
			}
			catch (InvalidLoginException ex)
			{
				Console.Error.WriteLine(ex.Message);
				return (int)ExitCode.InvalidLogin;
			}
			catch(Exception ex)
			{
				Console.Error.WriteLine(ex);
				return (int)ExitCode.UnknownError;
			}

			return (int)ExitCode.Success;
		}

		private static (DayOfWeek Day, TimeSpan Time) ParseTimeslot(string timeslot)
		{
			var formatInfo = DateTimeFormatInfo.GetInstance(ReservationClient.CultureNL);

			string[] split = timeslot.Split(' ');

			DayOfWeek day = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().FirstOrDefault(e => formatInfo.GetDayName(e).Equals(split[0], StringComparison.OrdinalIgnoreCase));
			TimeSpan time = DateTime.ParseExact(split[1], "HH:mm", formatInfo).TimeOfDay;

			return (day, time);
		}
	}
}
