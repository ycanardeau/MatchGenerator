using Aigamo.MatchGenerator;

// Generate a Match for an enum we don't own (System.DayOfWeek).
[assembly: GenerateMatchFor(typeof(DayOfWeek))]

namespace Aigamo.MatchGenerator.ConsoleApp;

[GenerateMatch]
enum Gender
{
	Male = 1,
	Female,
}

[GenerateMatch]
closed record MaritalStatus
{
	private MaritalStatus() { }

	public sealed record Single : MaritalStatus;
	public sealed record Married : MaritalStatus;
	public sealed record Divorced : MaritalStatus;
	public sealed record Widowed : MaritalStatus;
}

class Program
{
	static void Main()
	{
		var gender = Gender.Male;

		var x = gender.Match(
			Male: () => "male",
			Female: () => "female"
		);

		Console.WriteLine(x);

		var maritalStatus = new MaritalStatus.Single();

		var y = maritalStatus.Match(
			Single: x => "single",
			Married: x => "married",
			Divorced: x => "divorced",
			Widowed: x => "widowed"
		);

		Console.WriteLine(y);

		var day = DayOfWeek.Monday;

		var z = day.Match(
			Sunday: () => "sunday",
			Monday: () => "monday",
			Tuesday: () => "tuesday",
			Wednesday: () => "wednesday",
			Thursday: () => "thursday",
			Friday: () => "friday",
			Saturday: () => "saturday"
		);

		Console.WriteLine(z);
	}
}
