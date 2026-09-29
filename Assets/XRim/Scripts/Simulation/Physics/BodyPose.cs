using System;
using XRim.Core;

namespace XRim.Simulation.Physics
{
    /// <summary>Position and rotation of one physics body in arena units. Serializable for scenario files.</summary>
    [Serializable]
    public struct BodyPose
    {
        public Vec2 PositionUnits;
        public float RotationDegrees;

        public BodyPose(Vec2 positionUnits, float rotationDegrees)
        {
            PositionUnits = positionUnits;
            RotationDegrees = rotationDegrees;
        }
    }
}
