#if DAEDALUS_GALLERY_SPIKE
using UnityEngine;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Dev-only save probe for the class gallery spike (Documents/Plans/archive/class-gallery-spike.md). Compiled only into
    /// GallerySpikeBuild's WebGL builds: counts launches in PlayerPrefs and logs the count and the save path to the
    /// browser console, so the spike can see whether saves survive a reload and whether two games share them.
    /// </summary>
    static class SaveProbe
    {
        const string Key = "GallerySpike.Launches";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Count()
        {
            int launches = PlayerPrefs.GetInt(Key, 0) + 1;
            PlayerPrefs.SetInt(Key, launches);
            PlayerPrefs.Save();
            Debug.Log($"GallerySpike launches={launches} path={Application.persistentDataPath}");
        }
    }
}
#endif
