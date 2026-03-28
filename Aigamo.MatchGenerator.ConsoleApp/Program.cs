namespace Aigamo.MatchGenerator.ConsoleApp;

enum Gender
{
	Male = 1,
	Female,
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
	}
}
