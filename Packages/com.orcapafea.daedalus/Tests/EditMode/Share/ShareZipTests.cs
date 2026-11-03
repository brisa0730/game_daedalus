using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEngine;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The Share zip's layout: six root entries named with '/', compression per entry, daedalus.json, safe replace.</summary>
    public class ShareZipTests
    {
        static readonly string[] Expected =
        {
            "daedalus.json", "screenshot.png", "Build/game.loader.js", "Build/game.data.br", "Build/game.framework.js.br",
            "Build/game.wasm.br",
        };

        string root;
        string buildDir;
        string zipPath;
        readonly byte[] png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

        [SetUp]
        public void FakeBuild()
        {
            root = Path.Combine(Path.GetTempPath(), "daedalus-share-zip-" + Guid.NewGuid().ToString("N"));
            buildDir = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(buildDir, "Build"));
            Directory.CreateDirectory(Path.Combine(buildDir, "TemplateData"));
            for (int i = 2; i < Expected.Length; i++)
                File.WriteAllBytes(Path.Combine(buildDir, Expected[i]), Encoding.ASCII.GetBytes("bytes of " + Expected[i]));
            File.WriteAllText(Path.Combine(buildDir, "index.html"), "<html>");
            File.WriteAllText(Path.Combine(buildDir, "TemplateData", "style.css"), "body{}");
            File.WriteAllText(Path.Combine(buildDir, "Build", "game.symbols.json.br"), "junk");
            zipPath = Path.Combine(root, "My Game.zip");
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void ExactlyTheSixEntriesWithSlashNames()
        {
            ShareZip.Write(zipPath, "Spike Hell", png, buildDir, long.MaxValue);
            using (ZipArchive zip = ZipFileOpen())
            {
                var names = new List<string>();
                foreach (ZipArchiveEntry entry in zip.Entries)
                    names.Add(entry.FullName);
                CollectionAssert.AreEqual(Expected, names);
            }
        }

        [Test]
        public void EveryEntryIsDeflated()
        {
            ShareZip.Write(zipPath, "Spike Hell", png, buildDir, long.MaxValue);
            Dictionary<string, int> methods = CentralDirectoryMethods(File.ReadAllBytes(zipPath));
            // Recorded behaviour of Mono's ZipArchive (6000.6.7f1): CompressionLevel.NoCompression still writes method 8
            // (Deflate at level 0), never Stored (0). The gallery accepts both; this pins what the editor really writes.
            CollectionAssert.AreEquivalent(Expected, methods.Keys);
            foreach (KeyValuePair<string, int> entry in methods)
                Assert.AreEqual(8, entry.Value, entry.Key);
        }

        [Test]
        public void MetadataIsFormatOneWithTheTitleAndNoBom()
        {
            const string title = "함정 지옥 😀";
            ShareZip.Write(zipPath, title, png, buildDir, long.MaxValue);
            byte[] json = EntryBytes("daedalus.json");
            Assert.IsFalse(json.Length >= 3 && json[0] == 0xEF && json[1] == 0xBB && json[2] == 0xBF, "no BOM");
            var metadata = JsonUtility.FromJson<MetadataProbe>(new UTF8Encoding(false).GetString(json));
            Assert.AreEqual(1, metadata.format);
            Assert.AreEqual(title, metadata.title);
        }

        [Test]
        public void EveryEntryRoundTrips()
        {
            ShareZip.Write(zipPath, "Spike Hell", png, buildDir, long.MaxValue);
            CollectionAssert.AreEqual(png, EntryBytes("screenshot.png"));
            for (int i = 2; i < Expected.Length; i++)
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(buildDir, Expected[i])), EntryBytes(Expected[i]));
        }

        [Test]
        public void AnEarlierZipIsReplaced()
        {
            File.WriteAllText(zipPath, "old");
            long length = ShareZip.Write(zipPath, "Spike Hell", png, buildDir, long.MaxValue);
            Assert.AreEqual(new FileInfo(zipPath).Length, length);
            Assert.AreNotEqual("old", File.ReadAllText(zipPath));
            Assert.IsFalse(File.Exists(zipPath + ".partial"));
        }

        [Test]
        public void AnOversizeZipLeavesTheEarlierOneUntouched()
        {
            File.WriteAllText(zipPath, "old");
            long length = ShareZip.Write(zipPath, "Spike Hell", png, buildDir, 10);
            Assert.Greater(length, 10);
            Assert.AreEqual("old", File.ReadAllText(zipPath));
            Assert.IsFalse(File.Exists(zipPath + ".partial"));
        }

        [Test]
        public void AMissingBuildFileThrowsAndLeavesNothing()
        {
            File.Delete(Path.Combine(buildDir, "Build", "game.wasm.br"));
            Assert.IsFalse(ShareZip.HasBuildFiles(buildDir));
            Assert.Throws<FileNotFoundException>(() => ShareZip.Write(zipPath, "Spike Hell", png, buildDir, long.MaxValue));
            Assert.IsFalse(File.Exists(zipPath));
            Assert.IsFalse(File.Exists(zipPath + ".partial"));
        }

        [Serializable]
        sealed class MetadataProbe
        {
            public int format;
            public string title;
        }

        ZipArchive ZipFileOpen()
        {
            return new ZipArchive(new FileStream(zipPath, FileMode.Open, FileAccess.Read), ZipArchiveMode.Read);
        }

        byte[] EntryBytes(string name)
        {
            using (ZipArchive zip = ZipFileOpen())
            using (Stream entry = zip.GetEntry(name).Open())
            using (var copy = new MemoryStream())
            {
                entry.CopyTo(copy);
                return copy.ToArray();
            }
        }

        /// <summary>Each entry's compression method, read from the central directory headers (signature 0x02014b50).</summary>
        static Dictionary<string, int> CentralDirectoryMethods(byte[] zip)
        {
            var methods = new Dictionary<string, int>();
            for (int i = 0; i + 46 <= zip.Length; i++)
            {
                if (BitConverter.ToUInt32(zip, i) != 0x02014b50)
                    continue;
                int method = BitConverter.ToUInt16(zip, i + 10);
                int nameLength = BitConverter.ToUInt16(zip, i + 28);
                int extraLength = BitConverter.ToUInt16(zip, i + 30);
                int commentLength = BitConverter.ToUInt16(zip, i + 32);
                methods[Encoding.UTF8.GetString(zip, i + 46, nameLength)] = method;
                i += 45 + nameLength + extraLength + commentLength;
            }
            return methods;
        }
    }
}
