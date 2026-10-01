using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The only door between gameplay and a physics engine. The Unity 2D implementation lives in
    /// XRim.Simulation.Unity2D (a hidden, manually stepped physics scene); tests use a fake. Another backend
    /// (e.g. for determinism or a non-Unity server) would implement this without touching the rules.
    /// All positions, velocities and accelerations are in arena units.
    /// </summary>
    public interface IPhysicsWorld : IDisposable
    {
        /// <summary>
        /// Rebuilds the board from a frozen pose, all at zero velocity (GDD §3 stance persistence): both ragdolls (limbs
        /// the state marks severed are left off), held items, the severed limbs lying in the arena, the floor and the arena
        /// edges. Each fighter holds <c>state.Fighters[side].CurrentWeapon</c> in its dominant hand (GDD §12) when its pose
        /// has one. Electric walls come in Session 12.
        /// </summary>
        void Load(PoseSnapshot pose, MatchState state, RulesSettings rules, SimulationSettings simulation, ArenaEdges edges);

        /// <summary>
        /// Gap at which the engine already counts two bodies as touching (its contact offset). Time-to-impact
        /// refinement treats a blade this close as touching.
        /// </summary>
        float TouchDistanceUnits { get; }

        /// <summary>Kinematic drive: the held item moves exactly onto this pose during the next step and pushes whatever is in the way.</summary>
        void SetHeldItemTarget(Side side, BodyPose target);

        /// <summary>
        /// Motor drive: the held item becomes a free physics body and is accelerated during the next step (mass-free
        /// accelerations; the world multiplies by mass and inertia). Zero means limp.
        /// </summary>
        void PushHeldItem(Side side, Vec2 accelerationUnitsPerSecondSquared, float angularAccelerationDegreesPerSecondSquared);

        BodyState GetHeldItemState(Side side);

        /// <summary>Where the dummy's root (the torso's pivot, the pelvis) should be this step: standing still or a body move.</summary>
        void SetRootTarget(Side side, BodyPose target);

        void Step(float deltaSeconds);

        /// <summary>Appends contacts that began during the last step, in a stable order.</summary>
        void DrainContacts(List<ContactFacts> into);

        /// <summary>Logic-driven sever: disables the limb's joint. Never physics breakForce (GDD §18; breakForce is Rejected).</summary>
        void BreakJoint(Side side, BodyPart part);

        void DropHeldItem(Side side);

        void ApplyImpulse(Side side, BodyPart part, Vec2 impulse);

        /// <summary>The body of a part; for a split limb (ten bodies), its upper segment.</summary>
        BodyPose GetPose(Side side, BodyPart part);

        PoseSnapshot CapturePose();

        bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond);
    }
}
