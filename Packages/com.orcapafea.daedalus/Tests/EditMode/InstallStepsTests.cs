using NUnit.Framework;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The student install steps exist and the README, which is the registry listing, links them.</summary>
    public class InstallStepsTests
    {
        const string StepsPath = "Documentation~/install-ko.md";

        [Test]
        public void TheReadmeLinksTheInstallSteps()
        {
            StringAssert.Contains($"({StepsPath})", PackageFiles.Read("README.md"));
        }

        [Test]
        public void TheStepsNameTheVersionAndTheRegistry()
        {
            string steps = PackageFiles.Read(StepsPath);
            StringAssert.Contains("6000.0.85f1", steps);
            StringAssert.Contains("https://package.openupm.com", steps);
            StringAssert.Contains("com.orcapafea", steps);
        }
    }
}
