using System;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The fields of an .asmdef file the guard tests read (via JsonUtility).</summary>
    [Serializable]
    class AsmdefData
    {
        public string name;
        public string[] references = Array.Empty<string>();
        public string[] includePlatforms = Array.Empty<string>();
        public bool noEngineReferences;
    }
}
