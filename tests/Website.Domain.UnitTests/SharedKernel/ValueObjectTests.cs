using Website.Domain.SharedKernel;

namespace Website.Domain.UnitTests.SharedKernel;

public class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        public decimal Amount { get; } = amount;
        public string Currency { get; } = currency;

        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void SameComponents_AreEqual()
    {
        var a = new Money(100, "VND");
        var b = new Money(100, "VND");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void DifferentComponents_AreNotEqual()
    {
        Assert.NotEqual(new Money(100, "VND"), new Money(200, "VND"));
        Assert.True(new Money(100, "VND") != new Money(100, "USD"));
    }
}
