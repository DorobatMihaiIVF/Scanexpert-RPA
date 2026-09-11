using System;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    // CNP-uri fictive; cifra de control calculată cu ponderile 279146358279 (investigatie §CNP).
    // Ordinea verificărilor în CnpValidator: gol, lungime, cifre, S != 0, lună, zi, cifra de control.
    public class CnpValidatorTests
    {
        [Theory]
        [InlineData("1850312400012", 1985, 3, 12)]  // S=1: 1900-1999
        [InlineData("2900725171236", 1990, 7, 25)]  // S=2
        [InlineData("2990101010033", 1999, 1, 1)]
        [InlineData("5030228224566", 2003, 2, 28)]  // S=5: 2000-2099
        [InlineData("6101231087890", 2010, 12, 31)] // S=6, cifra de control 0
        [InlineData("5000229170104", 2000, 2, 29)]  // 29 februarie, an bisect
        [InlineData("6240229520041", 2024, 2, 29)]
        [InlineData("3500615123337", 1850, 6, 15)]  // S=3: 1800-1899
        [InlineData("4991231160020", 1899, 12, 31)] // S=4
        [InlineData("1850312400081", 1985, 3, 12)]  // rest 10 => cifra de control 1
        public void Check_ValidCnp_IsValidAndExtractsBirthDate(string cnp, int year, int month, int day)
        {
            CnpCheckResult result = CnpValidator.Check(cnp);

            Assert.True(result.IsValid);
            Assert.Equal("", result.Reason);
            Assert.True(result.BirthDate.HasValue);
            Assert.Equal(new DateTime(year, month, day), result.BirthDate.Value);
            Assert.True(CnpValidator.IsValid(cnp));
        }

        [Theory]
        [InlineData("5030228224566", 2003)]
        [InlineData("6101231087890", 2010)]
        public void Check_SexDigit5Or6_BirthYearIn2000s(string cnp, int expectedYear)
        {
            CnpCheckResult result = CnpValidator.Check(cnp);

            Assert.True(result.BirthDate.HasValue);
            Assert.Equal(expectedYear, result.BirthDate.Value.Year);
        }

        [Theory]
        [InlineData("7850312400013")] // S=7
        [InlineData("8900505400061")] // S=8
        [InlineData("9850312400017")] // S=9
        [InlineData("7000229400012")] // 29.02.„00”: valid doar în 2000, acceptat
        public void Check_ForeignerSexDigit_ValidWithoutBirthDate(string cnp)
        {
            CnpCheckResult result = CnpValidator.Check(cnp);

            Assert.True(result.IsValid);
            Assert.Equal("", result.Reason);
            Assert.False(result.BirthDate.HasValue);
        }

        [Fact]
        public void Check_ForeignerSexDigit_DayInvalidInEveryCentury_ReasonDay()
        {
            AssertInvalid("7010229400010", CnpValidator.ReasonDay); // 29.02.„01”: nebisect în 1801, 1901, 2001
        }

        [Theory]
        [InlineData("1850312990010")] // JJ = 99
        [InlineData("1850312470013")] // JJ = 47
        public void Check_CountyCodeIsNotChecked(string cnp)
        {
            Assert.True(CnpValidator.IsValid(cnp));
        }

        [Theory]
        [InlineData("1850312400013")] // corect: ...2
        [InlineData("2900725171230")] // corect: ...6
        [InlineData("1850312400080")] // rest 10: cifra corectă e 1, nu 0
        public void Check_WrongChecksum_ReasonChecksum(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonChecksum);
        }

        [Theory]
        [InlineData((string)null)]
        [InlineData("")]
        public void Check_NullOrEmpty_ReasonEmpty(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonEmpty);
        }

        [Theory]
        [InlineData("185031240001")]   // 12 cifre
        [InlineData("18503124000120")] // 14 cifre
        [InlineData(" 1850312400012")] // fără Trim: 14 caractere
        [InlineData("18503124000A")]   // lungimea se verifică înaintea cifrelor
        public void Check_WrongLength_ReasonLength(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonLength);
        }

        [Theory]
        [InlineData("18503124000A2")]
        [InlineData("1850312 00012")]
        [InlineData("185031240001 ")] // spațiu la final, fără Trim
        [InlineData("-850312400012")]
        [InlineData("185031240001２")] // cifră „２” fullwidth, nu ASCII
        public void Check_NonDigits_ReasonNotDigits(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonNotDigits);
        }

        [Theory]
        [InlineData("0850312400010")] // cifra de control corectă
        [InlineData("0851312400010")] // S=0 se verifică înaintea lunii
        public void Check_SexDigitZero_ReasonFirstDigitZero(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonFirstDigitZero);
        }

        [Theory]
        [InlineData("1851312400013")] // luna 13, cifra de control corectă
        [InlineData("1850012400011")] // luna 00, cifra de control corectă
        [InlineData("1851312400010")] // luna 13 și cifra de control greșită: luna se verifică prima
        public void Check_InvalidMonth_ReasonMonth(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonMonth);
        }

        [Theory]
        [InlineData("1850431400014")] // 31 aprilie
        [InlineData("1000229400011")] // 29 februarie 1900 (an nebisect)
        [InlineData("1850300400011")] // ziua 00
        [InlineData("1850332400013")] // ziua 32
        public void Check_InvalidDay_ReasonDay(string cnp)
        {
            AssertInvalid(cnp, CnpValidator.ReasonDay);
        }

        [Fact]
        public void ReasonConstants_AreDistinctAndNotEmpty()
        {
            string[] reasons =
            {
                CnpValidator.ReasonEmpty, CnpValidator.ReasonLength, CnpValidator.ReasonNotDigits,
                CnpValidator.ReasonFirstDigitZero, CnpValidator.ReasonMonth, CnpValidator.ReasonDay,
                CnpValidator.ReasonChecksum,
            };

            Assert.All(reasons, r => Assert.False(string.IsNullOrEmpty(r)));
            Assert.Equal(reasons.Length, new System.Collections.Generic.HashSet<string>(reasons).Count);
        }

        private static void AssertInvalid(string cnp, string expectedReason)
        {
            CnpCheckResult result = CnpValidator.Check(cnp);

            Assert.False(result.IsValid);
            Assert.Equal(expectedReason, result.Reason);
            Assert.False(result.BirthDate.HasValue);
            Assert.False(CnpValidator.IsValid(cnp));
        }
    }
}
