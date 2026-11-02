using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>The Player's Health: what happens when it touches spikes, falls into a pit or is crushed.</summary>
    [Serializable]
    public sealed class PlayerHealthSection
    {
        [SerializeField, Tooltip("What happens when the player touches spikes, falls into a pit or is crushed. Return to safe ground: back to where it last stood safely. Respawn at checkpoint: back to the last checkpoint it touched, or the start.")]
        OnHazard onHazard = OnHazard.ReturnToSafeGround;

        internal OnHazard OnHazard { get => onHazard; set => onHazard = value; }

        internal RespawnRule ToRule()
        {
            return onHazard == OnHazard.RespawnAtCheckpoint ? RespawnRule.Checkpoint : RespawnRule.SafeGround;
        }
    }
}
