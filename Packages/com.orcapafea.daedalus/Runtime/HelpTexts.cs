using System.Globalization;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

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
        public const string ExitOnPrefabAsset = "This is the prefab, so a Next stage set here sends every placed copy to one scene. Set it on the placed Exit in each scene instead.";
        public const string ExitsDiffer = "The selected Exits go to different places.";
        public const string TrapPlayModeNote = "Trap changes take effect the next time you press Play.";
        public const string MoveStill = "Its Offset is 0 × 0, so it doesn't move.";

        /// <summary>A scene's name for the student: its file name, or "the first scene" when it was never saved.</summary>
        public static string StageName(string path)
        {
            return string.IsNullOrEmpty(path) ? "the first scene" : System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public static string StageLoadFailed(string stageName)
        {
            return $"Couldn't open {stageName}. Check that it is saved and still in the project.";
        }

        /// <summary>What touching this Exit does, in one sentence.</summary>
        public static string ExitStory(ExitLink link)
        {
            switch (link.Kind)
            {
                case ExitLink.LinkKind.Scene:
                    return $"Goes to {StageName(link.Path)}.";
                case ExitLink.LinkKind.Broken:
                    return "The next stage is missing: its scene was deleted, or moved outside Unity. Drag the scene in again.";
                default:
                    return "Ends the game with the win screen.";
            }
        }

        /// <summary>For the editor (which can't name the sim's types): <paramref name="player"/>'s settings, or the defaults without a Player.</summary>
        public static string TrapStoryInScene(Trap trap, Tilemap collision, Player player)
        {
            return TrapStory(trap, collision, TrapLook.SettingsOf(player));
        }

        /// <summary>
        /// The trap's story in one or more sentences, e.g. "Hidden until the player touches it, then it appears 0.5 s later."
        /// or "Slides 5 tiles right in 0.6 s after the player enters the zone." A Fall's drop and time come from
        /// <paramref name="collision"/> (the Collision Tilemap; none = nothing below) and <paramref name="settings"/> (the Player's).
        /// </summary>
        public static string TrapStory(Trap trap, Tilemap collision, MovementSettings settings)
        {
            bool onExit = TrapLook.IsOnExit(trap);
            bool hazard = !onExit && TrapLook.KindOf(trap.Tile) == TileKind.Hazard;
            string when = trap.Trigger == TrapTrigger.Touch ? "the player touches it" : "the player enters the zone";
            int delayTicks = LevelRules.SecondsToTicks(trap.Delay);
            string delay = delayTicks == 0 ? null : Number(Seconds(delayTicks)) + " s";
            string later = delay == null ? "" : " " + delay + " later";
            string after = delay == null ? $"after {when}" : $"{delay} after {when}";
            string story;
            switch (trap.Action)
            {
                case TrapAction.Appear:
                    story = onExit ? $"The exit is hidden until {when}, then appears{later}." : $"Hidden until {when}, then it appears{later}.";
                    break;
                case TrapAction.Move:
                    story = MoveStory(trap, onExit, delay, after);
                    break;
                case TrapAction.Fall:
                    story = FallStory(trap, onExit, collision, settings, delay, after);
                    break;
                default:
                    story = onExit
                        ? (delay == null ? $"The exit vanishes when {when}." : $"The exit vanishes {after}.")
                        : $"{(hazard ? "Spikes" : "Solid ground")} until {when}, then it vanishes{later}.";
                    break;
            }
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

        // "Slides 5 tiles right in 0.6 s after …"; on an Exit, "The exit runs 5 tiles right in 0.6 s after …".
        static string MoveStory(Trap trap, bool onExit, string delay, string after)
        {
            Vector2Int offset = TrapLook.ClampedOffset(trap);
            if (offset == Vector2Int.zero)
            {
                return MoveStill;
            }
            int ticks = TrapMotion.MoveTicks(offset.x, offset.y, TrapLook.ClampedSpeed(trap));
            string time = Seconds(ticks).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            int x = Mathf.Abs(offset.x);
            int y = Mathf.Abs(offset.y);
            string across = offset.x > 0 ? "right" : "left";
            string upDown = offset.y > 0 ? "up" : "down";
            string path;
            if (offset.y == 0)
            {
                path = onExit ? $"The exit runs {Tiles(x)} {across}" : $"Slides {Tiles(x)} {across}";
            }
            else if (offset.x == 0)
            {
                path = onExit ? $"The exit runs {Tiles(y)} {upDown}" : $"{(offset.y > 0 ? "Rises" : "Sinks")} {Tiles(y)}";
            }
            else
            {
                path = $"{(onExit ? "The exit runs" : "Moves")} {x} {across} and {y} {upDown}";
            }
            return $"{path} in {time}{(delay == null ? "" : ",")} {after}.";
        }

        static string FallStory(Trap trap, bool onExit, Tilemap collision, MovementSettings settings, string delay, string after)
        {
            string subject = onExit ? "The exit falls" : "Falls";
            if (!TrapLook.FallLanding(trap, collision, settings, out float tiles, out int ticks))
            {
                return $"{subject} out of the map {after}.";
            }
            if (ticks == 0)
            {
                return onExit ? "The exit is already resting on painted ground, so it won't fall." : "It's already resting on painted ground, so it won't fall.";
            }
            string time = Seconds(ticks).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            string drop = Mathf.Approximately(tiles, 1f) ? "1 tile" : Number(tiles) + " tiles";
            return $"{subject} {drop} in {time}{(delay == null ? "" : ",")} {after}.";
        }

        static string Tiles(int n)
        {
            return n == 1 ? "1 tile" : n + " tiles";
        }

        static float Seconds(int ticks)
        {
            return ticks / (float)FixedStepAccumulator.TicksPerSecond;
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
