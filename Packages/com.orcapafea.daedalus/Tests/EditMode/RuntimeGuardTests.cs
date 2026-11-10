using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>CLAUDE.md Architecture rules 1 and 5, for the runtime assembly.</summary>
    public class RuntimeGuardTests
    {
        // Looser than the sim's rule on purpose: a static readonly lookup table (PlayHud.Skull) is fine here.
        [Test]
        public void TheRuntimeHasNoStaticMutableFields()
        {
            var hits = new List<string>();
            int checkedFields = 0;
            foreach (Type type in typeof(Player).Assembly.GetTypes())
            {
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    continue;
                }
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (FieldInfo field in type.GetFields(flags))
                {
                    checkedFields++;
                    if (!field.IsLiteral && !field.IsInitOnly && !field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    {
                        hits.Add($"{type.FullName}.{field.Name}");
                    }
                }
            }
            Assert.That(checkedFields, Is.GreaterThan(0), "The scan found no static fields; it would pass without checking anything.");
            Assert.IsEmpty(hits, "Static mutable fields in the runtime (const or readonly only):\n" + string.Join("\n", hits));
        }

        [Test]
        public void OnlyStageLoadNamesTheEditorInTheRuntime()
        {
            string runtime = Path.Combine(PackageFiles.Root, "Runtime");
            string[] files = Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Select(Path.GetFileName), Does.Contain("Player.cs"), "The scan found no runtime source; it would pass without checking anything.");
            var editor = new Regex(@"\bUnityEditor\b");
            string[] naming = files
                .Where(file => editor.IsMatch(SimPurityTests.StringsAndComments.Replace(File.ReadAllText(file), " ")))
                .Select(Path.GetFileName)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "StageLoad.cs" }, naming);
        }
    }
}
