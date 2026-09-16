using Aigamo.MatchGenerator;
using BenchmarkDotNet.Attributes;

namespace Aigamo.MatchGenerator.Benchmarks;

[GenerateMatch]
public enum Color
{
	Red = 1,
	Green,
	Blue,
}

[GenerateMatch]
public closed record Shape
{
	private Shape() { }

	public sealed record Circle(double Radius) : Shape;

	public sealed record Square(double Side) : Shape;

	public sealed record Rectangle(double Width, double Height) : Shape;
}

[MemoryDiagnoser]
public class MatchBenchmarks
{
	private readonly Color _color = Color.Blue;
	private readonly Shape _shape = new Shape.Rectangle(3, 4);

	// Non-capturing delegates are cached once by the compiler in static fields, so calls through
	// them measure only the generated Match method's dispatch (the switch + delegate invoke) with
	// no per-call closure allocation.
	private static readonly Func<string> s_onRed = () => "red";
	private static readonly Func<string> s_onGreen = () => "green";
	private static readonly Func<string> s_onBlue = () => "blue";

	private static readonly Func<Shape.Circle, double> s_onCircle = c => c.Radius;
	private static readonly Func<Shape.Square, double> s_onSquare = s => s.Side;
	private static readonly Func<Shape.Rectangle, double> s_onRectangle = r => r.Width * r.Height;

	// --- Dispatch cost only (cached delegates, no allocation) ---

	[Benchmark]
	public string EnumMatch() =>
		_color.Match(onRed: s_onRed, onGreen: s_onGreen, onBlue: s_onBlue);

	[Benchmark]
	public double UnionMatch() =>
		_shape.Match(onCircle: s_onCircle, onSquare: s_onSquare, onRectangle: s_onRectangle);

	// --- Allocation story: static (non-capturing) vs capturing lambdas at the call site ---

	[Benchmark]
	public string EnumMatch_StaticLambdas() =>
		_color.Match(
			onRed: static () => "red",
			onGreen: static () => "green",
			onBlue: static () => "blue"
		);

	[Benchmark]
	public double UnionMatch_CapturingLambdas()
	{
		// Capture a local so the compiler must allocate a fresh closure on every call.
		var scale = _shape.GetHashCode() & 1;
		return _shape.Match(
			onCircle: c => c.Radius * scale,
			onSquare: s => s.Side * scale,
			onRectangle: r => r.Width * r.Height * scale
		);
	}
}
