namespace EniadCsharpDotnet;

/// <summary>
/// Provides operations for comparing integer values.
/// </summary>
public static class NumberComparison
{
    /// <summary>
    /// Returns the larger of two integer values.
    /// </summary>
    /// <param name="firstValue">The first value to compare.</param>
    /// <param name="secondValue">The second value to compare.</param>
    /// <returns>The larger value. If both values are equal, that value is returned.</returns>
    public static int GetLarger(int firstValue, int secondValue) =>
        Math.Max(firstValue, secondValue);
}
