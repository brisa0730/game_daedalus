using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>CLAUDE.md Architecture rule 2: the sim never touches Unity, the editor, the clock or randomness.</summary>
    public class SimPurityTests
    {
        static readonly Regex[] Forbidden =
        {
            new Regex(@"\bUnityEngine\b"),
            new Regex(@"\bUnityEditor\b"),
            new Regex(@"\bDateTime(Offset)?\b"),
            new Regex(@"\bStopwatch\b"),
            new Regex(@"\bRandom\w*"),
            new Regex(@"\bGuid\s*\.\s*NewGuid\b"),
            new Regex(@"\bEnvironment\b"),
            new Regex(@"\bTickCount"),
        };

        // Strings and comments are blanked together, so "//" inside a string can't hide the rest of a line.
        static readonly Regex StringsAndComments = new Regex(
            @"@""(?:[^""]|"""")*""|""(?:\\.|[^\\""\n])*""|'(?:\\.|[^\\'\n])+'|//[^\n]*|/\*.*?\*/",
            RegexOptions.Singleline);

        [Test]
        public void SimAssemblyReferencesNothingAndNoEngine()
        {
            AsmdefData sim = PackageFiles.ReadAsmdef(PackageFiles.SimAsmdef);
            Assert.AreEqual("Orcapafea.Daedalus.Sim", sim.name);
            Assert.IsEmpty(sim.references, "The sim assembly must not reference other assemblies.");
            Assert.IsTrue(sim.noEngineReferences, "The sim assembly must set noEngineReferences.");
        }

        [Test]
        public void SimSourceUsesNothingForbidden()
        {
            string simFolder = Path.Combine(PackageFiles.Root, "Runtime", "Sim");
            string[] files = Directory.GetFiles(simFolder, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Select(Path.GetFileName), Does.Contain("FixedStepAccumulator.cs"),
                "The scan found no sim source; it would pass without checking anything.");

            var hits = new List<string>();
            foreach (string file in files)
            {
                foreach (string token in FindForbidden(File.ReadAllText(file)))
                {
                    hits.Add($"{Path.GetFileName(file)}: {token}");
                }
            }
            Assert.IsEmpty(hits, "Forbidden use in the sim:\n" + string.Join("\n", hits));
        }

        [Test]
        public void ScannerCatchesForbiddenUseButIgnoresComments()
        {
            Assert.IsNotEmpty(FindForbidden("var r = new System.Random(1);"));
            Assert.IsNotEmpty(FindForbidden("using UnityEngine;"));
            Assert.IsNotEmpty(FindForbidden("var now = DateTimeOffset.UtcNow;"));
            Assert.IsNotEmpty(FindForbidden("var url = \"http://x\"; var t = Environment.TickCount;"));
            Assert.IsEmpty(FindForbidden("// Random is fine in a comment\n/* so is DateTime */ int x = 1;"));
        }

        static IEnumerable<string> FindForbidden(string source)
        {
            string code = StringsAndComments.Replace(source, " ");
            return Forbidden.Where(pattern => pattern.IsMatch(code)).Select(pattern => pattern.ToString());
        }
    }
}
