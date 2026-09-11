using System;
using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class ValidationIssueTests
    {
        [Fact]
        public void Constructor_KeepsValues()
        {
            var issue = new ValidationIssue("MISSING_FIELD", "BranchName", "sucursala lipsește");

            Assert.Equal("MISSING_FIELD", issue.Code);
            Assert.Equal("BranchName", issue.Field);
            Assert.Equal("sucursala lipsește", issue.Message);
        }

        [Fact]
        public void Constructor_NullBecomesEmpty()
        {
            var issue = new ValidationIssue(null, null, null);

            Assert.Equal("", issue.Code);
            Assert.Equal("", issue.Field);
            Assert.Equal("", issue.Message);
        }
    }

    public class CnpCheckResultTests
    {
        [Fact]
        public void Constructor_KeepsValues()
        {
            var result = new CnpCheckResult(true, "", new DateTime(1985, 3, 12));

            Assert.True(result.IsValid);
            Assert.Equal("", result.Reason);
            Assert.True(result.BirthDate.HasValue);
            Assert.Equal(new DateTime(1985, 3, 12), result.BirthDate.Value);
        }

        [Fact]
        public void Constructor_NullReasonBecomesEmpty()
        {
            var result = new CnpCheckResult(false, null, null);

            Assert.False(result.IsValid);
            Assert.Equal("", result.Reason);
            Assert.False(result.BirthDate.HasValue);
        }
    }

    public class PersonNameTests
    {
        [Fact]
        public void Constructor_KeepsValues()
        {
            var name = new PersonName("TESTPOPESCU", "ION");

            Assert.Equal("TESTPOPESCU", name.LastName);
            Assert.Equal("ION", name.FirstName);
        }

        [Fact]
        public void Constructor_NullBecomesEmpty()
        {
            var name = new PersonName(null, null);

            Assert.Equal("", name.LastName);
            Assert.Equal("", name.FirstName);
        }
    }
}
