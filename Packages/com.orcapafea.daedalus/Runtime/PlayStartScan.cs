using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The one play-start scan for placed Daedalus components: the documented exception to Architecture rule 5
    /// (no <c>Find</c>-style lookups). Callers sort the result deterministically. Never called per frame.
    /// </summary>
    internal static class PlayStartScan
    {
        /// <summary>Every active and enabled <typeparamref name="T"/> in the loaded scenes, in no particular order.</summary>
        public static T[] FindActive<T>() where T : Behaviour
        {
#if UNITY_6000_6_OR_NEWER
            // 6000.6 deprecates the FindObjectsSortMode overloads (framework.md).
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
#else
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#endif
            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].isActiveAndEnabled)
                {
                    count++;
                }
            }
            var active = new T[count];
            count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].isActiveAndEnabled)
                {
                    active[count++] = found[i];
                }
            }
            return active;
        }
    }
}
