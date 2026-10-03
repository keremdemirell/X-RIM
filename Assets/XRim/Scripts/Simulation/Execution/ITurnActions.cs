using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What a contact handler can read about each fighter's weapon during a turn, and what it can do to the fighters. Every
    /// action takes effect from <see cref="Now"/>, the start of the next physics step.
    /// </summary>
    public interface ITurnActions
    {
        SimTime Now { get; }

        /// <summary>The weapon a side holds this turn, its own stats; null when it holds nothing.</summary>
        WeaponStats HeldWeapon(Side side);

        /// <summary>The weapon's path speed this turn, with a body move's speed bonus (D13). 0 when it holds nothing.</summary>
        float WeaponSpeedUnitsPerSecond(Side side);

        /// <summary>The side's weapon was travelling its drawn path at this time (E2: only then does it land hits).</summary>
        bool IsWeaponTravelling(Side side, SimTime time);

        /// <summary>The weapon continues its path at this share of its own speed (D26).</summary>
        void SlowWeapon(Side side, float speedFraction);

        /// <summary>
        /// Ends the weapon's attack: a last hit recoils, an interrupt holds it where it is, a rebound bounces it back, a knock-off
        /// kicks it with <paramref name="knockVelocityUnitsPerSecond"/> and lets it fly free.
        /// </summary>
        void StopWeapon(Side side, WeaponStopKind kind, Vec2 knockVelocityUnitsPerSecond = default);

        /// <summary>Where the side's held item was at a time of the last few steps (interpolated): a shield's face at a block.</summary>
        BodyPose HeldItemPoseAt(Side side, SimTime time);

        void ApplyImpulse(Side side, BodyPart part, Vec2 impulse);

        /// <summary>Moves the dummy's standing point by this much over the knockback time; it stays there (E1).</summary>
        void KnockBack(Side side, Vec2 displacementUnits);
    }
}
