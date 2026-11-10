using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Guards on the Starter sample's files. A student's saved level holds instances of these prefabs, so a frozen piece
    /// (<c>StarterFrozen.txt</c>) must never change: a new behaviour is a new prefab under a new name.
    /// </summary>
    public class StarterSampleTests
    {
        static readonly Regex Guid = new Regex(@"^guid: ([0-9a-f]{32})", RegexOptions.Multiline);
        static readonly Regex SourcePrefab = new Regex(@"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})");

        static string Sample => Path.Combine(PackageFiles.Root, "Samples~", "Starter");

        static IEnumerable<string> FilesInSample(bool metas)
        {
            return Directory.GetFiles(Sample, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".meta", StringComparison.Ordinal) == metas);
        }

        static string RelativeToSample(string path) => path.Substring(Sample.Length + 1).Replace('\\', '/');

        static string GuidIn(string metaPath)
        {
            Match match = Guid.Match(File.ReadAllText(metaPath));
            Assert.IsTrue(match.Success, $"No GUID in {RelativeToSample(metaPath)}");
            return match.Groups[1].Value;
        }

        // Text is hashed with LF line endings, so a Windows checkout hashes the same as a Mac one.
        static string Sha256(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (!path.EndsWith(".png", StringComparison.Ordinal))
            {
                bytes = WithoutCarriageReturnsBeforeNewlines(bytes);
            }
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
        }

        static byte[] WithoutCarriageReturnsBeforeNewlines(byte[] bytes)
        {
            var kept = new List<byte>(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != (byte)'\r' || i + 1 >= bytes.Length || bytes[i + 1] != (byte)'\n')
                {
                    kept.Add(bytes[i]);
                }
            }
            return kept.ToArray();
        }

        [Test]
        public void EveryFileHasAMetaAndNoGuidRepeats()
        {
            foreach (string file in FilesInSample(metas: false))
            {
                Assert.IsTrue(File.Exists(file + ".meta"), $"No .meta for {RelativeToSample(file)}");
            }
            var seen = new Dictionary<string, string>();
            foreach (string meta in FilesInSample(metas: true))
            {
                string guid = GuidIn(meta);
                Assert.IsFalse(seen.ContainsKey(guid), $"{RelativeToSample(meta)} repeats the GUID of {(seen.TryGetValue(guid, out string other) ? other : "")}");
                seen[guid] = RelativeToSample(meta);
            }
        }

        [Test]
        public void EveryPrefabTheSceneUsesIsInTheSample()
        {
            var guids = new HashSet<string>(FilesInSample(metas: true).Select(GuidIn));
            foreach (string scene in new[] { "Starter.unity", "Stage Example/Stage 1.unity", "Stage Example/Stage 2.unity", "Trap Demo/Trap Demo.unity" })
            {
                MatchCollection used = SourcePrefab.Matches(File.ReadAllText(Path.Combine(Sample, scene)));
                Assert.IsNotEmpty(used, $"{scene} holds no prefab instances.");
                foreach (Match match in used)
                {
                    Assert.IsTrue(guids.Contains(match.Groups[1].Value), $"{scene} uses a prefab outside the sample: {match.Groups[1].Value}");
                }
            }
        }

        // A piece missing from the list ships unprotected, and a cut-down list would look clean to the test below.
        [Test]
        public void EveryPieceIsOnTheFrozenList()
        {
            var listed = new HashSet<string>(File.ReadAllLines(Path.Combine(PackageFiles.Root, "Tests", "EditMode", "StarterFrozen.txt"))
                .Where(line => line.Length > 0).Select(line => line.Split('\t')[0]));
            foreach (string file in Directory.GetFiles(Sample, "*", SearchOption.AllDirectories))
            {
                string relative = RelativeToSample(file);
                bool isFolderMeta = file.EndsWith(".meta", StringComparison.Ordinal) && Directory.Exists(file.Substring(0, file.Length - ".meta".Length));
                if (relative.StartsWith("Starter.unity", StringComparison.Ordinal) || relative.StartsWith("Stage Example", StringComparison.Ordinal) || relative.StartsWith("Trap Demo", StringComparison.Ordinal) || isFolderMeta)
                {
                    continue;
                }
                Assert.IsTrue(listed.Contains(relative), $"{relative} isn't on the frozen list. Run Tools/build_starter.py --refreeze.");
            }
        }

        [Test]
        public void FrozenPiecesKeepTheirGuidAndContent()
        {
            string[] rows = File.ReadAllLines(Path.Combine(PackageFiles.Root, "Tests", "EditMode", "StarterFrozen.txt"))
                .Where(line => line.Length > 0).ToArray();
            Assert.IsNotEmpty(rows, "The frozen list is empty.");
            foreach (string row in rows)
            {
                string[] fields = row.Split('\t');
                Assert.AreEqual(3, fields.Length, $"Bad row: {row}");
                string path = Path.Combine(Sample, fields[0]);
                Assert.IsTrue(File.Exists(path), $"A frozen piece is gone: {fields[0]}. Frozen pieces stay; add a new piece instead.");
                string meta = path.EndsWith(".meta", StringComparison.Ordinal) ? path : path + ".meta";
                Assert.AreEqual(fields[1], GuidIn(meta), $"{fields[0]} changed GUID.");
                Assert.AreEqual(fields[2], Sha256(path), $"{fields[0]} changed. A frozen piece never changes: a student's saved level inherits from it. Make a new prefab under a new name.");
            }
        }
    }
}
