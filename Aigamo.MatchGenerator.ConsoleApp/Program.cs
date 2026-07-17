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
abstract record MaritalStatus
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
			onMale: () => "male",
			onFemale: () => "female"
		);

		Console.WriteLine(x);

		var maritalStatus = new MaritalStatus.Single();

		var y = maritalStatus.Match(
			onSingle: x => "single",
			onMarried: x => "married",
			onDivorced: x => "divorced",
			onWidowed: x => "widowed"
		);

		Console.WriteLine(y);

		var day = DayOfWeek.Monday;

		var z = day.Match(
			onSunday: () => "sunday",
			onMonday: () => "monday",
			onTuesday: () => "tuesday",
			onWednesday: () => "wednesday",
			onThursday: () => "thursday",
			onFriday: () => "friday",
			onSaturday: () => "saturday"
		);

		Console.WriteLine(z);
	}
}
