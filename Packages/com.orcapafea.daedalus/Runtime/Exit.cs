using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Touching it goes to the next stage, or ends the game with a win screen when there is none. Its box is its sprite;
    /// without a sprite, one tile around its position. Read once at play start.
    /// </summary>
    [AddComponentMenu("Daedalus/Exit")]
    [DisallowMultipleComponent]
    public sealed class Exit : MonoBehaviour
    {
        internal const int CurrentDataVersion = 1;

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        // The scene's asset GUID: it survives a rename or a move, and "empty" is data rather than a missing reference.
        [SerializeField, Tooltip("The scene the player goes to from this Exit. Leave it empty to end the game with the win screen.")]
        string nextStage = "";

        // Written only into a build's copy of the scene (StagePathStamp): a build has no asset database.
        [SerializeField, HideInInspector]
        string builtNextStagePath = "";

        internal int DataVersion => dataVersion;

        internal string NextStageGuid
        {
            get => nextStage;
            set => nextStage = value;
        }

        /// <summary>Where this Exit leads.</summary>
        internal ExitLink Link => StageLoad.Resolve(nextStage, builtNextStagePath);

        internal string BuiltNextStagePath => builtNextStagePath;

        internal void StampBuiltPath(string path)
        {
            builtNextStagePath = path;
        }

        /// <summary>The box the player touches, in world space.</summary>
        internal Rect WorldBox => LevelCollector.ElementBox(this);

        void OnDrawGizmos()
        {
            Rect box = WorldBox;
            Gizmos.color = new Color(0.4f, 1f, 0.5f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
