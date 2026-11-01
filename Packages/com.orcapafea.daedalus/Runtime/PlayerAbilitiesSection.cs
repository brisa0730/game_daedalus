using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The Player's Abilities: what it can do from the start, and the numbers those abilities use.
    /// The numbers stay visible with their toggle off until the editor-tools inspectors hide them (Architecture rule 8);
    /// until then each tooltip says when it applies.
    /// </summary>
    [Serializable]
    public sealed class PlayerAbilitiesSection
    {
        [SerializeField, Tooltip("On from the start: jump again in mid-air.")]
        bool doubleJump;

        [SerializeField, Tooltip("On from the start: slide down walls and jump off them.")]
        bool wallJump;

        [SerializeField, Tooltip("On from the start: cling to and climb Climbable tiles.")]
        bool wallClimb;

        [SerializeField, Tooltip("On from the start: grab ropes with Up or Down and climb them.")]
        bool ropeClimb;

        [SerializeField, Range(MovementSettings.MinAirJumps, MovementSettings.MaxAirJumps)]
        [Tooltip("How many extra jumps in mid-air. Used only when Double Jump is on.")]
        int airJumps = MovementSettings.DefaultAirJumps;

        [SerializeField, Range(MovementSettings.MinWallSlideSpeed, MovementSettings.MaxWallSlideSpeed)]
        [Tooltip("How fast the player slides down a wall, in tiles per second. Used only when Wall Jump is on.")]
        float wallSlideSpeed = MovementSettings.DefaultWallSlideSpeed;

        [SerializeField, Range(MovementSettings.MinClimbSpeed, MovementSettings.MaxClimbSpeed)]
        [Tooltip("How fast the player climbs walls and ropes, in tiles per second. Used only when Wall Climb or Rope Climb is on.")]
        float climbSpeed = MovementSettings.DefaultClimbSpeed;

        internal bool DoubleJump { get => doubleJump; set => doubleJump = value; }
        internal bool WallJump { get => wallJump; set => wallJump = value; }
        internal bool WallClimb { get => wallClimb; set => wallClimb = value; }
        internal bool RopeClimb { get => ropeClimb; set => ropeClimb = value; }
        internal int AirJumps { get => airJumps; set => airJumps = value; }
        internal float WallSlideSpeed { get => wallSlideSpeed; set => wallSlideSpeed = value; }
        internal float ClimbSpeed { get => climbSpeed; set => climbSpeed = value; }

        /// <summary>Jump, plus every ability switched on.</summary>
        internal Abilities ToAbilities()
        {
            Abilities abilities = Abilities.Jump;
            if (doubleJump)
            {
                abilities |= Abilities.DoubleJump;
            }
            if (wallJump)
            {
                abilities |= Abilities.WallJump;
            }
            if (wallClimb)
            {
                abilities |= Abilities.WallClimb;
            }
            if (ropeClimb)
            {
                abilities |= Abilities.RopeClimb;
            }
            return abilities;
        }
    }
}
