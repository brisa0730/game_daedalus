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

        [Test]
        public void TheStageStepsComeAfterTheFirstShareAndSaveBeforeDuplicating()
        {
            string steps = PackageFiles.Read(StepsPath);
            int share = steps.IndexOf("## 8.", System.StringComparison.Ordinal);
            int stages = steps.IndexOf("## 9.", System.StringComparison.Ordinal);
            Assert.That(share, Is.GreaterThan(0), "the share section");
            Assert.That(stages, Is.GreaterThan(share), "sharing one level never depends on stages");
            string section = steps.Substring(stages);
            int save = section.IndexOf("Ctrl+S", System.StringComparison.Ordinal);
            int duplicate = section.IndexOf("Ctrl+D", System.StringComparison.Ordinal);
            Assert.That(save, Is.GreaterThan(0));
            Assert.That(duplicate, Is.GreaterThan(save), "a duplicate only has what was saved");
            StringAssert.Contains("Next stage", section);
        }
    }
}
