using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>The Player's Movement settings. Initial values and ranges come from <see cref="MovementSettings"/>.</summary>
    [Serializable]
    public sealed class PlayerMovementSection
    {
        [SerializeField, Range(MovementSettings.MinRunSpeed, MovementSettings.MaxRunSpeed)]
        [Tooltip("How fast the player runs, in tiles per second.")]
        float runSpeed = MovementSettings.DefaultRunSpeed;

        [SerializeField, Range(MovementSettings.MinTimeToFullSpeed, MovementSettings.MaxTimeToFullSpeed)]
        [Tooltip("Seconds from standing still to full run speed. 0 = instant.")]
        float timeToFullSpeed = MovementSettings.DefaultTimeToFullSpeed;

        [SerializeField, Range(MovementSettings.MinJumpHeight, MovementSettings.MaxJumpHeight)]
        [Tooltip("How high a full jump goes, in tiles. Hold Jump for a full jump, tap it for a short hop.")]
        float jumpHeight = MovementSettings.DefaultJumpHeight;

        [SerializeField, Range(MovementSettings.MinGravity, MovementSettings.MaxGravity)]
        [Tooltip("1 = normal. Higher = snappier jumps and falls. Jump height stays the same.")]
        float gravity = MovementSettings.DefaultGravity;

        [SerializeField, Range(MovementSettings.MinMaxFallSpeed, MovementSettings.MaxMaxFallSpeed)]
        [Tooltip("The fastest the player can fall, in tiles per second.")]
        float maxFallSpeed = MovementSettings.DefaultMaxFallSpeed;

        internal float RunSpeed { get => runSpeed; set => runSpeed = value; }
        internal float TimeToFullSpeed { get => timeToFullSpeed; set => timeToFullSpeed = value; }
        internal float JumpHeight { get => jumpHeight; set => jumpHeight = value; }
        internal float Gravity { get => gravity; set => gravity = value; }
        internal float MaxFallSpeed { get => maxFallSpeed; set => maxFallSpeed = value; }
    }
}
