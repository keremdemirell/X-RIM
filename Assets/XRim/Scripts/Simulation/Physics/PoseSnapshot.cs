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

        public FighterPose Get(Side side) => side == Side.Left ? Left : Right;

        public PoseSnapshot Clone() => new PoseSnapshot
        {
            Left = Left.Clone(),
            Right = Right.Clone(),
            SeveredLimbs = new List<SeveredLimbPose>(SeveredLimbs),
        };
    }
}
