using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Simulation.Physics
{
    /// <summary>Every physical thing on the board at one instant. Velocities are not stored: a frozen board has none.</summary>
    [Serializable]
    public sealed class PoseSnapshot
    {
        public FighterPose Left = new FighterPose();
        public FighterPose Right = new FighterPose();
        public List<SeveredLimbPose> SeveredLimbs = new List<SeveredLimbPose>();

        /// <summary>Each player's electric wall (GDD §13), none by default.</summary>
        public ElectricWallPose LeftWall;
        public ElectricWallPose RightWall;

        public FighterPose Get(Side side) => side == Side.Left ? Left : Right;

        public ElectricWallPose GetWall(Side side) => side == Side.Left ? LeftWall : RightWall;

        public void SetWall(Side side, ElectricWallPose wall)
        {
            if (side == Side.Left) LeftWall = wall;
            else RightWall = wall;
        }

        public PoseSnapshot Clone() => new PoseSnapshot
        {
            Left = Left.Clone(),
            Right = Right.Clone(),
            SeveredLimbs = new List<SeveredLimbPose>(SeveredLimbs),
            LeftWall = LeftWall,
            RightWall = RightWall,
        };
    }
}
