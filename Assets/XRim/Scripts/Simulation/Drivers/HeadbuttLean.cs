using System;
using XRim.Core;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// How an armless dummy's body carries its head to the drawn point (GDD §12 default: the path steers the head). The torso is wider
    /// than the head, so an upright body would meet the opponent chest first: the dummy instead leans at the pelvis toward the point
    /// and shifts along the floor just enough that its head, tilted with the torso, lands on it. Leaning forward, the head leads.
    /// All in the path's torso frame (pelvis at the origin, +X toward the opponent, upright). A point higher than the head can reach
    /// standing is reached at that height; the torso never bends past horizontal, so a point below the pelvis is reached at its height.
    /// Not a GDD rule: how the body follows the head is a feel choice (Session 11), to confirm at PT5.
    /// </summary>
    public static class HeadbuttLean
    {
        /// <summary>The torso bends at most to horizontal.</summary>
        private const float MaxLeanDegrees = 90f;

        /// <param name="headCentreLocal">Where the drawn path puts the head's centre.</param>
        /// <param name="torsoHeightUnits">Pelvis to neck.</param>
        /// <param name="headRadiusUnits">Neck to the head's centre.</param>
        /// <param name="leanDegrees">The torso's lean toward the opponent (negative: away), 0 upright.</param>
        /// <param name="forwardShiftUnits">How far the pelvis moves toward the opponent along the floor.</param>
        public static void Solve(Vec2 headCentreLocal, float torsoHeightUnits, float headRadiusUnits, out float leanDegrees,
            out float forwardShiftUnits)
        {
            float reach = torsoHeightUnits + headRadiusUnits;
            float height = XMath.Clamp(headCentreLocal.Y, 0f, reach);
            float lean = reach > 0f ? Math.Min(MaxLeanDegrees, (float)Math.Acos(height / reach) * XMath.RadiansToDegrees) : 0f;
            if (headCentreLocal.X < 0f) lean = -lean;
            leanDegrees = lean;
            forwardShiftUnits = headCentreLocal.X - reach * (float)Math.Sin(lean * XMath.DegreesToRadians);
        }

        /// <summary>The axis of a torso leaning this much, from the pelvis toward the neck (and on to the head's centre).</summary>
        public static Vec2 AxisLocal(float leanDegrees)
        {
            float radians = leanDegrees * XMath.DegreesToRadians;
            return new Vec2((float)Math.Sin(radians), (float)Math.Cos(radians));
        }
    }
}
