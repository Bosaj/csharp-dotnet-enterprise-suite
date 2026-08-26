using EniadCsharpDotnet;

namespace EniadCsharpDotnet.Tests;

[TestClass]
public sealed class NumberComparisonTests
{
    [TestMethod]
    public void GetLarger_ReturnsTheLargerPositiveValue()
    {
        Assert.AreEqual(600, NumberComparison.GetLarger(500, 600));
    }

    [TestMethod]
    public void GetLarger_ReturnsTheFirstValueWhenItIsLarger()
    {
        Assert.AreEqual(600, NumberComparison.GetLarger(600, 500));
    }

    [TestMethod]
    public void GetLarger_ReturnsTheSameValueWhenValuesAreEqual()
    {
        Assert.AreEqual(42, NumberComparison.GetLarger(42, 42));
    }

    [TestMethod]
    public void GetLarger_HandlesNegativeValues()
    {
        Assert.AreEqual(-2, NumberComparison.GetLarger(-2, -8));
    }
}
