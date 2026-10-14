using ClinicManagement.Domain.Common;
using Xunit;

namespace ClinicManagement.UnitTests.Patients;

public sealed class SubstanceNormalizerTests
{
    [Theory]
    [InlineData("أسبرين", "اسبرين")]
    [InlineData("إسبرين", "اسبرين")]
    [InlineData("آسبرين", "اسبرين")]
    [InlineData("أسبرين‌", "اسبرين")]
    public void Normalize_CollapsesArabicOrthographicVariants(string input, string expected)
    {
        Assert.Equal(expected, SubstanceNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("Benadryl", "benadryl")]
    [InlineData("  Penicillin  ", "penicillin")]
    [InlineData("Amoxicilline, 500mg", "amoxicilline 500mg")]
    [InlineData("Piroxicam   20", "piroxicam 20")]
    public void Normalize_ProducesAStableLookupKey(string input, string expected)
    {
        Assert.Equal(expected, SubstanceNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_TreatsCaseAndSpacingDifferencesAsEqual()
    {
        var first = SubstanceNormalizer.Normalize("Amoxicillin  500MG");
        var second = SubstanceNormalizer.Normalize("amoxicillin 500mg");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsEmptyForBlankInput(string? input)
    {
        Assert.Equal(string.Empty, SubstanceNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_KeepsDigitsAndDoseUnits()
    {
        Assert.Equal("insulin 100iu", SubstanceNormalizer.Normalize("Insulin 100IU"));
        Assert.Equal("vitamin d-3", SubstanceNormalizer.Normalize("Vitamin D-3"));
    }

    [Fact]
    public void Normalize_TreatsArabicAndLatinSpellingsOfTheSameDrugAsDifferent()
    {
        Assert.NotEqual(
            SubstanceNormalizer.Normalize("أسبرين"),
            SubstanceNormalizer.Normalize("aspirin"));
    }
}