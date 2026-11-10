using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>What a public release needs in package.json: OpenUPM and Package Manager read these fields.</summary>
    public class ReleaseMetadataTests
    {
        const string PackageFolderInRepo = "/Packages/com.orcapafea.daedalus/";

        [Serializable]
        class Repository
        {
            public string url = "";
        }

        [Serializable]
        class Sample
        {
            public string displayName = "";
            public string path = "";
        }

        [Serializable]
        class Manifest
        {
            public string version = "";
            public string license = "";
            public string documentationUrl = "";
            public Repository repository = new Repository();
            public Sample[] samples = Array.Empty<Sample>();
        }

        static Manifest Read()
        {
            return JsonUtility.FromJson<Manifest>(PackageFiles.Read("package.json"));
        }

        [Test]
        public void VersionIsPlainSemver()
        {
            string version = Read().version;
            Assert.IsTrue(Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"), $"Not a plain x.y.z version: \"{version}\"");
            Assert.AreEqual("0.2.0", version);
        }

        [Test]
        public void LicenseIsMitAndTheFileIsThere()
        {
            Assert.AreEqual("MIT", Read().license);
            StringAssert.StartsWith("MIT License", PackageFiles.Read("LICENSE.md"));
        }

        [Test]
        public void RepositoryIsThePublicOne()
        {
            Assert.AreEqual("https://github.com/brisa0730/game_daedalus.git", Read().repository.url);
        }

        [Test]
        public void TheStarterSampleFolderExists()
        {
            Sample[] samples = Read().samples;
            Assert.AreEqual(1, samples.Length, "Expected exactly one sample.");
            Assert.AreEqual("Starter", samples[0].displayName);
            string folder = Path.Combine(PackageFiles.Root, samples[0].path);
            Assert.IsTrue(Directory.Exists(folder), $"Sample folder not found: {samples[0].path}");
        }

        [Test]
        public void DocumentationUrlNamesAFileInThePackage()
        {
            string url = Read().documentationUrl;
            int at = url.IndexOf(PackageFolderInRepo, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"Not a link into the package folder: {url}");
            string relative = url.Substring(at + PackageFolderInRepo.Length);
            StringAssert.StartsWith("Documentation~/", relative);
            Assert.IsTrue(File.Exists(Path.Combine(PackageFiles.Root, relative)), $"Linked file is missing: {relative}");
        }
    }
}
