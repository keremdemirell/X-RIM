using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>Where a dummy stands when its body move begins: the frozen board it plans from (GDD §3).</summary>
    public readonly struct BodyMoveStart
    {
        public Side Side { get; }

        /// <summary>The turn-start root (<see cref="TurnStartRoot"/>): the frozen pelvis, upright.</summary>
        public BodyPose Root { get; }

        /// <summary>Where the front leg's sole is (<see cref="LegGeometry.FrontLeg"/>), arena units.</summary>
        public Vec2 FrontSoleUnits { get; }

        public Vec2 BackSoleUnits { get; }

        /// <summary>The pelvis height of this dummy standing straight; heights of moves are measured from it.</summary>
        public float StandingHeightUnits { get; }

        public BodyMoveStart(Side side, BodyPose root, Vec2 frontSoleUnits, Vec2 backSoleUnits, float standingHeightUnits)
        {
            Side = side;
            Root = root;
            FrontSoleUnits = frontSoleUnits;
            BackSoleUnits = backSoleUnits;
            StandingHeightUnits = standingHeightUnits;
        }

        /// <summary>The start of a dummy frozen in <paramref name="pose"/>; its front leg is on its dominant side.</summary>
        public static BodyMoveStart Of(Side side, FighterPose pose, Handedness handedness, RagdollSettings body) =>
            new BodyMoveStart(side, TurnStartRoot.Of(pose),
                LegGeometry.SoleOf(pose, LegGeometry.FrontLeg(handedness), body),
                LegGeometry.SoleOf(pose, LegGeometry.BackLeg(handedness), body),
                LegGeometry.StandingPelvisHeightUnits(body));
    }
}
