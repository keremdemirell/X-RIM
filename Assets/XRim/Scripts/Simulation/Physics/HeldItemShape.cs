using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// Where a held item's box sits on its body, in the body's own frame (the grip at the origin, +X along the body's rotation),
    /// and which of its points follows the drawn path. Every engine and tool that builds, sweeps or reads a held item uses this,
    /// so they all agree.
    /// <list type="bullet">
    /// <item>A weapon or club: a box from the grip to the tip, as long as the weapon and as wide as its hit width (GDD §6). Its
    /// tip follows the path.</item>
    /// <item>The shield, held like a shield (designer, 2026-10-03, A3): a box centred on the grip, its ink thickness along the
    /// body's rotation (the way its face looks) and its length across it (the face's height). Its centre follows the path.</item>
    /// </list>
    /// </summary>
    public readonly struct HeldItemShape
    {
        /// <summary>The box's centre in the body's frame, arena units.</summary>
        public Vec2 CentreLocal { get; }

        /// <summary>The box's size: x along the body's rotation, y across it.</summary>
        public Vec2 SizeUnits { get; }

        /// <summary>The point that follows the drawn path: a weapon's tip, the shield's centre.</summary>
        public Vec2 PathPointLocal { get; }

        /// <summary>True for the shield: its face looks along the body's rotation.</summary>
        public bool IsShield { get; }

        private HeldItemShape(Vec2 centreLocal, Vec2 sizeUnits, Vec2 pathPointLocal, bool isShield)
        {
            CentreLocal = centreLocal;
            SizeUnits = sizeUnits;
            PathPointLocal = pathPointLocal;
            IsShield = isShield;
        }

        public static HeldItemShape Of(WeaponStats item)
        {
            Guard.NotNull(item, nameof(item));
            float length = item.LengthUnits;
            float width = item.InkThicknessUnits;
            return item.Kind == WeaponKind.Shield
                ? new HeldItemShape(Vec2.Zero, new Vec2(width, length), Vec2.Zero, true)
                : new HeldItemShape(new Vec2(length * 0.5f, 0f), new Vec2(length, width), new Vec2(length, 0f), false);
        }

        /// <summary>Half the box's smaller side: how far its surface is from its long centre line.</summary>
        public float HalfThicknessUnits => Math.Min(SizeUnits.X, SizeUnits.Y) * 0.5f;

        /// <summary>One end of the box's long centre line, in the body's frame.</summary>
        public Vec2 CentreLineStartLocal => CentreLocal - HalfLongAxis;

        /// <summary>The other end of the box's long centre line, in the body's frame.</summary>
        public Vec2 CentreLineEndLocal => CentreLocal + HalfLongAxis;

        /// <summary>Half the box's long side, as a vector in the body's frame.</summary>
        private Vec2 HalfLongAxis => SizeUnits.X >= SizeUnits.Y ? new Vec2(SizeUnits.X * 0.5f, 0f) : new Vec2(0f, SizeUnits.Y * 0.5f);

        /// <summary>A point given in the body's frame, in the arena, for the body at a pose.</summary>
        public static Vec2 ToArena(Vec2 local, BodyPose body) => body.PositionUnits + local.Rotated(body.RotationDegrees);

        /// <summary>Where the path point is in the arena when the body is at a pose.</summary>
        public Vec2 PathPointAt(BodyPose body) => ToArena(PathPointLocal, body);
    }
}
