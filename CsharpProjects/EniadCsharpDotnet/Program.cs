using EniadCsharpDotnet;

const int defaultFirstValue = 500;
const int defaultSecondValue = 600;

if (!TryReadValues(args, out var firstValue, out var secondValue))
{
    Environment.ExitCode = 1;
    return;
}

var largerValue = NumberComparison.GetLarger(firstValue, secondValue);
Console.WriteLine(largerValue);

static bool TryReadValues(string[] args, out int firstValue, out int secondValue)
{
    if (args.Length == 0)
    {
        firstValue = defaultFirstValue;
        secondValue = defaultSecondValue;
        return true;
    }

    if (args.Length != 2 ||
        !int.TryParse(args[0], out firstValue) ||
        !int.TryParse(args[1], out secondValue))
    {
        Console.Error.WriteLine("Usage: EniadCsharpDotnet [first-integer second-integer]");
        return false;
    }

    return true;
}