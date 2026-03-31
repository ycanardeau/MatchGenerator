namespace Aigamo.MatchGenerator.ConsoleApp;

[GenerateMatch]
enum Gender
{
	Male = 1,
	Female,
}

[GenerateMatch]
abstract record MaritalStatus;

sealed record Single : MaritalStatus;

sealed record Married : MaritalStatus;

sealed record Divorced : MaritalStatus;

sealed record Widowed : MaritalStatus;

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

		var maritalStatus = new Single();

		var y = maritalStatus.Match(
			onDivorced: x => "divorced",
			onMarried: x => "married",
			onSingle: x => "single",
			onWidowed: x => "widowed"
		);

		Console.WriteLine(y);
	}
}
