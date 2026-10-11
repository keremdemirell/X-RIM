using System;
using XRim.Rules.Arena;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// One player's electric wall in a captured pose (GDD §13): whether it stands, how it looks (a warning or live) and where its face is
    /// along the arena X axis, so playback and the frozen board can draw it. The default is no wall. Like <c>FighterPose.SeveredParts</c>, the
    /// simulator marks it from the rules state; the rules' <see cref="ElectricWallState"/> stays the truth between turns.
    /// </summary>
    [Serializable]
    public struct ElectricWallPose
    {
        public bool IsUp;
        public ElectricWallPhase Phase;

        /// <summary>Where the wall's face stands, arena units; it stands behind its owner, on the side away from the opponent.</summary>
        public float XUnits;

        public ElectricWallPose(ElectricWallPhase phase, float xUnits)
        {
            IsUp = true;
            Phase = phase;
            XUnits = xUnits;
        }

        /// <summary>The wall as the rules state has it: standing where it stands, or none.</summary>
        public static ElectricWallPose Of(ElectricWallState wall) =>
            wall != null && wall.IsUp ? new ElectricWallPose(wall.Phase, wall.PositionUnits) : default;
    }
}
