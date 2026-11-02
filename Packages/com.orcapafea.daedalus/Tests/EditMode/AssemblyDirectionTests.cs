using NUnit.Framework;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>CLAUDE.md Architecture rule 1: editor tools → runtime → sim, never backwards.</summary>
    public class AssemblyDirectionTests
    {
        const string Sim = "Orcapafea.Daedalus.Sim";
        const string Runtime = "Orcapafea.Daedalus";
        const string Editor = "Orcapafea.Daedalus.Editor";
        const string InputSystem = "Unity.InputSystem";
        const string UI = "UnityEngine.UI";

        [Test]
        public void SimReferencesNothing()
        {
            Assert.IsEmpty(PackageFiles.ReadAsmdef(PackageFiles.SimAsmdef).references);
        }

        [Test]
        public void RuntimeReferencesOnlyTheSimTheInputSystemAndUI()
        {
            AsmdefData runtime = PackageFiles.ReadAsmdef(PackageFiles.RuntimeAsmdef);
            Assert.AreEqual(Runtime, runtime.name);
            Assert.That(runtime.references, Is.SubsetOf(new[] { Sim, InputSystem, UI }));
            foreach (string reference in runtime.references)
            {
                Assert.AreNotEqual(Editor, reference, "The runtime must never reference the editor tools.");
                Assert.IsFalse(reference.EndsWith(".Editor"), $"The runtime references an editor assembly: {reference}");
            }
            Assert.IsEmpty(runtime.includePlatforms, "The runtime must build for every platform.");
        }

        [Test]
        public void EditorToolsReferenceOnlyRuntimeAndSimAndAreEditorOnly()
        {
            AsmdefData editor = PackageFiles.ReadAsmdef(PackageFiles.EditorAsmdef);
            Assert.AreEqual(Editor, editor.name);
            Assert.That(editor.references, Is.SubsetOf(new[] { Runtime, Sim }));
            Assert.That(editor.includePlatforms, Is.EqualTo(new[] { "Editor" }));
        }
    }
}
