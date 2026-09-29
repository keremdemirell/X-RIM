using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The only door between gameplay and a physics engine. The Unity 2D implementation lives in
    /// XRim.Simulation.Unity2D (a hidden, manually stepped physics scene); tests use a fake. Another backend
    /// (e.g. for determinism or a non-Unity server) would implement this without touching the rules.
    /// </summary>
    public interface IPhysicsWorld : IDisposable
    {
        /// <summary>Rebuilds ragdolls, held items, walls and severed limbs from a frozen board, all at zero velocity.</summary>
        void Load(PoseSnapshot pose, MatchState state, RulesSettings settings);

        /// <summary>Where the held item should be this step. The weapon driver decides how it gets there.</summary>
        void SetHeldItemTarget(Side side, BodyPose target);

        /// <summary>Where the dummy's root should be this step (body move).</summary>
        void SetRootTarget(Side side, BodyPose target);

        void Step(float deltaSeconds);

        /// <summary>Appends contacts that began during the last step, in a stable order.</summary>
        void DrainContacts(List<ContactFacts> into);

        /// <summary>Logic-driven sever: disables the limb's joint. Never physics breakForce (GDD §18; breakForce is Rejected).</summary>
        void BreakJoint(Side side, BodyPart part);

        void DropHeldItem(Side side);

        void ApplyImpulse(Side side, BodyPart part, Vec2 impulse);

        BodyPose GetPose(Side side, BodyPart part);

        PoseSnapshot CapturePose();

        bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond);
    }
}
