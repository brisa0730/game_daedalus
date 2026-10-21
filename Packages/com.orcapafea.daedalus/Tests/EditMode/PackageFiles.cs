using System.IO;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Finds the package's files on disk, wherever it is installed (embedded, file: or git).</summary>
    static class PackageFiles
    {
        public const string SimAsmdef = "Runtime/Sim/Orcapafea.Daedalus.Sim.asmdef";
        public const string RuntimeAsmdef = "Runtime/Orcapafea.Daedalus.asmdef";
        public const string EditorAsmdef = "Editor/Orcapafea.Daedalus.Editor.asmdef";

        public static string Root
        {
            get
            {
                PackageInfo info = PackageInfo.FindForAssembly(typeof(FixedStepAccumulator).Assembly);
                Assert.IsNotNull(info, "The sim assembly does not belong to a package.");
                Assert.IsTrue(Directory.Exists(info.resolvedPath), $"Package folder not found: {info.resolvedPath}");
                return info.resolvedPath;
            }
        }

        public static string Read(string relativePath)
        {
            string path = Path.Combine(Root, relativePath);
            Assert.IsTrue(File.Exists(path), $"Expected file is missing: {relativePath}");
            return File.ReadAllText(path);
        }

        public static AsmdefData ReadAsmdef(string relativePath)
        {
            return JsonUtility.FromJson<AsmdefData>(Read(relativePath));
        }
    }
}
