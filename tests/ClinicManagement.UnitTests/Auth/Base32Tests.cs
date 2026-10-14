using ClinicManagement.Application.Auth.Totp;
using Xunit;

namespace ClinicManagement.UnitTests.Auth;

public sealed class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Encode_MatchesRfc4648TestVectors(string input, string expected)
    {
        var encoded = Base32.Encode(System.Text.Encoding.ASCII.GetBytes(input));

        Assert.Equal(expected, encoded);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("MY", "f")]
    [InlineData("MZXQ", "fo")]
    [InlineData("MZXW6", "foo")]
    [InlineData("MZXW6YQ", "foob")]
    [InlineData("MZXW6YTB", "fooba")]
    [InlineData("MZXW6YTBOI", "foobar")]
    public void Decode_MatchesRfc4648TestVectors(string encoded, string expected)
    {
        var decoded = System.Text.Encoding.ASCII.GetString(Base32.Decode(encoded));

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void Decode_RoundTripsRandomBytes()
    {
        var data = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(data);

        var decoded = Base32.Decode(Base32.Encode(data));

        Assert.Equal(data, decoded);
    }

    [Theory]
    [InlineData("mzxw6ytboi")]
    [InlineData("MZXW 6YTB OI")]
    [InlineData("mzxw-6ytb-oi")]
    [InlineData("MZXW6YTBOI====")]
    public void Decode_IsTolerantOfAuthenticatorAppFormatting(string input)
    {
        var decoded = Base32.Decode(input);

        Assert.Equal("foobar", System.Text.Encoding.ASCII.GetString(decoded));
    }

    [Theory]
    [InlineData("MZXW6YTB01")]
    [InlineData("not-base-32!")]
    public void Decode_RejectsCharactersOutsideTheAlphabet(string input)
    {
        Assert.Throws<FormatException>(() => Base32.Decode(input));
    }
}