namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Death, respawn and trap numbers (CLAUDE.md § Tunable defaults). Each is defined only here; entities.md
    /// mirrors them. Times are authored in seconds and converted to 60 Hz ticks.
    /// </summary>
    public static class LevelRules
    {
        /// <summary>Seconds from a death to the player reappearing.</summary>
        public const float DeathBeatSeconds = 0.4f;
        public const int DeathBeatTicks = (int)(DeathBeatSeconds * FixedStepAccumulator.TicksPerSecond + 0.5f);

        /// <summary>Tiles taken off each side of the player's hitbox to make the hazard core: grazing spikes is safe.</summary>
        public const float HazardInset = 0.2f;
        /// <summary>Tiles the player's hitbox grows by for "touches" (Touch triggers, checkpoints, exits).</summary>
        public const float ContactMargin = 0.0625f;
        /// <summary>Tiles; a trap's default zone is its box grown upward by this.</summary>
        public const float DefaultZoneGrowUp = 1f;

        /// <summary>The Trap's Delay setting, in seconds.</summary>
        public const float DefaultTrapDelay = 0f;
        public const float MinTrapDelay = 0f;
        public const float MaxTrapDelay = 3f;

        /// <summary>The Trap's Size setting, in whole tiles.</summary>
        public const int DefaultTrapWidth = 3;
        public const int DefaultTrapHeight = 1;
        public const int MinTrapSize = 1;
        public const int MaxTrapSize = 30;

        /// <summary>Seconds the win screen ignores input before a key plays again.</summary>
        public const float WinInputLockSeconds = 1f;
        public const int WinInputLockTicks = (int)(WinInputLockSeconds * FixedStepAccumulator.TicksPerSecond + 0.5f);

        /// <summary>Seconds to whole ticks, rounded as <see cref="MovementInternals"/> does; negative or non-finite gives 0.</summary>
        public static int SecondsToTicks(float seconds)
        {
            if (!(seconds > 0) || float.IsInfinity(seconds))
            {
                return 0;
            }
            return (int)(seconds * FixedStepAccumulator.TicksPerSecond + 0.5f);
        }

        /// <summary>The default zone of a trap whose box is <paramref name="trapBox"/>: the box grown upward.</summary>
        public static Box DefaultZone(Box trapBox)
        {
            return new Box(trapBox.MinX, trapBox.MinY, trapBox.MaxX, trapBox.MaxY + DefaultZoneGrowUp);
        }
    }
}
