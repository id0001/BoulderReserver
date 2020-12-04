
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BoulderReserver
{
	public class ReservationClient : IDisposable
	{
		public static CultureInfo CultureNL
		{
			get
			{
				var culture = new CultureInfo("nl-NL");
				culture.DateTimeFormat.AbbreviatedMonthNames = new string[] { "jan", "feb", "maa", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec", string.Empty };
				return culture;
			}
		}

		private static readonly string[] TimeslotDateFormats = new[] {
			"dddd (dd MMM)",
			"dddd (dd MMM.)"
		};

		private readonly HttpClient client;
		private bool disposedValue;

		public ReservationClient(string baseUrl, int gym, string username, string password)
		{
			Gym = gym;
			Username = username;
			Password = password;
			client = CreateHttpClient(baseUrl);
		}

		public string Username { get; }

		public int Gym { get; }

		public string Password { get; }

		private string LoginUrl => $"/nl/klimmen/reservations/gym-{Gym}/";

		private string TimeslotUrl => $"/nl/klimmen/reservations/gym-{Gym}/reserve";

		/// <summary>
		/// Log in to the website using the provider username and password.
		/// </summary>
		/// <returns>Task</returns>
		public async Task LoginAsync()
		{
			var content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				{ "login_email", Username },
				{"login_password", Password },
				{"login_with_pass", "Log in" }
			});

			var response = await client.PostAsync(LoginUrl, content);
			response.EnsureSuccessStatusCode();

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());
			EnsureLoggedIn(htmlDoc);
		}

		/// <summary>
		/// Get all available timeslots.
		/// </summary>
		/// <returns>A list of available timeslots</returns>
		public async Task<SortedDictionary<DateTime, string>> GetTimeSlotsAsync(int groupAmount)
		{
			var response = await SelectGroupAmountAsync(groupAmount, await SelectBoulderenAsync());

			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());

			var timeslotWrappers = htmlDoc.DocumentNode.SelectNodes("//div[@class='timeslot-day-wrapper']");

			var result = new SortedDictionary<DateTime, string>();

			foreach (var wrapper in timeslotWrappers)
			{
				string title = wrapper.SelectSingleNode("h3")?.InnerText;
				var date = DateTime.ParseExact(title, TimeslotDateFormats, CultureNL);

				var slots = wrapper.SelectNodes("a[contains(@class, 'timeslot-selector')]");
				foreach (var slot in slots)
				{
					TimeSpan time = DateTime.ParseExact(slot.InnerText.TrimEnd('*'), "HH:mm", CultureNL).TimeOfDay;

					var dt = date.Date.Add(time);
					string url = slot.Attributes["href"].Value;

					result.Add(dt, url);
				}

			}

			return result;
		}

		/// <summary>
		/// Reserve a timeslot.
		/// </summary>
		/// <param name="url">The url of the timeslot</param>
		/// <returns>Task</returns>
		public async Task ReserveTimeslot(string url)
		{
			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(await client.GetStringAsync(url));

			var formInputs = htmlDoc.DocumentNode.SelectNodes("//input").Where(e => !e.Attributes.Contains("disabled") && !(e.GetAttributeValue("type", string.Empty) == "checkbox" && !e.Attributes.Contains("checked")));

			var content = new FormUrlEncodedContent(formInputs.Select(input =>
			{
				string name = input.Attributes["name"].Value;
				string value = input.Attributes["value"].Value;
				return new KeyValuePair<string, string>(name, value);
			}));

			await client.PostAsync(url, content);
		}

		private async Task<HttpResponseMessage> SelectBoulderenAsync()
		{
			var content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				{"select_area_1", "Boulderen" }
			});

			return await client.PostAsync(TimeslotUrl, content);
		}

		private async Task<HttpResponseMessage> SelectGroupAmountAsync(int amount, HttpResponseMessage response)
		{
			var htmlDoc = new HtmlDocument();
			htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());

			var groupOptions = ExtractGroupOptions(htmlDoc);
			if (!groupOptions.TryGetValue(amount, out var groupOption))
				throw new GroupAmountNotAvailableException();

			var content = new FormUrlEncodedContent(new Dictionary<string, string>(new[] { groupOption }));

			return await client.PostAsync(TimeslotUrl, content);
		}

		private IDictionary<int, KeyValuePair<string, string>> ExtractGroupOptions(HtmlDocument html)
		{
			var nodes = html.DocumentNode.SelectNodes("//input[starts-with(@name, 'select_number')]");

			var result = new Dictionary<int, KeyValuePair<string, string>>();
			foreach (var node in nodes)
			{
				int num = int.Parse(node.GetAttributeValue("name", string.Empty).Substring("select_number_".Length));
				string key = node.GetAttributeValue("name", string.Empty);
				string value = node.GetAttributeValue("value", string.Empty);
				result.Add(num, new KeyValuePair<string, string>(key, value));
			}

			return result;
		}

		private static HttpClient CreateHttpClient(string baseUrl)
		{
			var cookieContainer = new CookieContainer();
			var httpClientHandler = new HttpClientHandler
			{
				AllowAutoRedirect = true,
				UseCookies = true,
				CookieContainer = cookieContainer,
			};

			var httpClient = new HttpClient(httpClientHandler, true);
			httpClient.BaseAddress = new Uri(baseUrl);
			return httpClient;
		}

		private static void EnsureLoggedIn(HtmlDocument html)
		{
			var node = html.DocumentNode.SelectSingleNode("//li[@class='error']");
			if (node != null)
				throw new InvalidLoginException(node.InnerText);
		}

		#region IDisposable

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					client.Dispose();
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		#endregion
	}
}
