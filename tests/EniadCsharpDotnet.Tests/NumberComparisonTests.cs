using EniadCsharpDotnet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EniadCsharpDotnet.Tests;

[TestClass]
public sealed class NumberComparisonTests
{
    [TestMethod]
    public void GetLargerReturnsTheLargerPositiveValue()
    {
        Assert.AreEqual(600, NumberComparison.GetLarger(500, 600));
    }

    [TestMethod]
    public void GetLargerReturnsTheFirstValueWhenItIsLarger()
    {
        Assert.AreEqual(600, NumberComparison.GetLarger(600, 500));
    }

    [TestMethod]
    public void GetLargerReturnsTheSameValueWhenValuesAreEqual()
    {
        Assert.AreEqual(42, NumberComparison.GetLarger(42, 42));
    }

    [TestMethod]
    public void GetLargerHandlesNegativeValues()
    {
        Assert.AreEqual(-2, NumberComparison.GetLarger(-2, -8));
    }
}
