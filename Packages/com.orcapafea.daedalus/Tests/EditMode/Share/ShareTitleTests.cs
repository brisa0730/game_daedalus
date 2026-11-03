using System.Linq;
using System.Text;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Title cleaning matches the gallery's clean_label (Deploy/gallery/src/text.rs), and the default zip filename.</summary>
    public class ShareTitleTests
    {
        // text.rs labels_are_cleaned, verbatim.
        [TestCase("  Spike   Hell \n", 40, "Spike Hell")]
        [TestCase("a\u0007b‮c⁦d", 40, "abcd")]
        [TestCase("가나다라마바사", 3, "가나다")]
        [TestCase("ab cd", 3, "ab")]
        [TestCase(" \t‮ ", 40, null)]
        [TestCase("", 40, null)]
        public void TheGallerysOwnCases(string raw, int maxChars, string expected)
        {
            Assert.AreEqual(expected, ShareTitle.Clean(raw, maxChars));
        }

        [Test]
        public void FortyOneKoreanSyllablesKeepTheFirstForty()
        {
            string raw = string.Concat(Enumerable.Repeat("가", 41));
            Assert.AreEqual(string.Concat(Enumerable.Repeat("가", 40)), ShareTitle.Clean(raw));
        }

        [Test]
        public void EmojiCountAsOneCharacterEach()
        {
            string raw = string.Concat(Enumerable.Repeat("😀", 41));
            Assert.AreEqual(82, raw.Length, "41 emoji are 82 UTF-16 units");
            Assert.AreEqual(string.Concat(Enumerable.Repeat("😀", 40)), ShareTitle.Clean(raw));
        }

        [Test]
        public void AKoreanAndEmojiMixIsCutAtFortyCharacters()
        {
            string raw = string.Concat(Enumerable.Repeat("가", 39)) + "😀😀나";
            Assert.AreEqual(string.Concat(Enumerable.Repeat("가", 39)) + "😀", ShareTitle.Clean(raw));
        }

        [TestCase("a b", "a b")]
        [TestCase("a　　b", "a b")]
        [TestCase("a\u0085b", "a b")]
        [TestCase("a \t\r\n b", "a b")]
        public void UnicodeWhitespaceCollapsesToOneSpace(string raw, string expected)
        {
            Assert.AreEqual(expected, ShareTitle.Clean(raw));
        }

        [TestCase("a​b")]
        [TestCase("a᠎b")]
        public void CharactersRustDoesNotCallWhitespaceAreKept(string raw)
        {
            Assert.AreEqual(raw, ShareTitle.Clean(raw));
        }

        [Test]
        public void ALoneSurrogateIsDropped()
        {
            Assert.AreEqual("ab", ShareTitle.Clean("a\uD83Db"));
        }

        [Test]
        public void ASpaceAtTheCutIsTrimmed()
        {
            string raw = new StringBuilder().Append('a', 39).Append(" bcd").ToString();
            Assert.AreEqual(new string('a', 39), ShareTitle.Clean(raw));
        }

        [TestCase("Spike Hell", "Spike Hell.zip")]
        [TestCase("a/b\\c:d*e?f\"g<h>i|j", "a-b-c-d-e-f-g-h-i-j.zip")]
        [TestCase("Level 1.", "Level 1.zip")]
        [TestCase("con", "con-game.zip")]
        [TestCase("COM3", "COM3-game.zip")]
        [TestCase("COM", "COM.zip")]
        [TestCase("...", "my-game.zip")]
        [TestCase("함정 지옥", "함정 지옥.zip")]
        public void FileNames(string cleanTitle, string expected)
        {
            Assert.AreEqual(expected, ShareTitle.FileName(cleanTitle));
        }
    }
}
