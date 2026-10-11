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
        /// edges. When its pose has a held item, each fighter holds <c>state.Fighters[side].CurrentWeapon</c> (a weapon, shield or
        /// severed-limb club) at that pose, in the arm <c>FighterState.HeldItemArm</c> names: the dominant arm while intact, then
        /// the other (GDD §12). A body blow holds nothing (see <see cref="StrikeWithBodyPart"/>). A dropped item or a split torso
        /// in the pose is not rebuilt. Each electric wall the state has up (<c>state.Walls</c>, GDD §13) stands at its position.
        /// </summary>
        void Load(PoseSnapshot pose, MatchState state, RulesSettings rules, SimulationSettings simulation, ArenaEdges edges);

        /// <summary>
        /// A player's electric wall (GDD §13): a barrier across the arena standing on the floor, its face at <paramref name="xUnits"/> and
        /// its body behind it, away from the owner's opponent; null takes it away. A wall that was not standing appears there at once; a
        /// standing one moves there during the next step and pushes what is in its way (the advance). It collides like the arena edge and
        /// its contacts are reported, its owner's included (tagged <see cref="BodyRole.ElectricWall"/>, owned by its player).
        /// </summary>
        void SetElectricWall(Side owner, float? xUnits);

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

        /// <summary>
        /// The held item becomes a free (dynamic) body moving at this velocity, still the held item: a blade the rules stopped,
        /// handed to physics so its own mass hits (D1). Its driver keeps it in the hand with <see cref="PushHeldItem"/>.
        /// </summary>
        void ReleaseHeldItem(Side side, Vec2 velocityUnitsPerSecond, float angularVelocityDegreesPerSecond);

        BodyState GetHeldItemState(Side side);

        /// <summary>Where the dummy's root (the torso's pivot, the pelvis) should be this step: standing still or a body move.</summary>
        void SetRootTarget(Side side, BodyPose target);

        /// <summary>
        /// The angles a limb's joint motors turn toward, so a body move bends the legs (GDD §5): shoulder or hip, then elbow
        /// or knee, in the <see cref="LimbAngles"/> convention. After <see cref="Load"/> every limb holds its rest pose. The
        /// arm carrying the weapon follows the weapon instead.
        /// </summary>
        void SetLimbTarget(Side side, BodyPart limb, LimbAngles target);

        void Step(float deltaSeconds);

        /// <summary>Appends contacts that began during the last step, in a stable order.</summary>
        void DrainContacts(List<ContactFacts> into);

        /// <summary>
        /// Logic-driven sever: disables the limb's joint, so the limb falls and stays on the floor. Never physics breakForce (GDD §18;
        /// breakForce is Rejected). From then on <see cref="CapturePose"/> lists the limb in <c>PoseSnapshot.SeveredLimbs</c>.
        /// </summary>
        void BreakJoint(Side side, BodyPart part);

        /// <summary>
        /// The held item drops (the arm holding it was severed, GDD §12): it falls under physics, the hand lets go and it is no longer
        /// driven. <see cref="CapturePose"/> reports it as the fighter's dropped item. It is never thrown: dropping is the only release.
        /// </summary>
        void DropHeldItem(Side side);

        /// <summary>
        /// The dummy died (GDD §11): its joints go limp, its root lets go and its torso splits in two by logic (a pre-split torso, never a
        /// physics break). <see cref="CapturePose"/> reports the split.
        /// </summary>
        void SplitTorso(Side side);

        /// <summary>
        /// An armless body blow (GDD §12): from now this body part takes the held item's place. It is driven along the path in the held
        /// item convention (the head: <see cref="HeldItemShape.HeadAsItem"/>) and captured as the held item; its contacts are still its
        /// own. Call after <see cref="Load"/>, for a fighter whose weapon is a body blow.
        /// </summary>
        void StrikeWithBodyPart(Side side, BodyPart part);

        void ApplyImpulse(Side side, BodyPart part, Vec2 impulse);

        /// <summary>The body of a part; for a split limb (ten bodies), its upper segment.</summary>
        BodyPose GetPose(Side side, BodyPart part);

        PoseSnapshot CapturePose();

        bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond);
    }
}
