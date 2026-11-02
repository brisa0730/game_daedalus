using System.Globalization;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The plain-words notices the editor tools show in help boxes, built from the same settings and box math the
    /// runtime uses (Architecture rule 3: the editor re-derives nothing). Editor-time only; never per frame.
    /// </summary>
    internal static class HelpTexts
    {
        /// <summary>Tiles a zone may reach past the trap's box (beyond the default growth) before the story warns it fires early.</summary>
        internal const float EarlyZoneThreshold = 1f;

        public const string CheckpointNotice = "Touching this checkpoint makes it the place the player comes back to.";
        public const string CheckpointNoEffect = "The Player's On hazard is Return to safe ground, so the player comes back where it last stood safely, not here. Set it to Respawn at checkpoint to use checkpoints.";
        public const string ExitNotice = "Touching this ends the game with a win screen.";
        public const string TrapPlayModeNote = "Trap changes take effect the next time you press Play.";

        /// <summary>The trap's story in one or more sentences, e.g. "Hidden until the player touches it, then it appears 0.5 s later."</summary>
        public static string TrapStory(Trap trap)
        {
            bool hazard = TrapLook.KindOf(trap.Tile) == TileKind.Hazard;
            string when = trap.Trigger == TrapTrigger.Touch ? "the player touches it" : "the player enters the zone";
            int ticks = LevelRules.SecondsToTicks(trap.Delay);
            string later = ticks == 0 ? "" : " " + Number(ticks / (float)FixedStepAccumulator.TicksPerSecond) + " s later";
            string story = trap.Action == TrapAction.Appear
                ? $"Hidden until {when}, then it appears{later}."
                : $"{(hazard ? "Spikes" : "Solid ground")} until {when}, then it vanishes{later}.";
            if (hazard)
            {
                story += " It's a spike trap: touching it while it's out kills.";
            }
            if (trap.Trigger == TrapTrigger.Zone)
            {
                string early = EarlyZone(trap);
                if (early != null)
                {
                    story += " " + early;
                }
            }
            return story;
        }

        // The side where the zone reaches furthest past the trap's box (the default upward growth not counted).
        static string EarlyZone(Trap trap)
        {
            Rect box = TrapLook.WorldBox(trap);
            Rect zone = TrapLook.WorldZone(trap);
            float left = box.xMin - zone.xMin;
            float right = zone.xMax - box.xMax;
            float below = box.yMin - zone.yMin;
            float above = zone.yMax - (box.yMax + LevelRules.DefaultZoneGrowUp);
            float most = Mathf.Max(Mathf.Max(left, right), Mathf.Max(below, above));
            if (!(most > EarlyZoneThreshold))
            {
                return null;
            }
            string side = most == left ? "left of" : most == right ? "right of" : most == below ? "below" : "above";
            return $"The zone reaches {Number(most)} tiles {side} the trap, so it fires before the player gets there.";
        }

        // "0.5", never "0,5": the texts read the same on every machine.
        static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
