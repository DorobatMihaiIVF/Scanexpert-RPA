using PixelDataProgramari.Logic;
using Xunit;

namespace PixelDataProgramari.Logic.Tests
{
    public class NameSplitterTests
    {
        [Fact]
        public void Split_BothPartsPresent_UsesThemAsGiven()
        {
            PersonName name = NameSplitter.Split("TEST ALTCEVA COMPLET", "TEST POPESCU", "ION MARIA");

            Assert.Equal("TEST POPESCU", name.LastName);
            Assert.Equal("ION MARIA", name.FirstName);
        }

        [Fact]
        public void Split_BothPartsPresent_TrimsThem()
        {
            PersonName name = NameSplitter.Split("TEST POPESCU ION", "  TEST POPESCU  ", "  ION ");

            Assert.Equal("TEST POPESCU", name.LastName);
            Assert.Equal("ION", name.FirstName);
        }

        [Fact]
        public void Split_BothPartsEmpty_FirstWordIsLastName_RestIsFirstName()
        {
            PersonName name = NameSplitter.Split("TESTPOPESCU ION MARIA", "", "");

            Assert.Equal("TESTPOPESCU", name.LastName);
            Assert.Equal("ION MARIA", name.FirstName);
        }

        [Theory]
        [InlineData("TESTPOPESCU", "")]
        [InlineData("", "ION")]
        public void Split_OnlyOnePartPresent_SplitsFullName(string lastName, string firstName)
        {
            PersonName name = NameSplitter.Split("TESTIONESCU ANA MARIA", lastName, firstName);

            Assert.Equal("TESTIONESCU", name.LastName);
            Assert.Equal("ANA MARIA", name.FirstName);
        }

        [Fact]
        public void Split_FullName_CollapsesMultipleSpaces()
        {
            PersonName name = NameSplitter.Split("  TESTPOPESCU   ION    MARIA  ", "", "");

            Assert.Equal("TESTPOPESCU", name.LastName);
            Assert.Equal("ION MARIA", name.FirstName);
        }

        [Fact]
        public void Split_SingleWord_FirstNameIsEmpty()
        {
            PersonName name = NameSplitter.Split("TESTPOPESCU", "", "");

            Assert.Equal("TESTPOPESCU", name.LastName);
            Assert.Equal("", name.FirstName);
        }

        [Fact]
        public void Split_SingleWordWithSurroundingSpaces_FirstNameIsEmpty()
        {
            PersonName name = NameSplitter.Split("   TESTPOPESCU   ", "", "");

            Assert.Equal("TESTPOPESCU", name.LastName);
            Assert.Equal("", name.FirstName);
        }

        [Fact]
        public void Split_HyphenatedLastName_StaysOneWord()
        {
            PersonName name = NameSplitter.Split("TESTPOPESCU-IONESCU ANA", "", "");

            Assert.Equal("TESTPOPESCU-IONESCU", name.LastName);
            Assert.Equal("ANA", name.FirstName);
        }

        [Fact]
        public void Split_WhitespaceOnlyPart_CountsAsEmpty()
        {
            PersonName name = NameSplitter.Split("TESTIONESCU ANA", "   ", "ION");

            Assert.Equal("TESTIONESCU", name.LastName);
            Assert.Equal("ANA", name.FirstName);
        }

        [Fact]
        public void Split_TabsAndNewlines_SeparateWords()
        {
            PersonName name = NameSplitter.Split("TESTPOPESCU\tION\n  MARIA", "", "");

            Assert.Equal("TESTPOPESCU", name.LastName);
            Assert.Equal("ION MARIA", name.FirstName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Split_NothingToSplit_BothEmpty(string fullName)
        {
            PersonName name = NameSplitter.Split(fullName, "", "");

            Assert.Equal("", name.LastName);
            Assert.Equal("", name.FirstName);
        }

        [Fact]
        public void Split_NullArguments_TreatedAsEmpty()
        {
            PersonName none = NameSplitter.Split(null, null, null);
            PersonName fromFull = NameSplitter.Split("TESTPOPESCU ION", null, null);
            PersonName fromParts = NameSplitter.Split(null, "TESTPOPESCU", "ION");

            Assert.Equal("", none.LastName);
            Assert.Equal("", none.FirstName);
            Assert.Equal("TESTPOPESCU", fromFull.LastName);
            Assert.Equal("ION", fromFull.FirstName);
            Assert.Equal("TESTPOPESCU", fromParts.LastName);
            Assert.Equal("ION", fromParts.FirstName);
        }

        [Fact]
        public void Split_KeepsDiacriticsAndCase()
        {
            PersonName name = NameSplitter.Split("Testștefănescu Ștefan Țicu", "", "");

            Assert.Equal("Testștefănescu", name.LastName);
            Assert.Equal("Ștefan Țicu", name.FirstName);
        }
    }
}
