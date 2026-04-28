using FluentAssertions;
using FplLiveRank.Domain.Enums;

namespace FplLiveRank.UnitTests.Domain;

public sealed class ChipTypeParserTests
{
    [Theory]
    [InlineData("wildcard", ChipType.Wildcard)]
    [InlineData("WILDCARD", ChipType.Wildcard)]
    [InlineData("freehit", ChipType.FreeHit)]
    [InlineData("bboost", ChipType.BenchBoost)]
    [InlineData("3xc", ChipType.TripleCaptain)]
    [InlineData(null, ChipType.None)]
    [InlineData("", ChipType.None)]
    [InlineData("unknown", ChipType.None)]
    public void Parse_maps_fpl_chip_codes(string? value, ChipType expected)
    {
        var result = ChipTypeParser.Parse(value);

        result.Should().Be(expected);
    }
}
