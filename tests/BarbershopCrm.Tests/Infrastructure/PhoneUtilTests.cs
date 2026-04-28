using BarbershopCrm.Web.Common;

namespace BarbershopCrm.Tests.Infrastructure;

public class PhoneUtilTests
{
    [Theory]
    [InlineData("+7 (920) 111-22-33", "+79201112233")]
    [InlineData("89201112233", "+79201112233")]
    [InlineData("9201112233", "+79201112233")]
    [InlineData("79201112233", "+79201112233")]
    [InlineData("", "")]
    public void Normalize_KnownInputs_ReturnsCanonical(string input, string expected)
    {
        Assert.Equal(expected, PhoneUtil.Normalize(input));
    }

    [Theory]
    [InlineData("+7 (920) 111-22-33", true)]
    [InlineData("89201112233", true)]
    [InlineData("9201112233", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void IsValid_KnownInputs(string input, bool expected)
    {
        Assert.Equal(expected, PhoneUtil.IsValid(input));
    }

    [Theory]
    [InlineData("+7 (920) 111-22-33", "79201112233")]
    [InlineData("abc", "")]
    public void Digits_StripsNonDigits(string input, string expected)
    {
        Assert.Equal(expected, PhoneUtil.Digits(input));
    }
}
