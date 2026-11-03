using GS1CodeStream.Server;

namespace GS1CodeStream.Tests;

public class MarkingCodeGeneratorTests
{
    [Theory]
    [InlineData("0000000000000", '0')]
    [InlineData("1234567890123", '1')]
    [InlineData("4006381333930", '7')]
    [InlineData("9876543210987", '9')]
    public void CalculateCheckDigit_ReturnsExpectedDigit(string digits, char expected)
    {
        Assert.Equal(expected, MarkingCodeGenerator.CalculateCheckDigit(digits));
    }

    [Fact]
    public void Generate_ReturnsCodesWithValidFormatAndCheckDigit()
    {
        var generator = new MarkingCodeGenerator();

        for (int i = 0; i < 100; i++)
        {
            string code = generator.Generate();

            Assert.Matches(@"\A\(01\)[0-9]{14}\(21\)[A-Z0-9]{12}\z", code);

            string gtin = code.Substring(4, 14);
            int weightedSum = 0;
            for (int j = 0; j < gtin.Length; j++)
            {
                weightedSum += (gtin[j] - '0') * (j % 2 == 0 ? 3 : 1);
            }

            Assert.Equal(0, weightedSum % 10);
        }
    }
}
