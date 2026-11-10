using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

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
            new Regex(@"\bSystem\s*\.\s*Linq\b"),
        };

        // Strings and comments are blanked together, so "//" inside a string can't hide the rest of a line.
        internal static readonly Regex StringsAndComments = new Regex(
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
            Assert.IsNotEmpty(FindForbidden("using System.Linq;"));
            Assert.IsEmpty(FindForbidden("// Random is fine in a comment\n/* so is DateTime */ int x = 1;"));
        }

        // Static mutable state is a singleton (CLAUDE.md Architecture rule 5). Compiler-generated types are
        // skipped on purpose: their lambda caches hold no game state, and lambdas in Step are banned by review.
        [Test]
        public void SimHasNoStaticMutableState()
        {
            var hits = new List<string>();
            int checkedFields = 0;
            foreach (Type type in typeof(FixedStepAccumulator).Assembly.GetTypes())
            {
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    continue;
                }
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (FieldInfo field in type.GetFields(flags))
                {
                    checkedFields++;
                    bool immutable = field.IsLiteral
                        || (field.IsInitOnly && (field.FieldType.IsValueType || field.FieldType == typeof(string)));
                    if (!immutable)
                    {
                        hits.Add($"{type.FullName}.{field.Name}");
                    }
                }
            }
            Assert.That(checkedFields, Is.GreaterThan(0), "The scan found no static fields; it would pass without checking anything.");
            Assert.IsEmpty(hits, "Static mutable fields in the sim (only const, or static readonly of a value type or string, is allowed):\n"
                + string.Join("\n", hits));
        }

        static IEnumerable<string> FindForbidden(string source)
        {
            string code = StringsAndComments.Replace(source, " ");
            return Forbidden.Where(pattern => pattern.IsMatch(code)).Select(pattern => pattern.ToString());
        }
    }
}
