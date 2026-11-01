using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The package installs on every Unity 6 and pins exact dependency versions.</summary>
    public class PackageManifestTests
    {
        static readonly Regex Entry = new Regex("\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\"");
        static readonly Regex ExactVersion = new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$");

        [Test]
        public void NameAndMinimumUnity()
        {
            string json = PackageFiles.Read("package.json");
            StringAssert.Contains("\"name\": \"com.orcapafea.daedalus\"", json);
            StringAssert.Contains("\"unity\": \"6000.0\"", json);
        }

        [Test]
        public void EveryDependencyIsPinnedExactly()
        {
            MatchCollection entries = DependencyEntries();
            Assert.IsNotEmpty(entries, "The dependencies block was not read.");
            foreach (Match entry in entries)
            {
                string version = entry.Groups[2].Value;
                Assert.IsTrue(ExactVersion.IsMatch(version), $"{entry.Groups[1].Value} is not pinned exactly: \"{version}\"");
            }
        }

        /// <summary>A dependency added or bumped without editing this test fails it (CLAUDE.md Project Config).</summary>
        [Test]
        public void DependenciesAreExactlyTheExpectedSet()
        {
            var expected = new Dictionary<string, string>
            {
                { "com.unity.inputsystem", "1.11.2" },
                { "com.unity.modules.imageconversion", "1.0.0" },
                { "com.unity.modules.tilemap", "1.0.0" },
            };
            var actual = new Dictionary<string, string>();
            foreach (Match entry in DependencyEntries())
            {
                actual.Add(entry.Groups[1].Value, entry.Groups[2].Value);
            }
            CollectionAssert.AreEquivalent(expected, actual);
        }

        static MatchCollection DependencyEntries()
        {
            string json = PackageFiles.Read("package.json");
            Match block = Regex.Match(json, "\"dependencies\"\\s*:\\s*\\{(?<body>[^}]*)\\}");
            Assert.IsTrue(block.Success, "package.json has no dependencies block.");
            return Entry.Matches(block.Groups["body"].Value);
        }
    }
}
