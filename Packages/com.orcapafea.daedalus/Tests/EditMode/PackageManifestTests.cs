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
            string json = PackageFiles.Read("package.json");
            Match block = Regex.Match(json, "\"dependencies\"\\s*:\\s*\\{(?<body>[^}]*)\\}");
            Assert.IsTrue(block.Success, "package.json has no dependencies block.");

            MatchCollection entries = Entry.Matches(block.Groups["body"].Value);
            Assert.IsNotEmpty(entries, "The dependencies block was not read.");
            foreach (Match entry in entries)
            {
                string version = entry.Groups[2].Value;
                Assert.IsTrue(ExactVersion.IsMatch(version), $"{entry.Groups[1].Value} is not pinned exactly: \"{version}\"");
            }
        }
    }
}
