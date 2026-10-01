using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Where a dummy's root (the pelvis, the origin of the torso frame paths are drawn in) stands when a turn starts:
    /// the frozen pelvis position, upright (approved with the Session 04 plan, 2026-10-01). A dummy that ended the last
    /// turn tilted straightens up during the next one instead of keeping the lean forever, and paths are drawn in an
    /// upright frame, so what is drawn is what the weapon does even while the body reels. Planning (the weapon tip a
    /// stroke starts from) and execution use this same frame.
    /// </summary>
    public static class TurnStartRoot
    {
        public const float UprightDegrees = 0f;

        public static BodyPose Of(FighterPose pose) => new BodyPose(pose.Get(BodyPart.Torso).PositionUnits, UprightDegrees);
    }
}
