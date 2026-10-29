using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Planning commands as a player made them (GDD §18): a stroke travels as its raw points, exactly, so the server builds the same path
    /// the phone previewed, and never as a built path. A command type without a form here cannot be sent; <see cref="SupportedTypes"/> lets
    /// a test catch a new one.
    /// </summary>
    internal static class CommandCodec
    {
        private const byte SelectWeapon = 1;
        private const byte SetBodyMove = 2;
        private const byte SetPath = 3;
        private const byte ClearPath = 4;
        private const byte SetReady = 5;
        private const byte UseSignature = 6;

        /// <summary>A stroke point is two exact floats.</summary>
        private const int BytesPerStrokePoint = 8;

        public static IReadOnlyList<Type> SupportedTypes { get; } = new[]
        {
            typeof(SelectWeaponCommand), typeof(SetBodyMoveCommand), typeof(SetPathCommand), typeof(ClearPathCommand),
            typeof(SetReadyCommand), typeof(UseSignatureCommand),
        };

        public static void Write(PacketWriter writer, PlanningCommand command)
        {
            switch (command)
            {
                case SelectWeaponCommand select:
                    writer.WriteByte(SelectWeapon);
                    BoardCodec.WriteWeapon(writer, select.Weapon);
                    break;
                case SetBodyMoveCommand body:
                    writer.WriteByte(SetBodyMove);
                    writer.WriteInt((int)body.Move);
                    break;
                case SetPathCommand path:
                    writer.WriteByte(SetPath);
                    WriteStroke(writer, path.Path != null ? path.Path.Points : (IReadOnlyList<Vec2>)Array.Empty<Vec2>());
                    break;
                case ClearPathCommand _:
                    writer.WriteByte(ClearPath);
                    break;
                case SetReadyCommand ready:
                    writer.WriteByte(SetReady);
                    writer.WriteBool(ready.IsReady);
                    break;
                case UseSignatureCommand signature:
                    writer.WriteByte(UseSignature);
                    BoardCodec.WriteSignature(writer, signature.Signature);
                    break;
                default:
                    throw new NotSupportedException($"The planning command {command.GetType().Name} has no protocol form: add it to {nameof(CommandCodec)}.");
            }
        }

        public static PlanningCommand Read(PacketReader reader)
        {
            byte kind = reader.ReadByte();
            switch (kind)
            {
                case SelectWeapon:
                    return new SelectWeaponCommand(BoardCodec.ReadWeapon(reader));
                case SetBodyMove:
                    return new SetBodyMoveCommand(reader.ReadEnum<BodyMove>("a body move"));
                case SetPath:
                    return new SetPathCommand(new WeaponPath(ReadStroke(reader)));
                case ClearPath:
                    return new ClearPathCommand();
                case SetReady:
                    return new SetReadyCommand(reader.ReadBool("Ready"));
                case UseSignature:
                    return new UseSignatureCommand(BoardCodec.ReadSignature(reader));
                default:
                    throw PacketReader.Malformed($"A planning command of an unknown kind ({kind}).");
            }
        }

        public static void WriteStroke(PacketWriter writer, IReadOnlyList<Vec2> points)
        {
            writer.WriteCount(points.Count, ProtocolLimits.MaxStrokePoints, "stroke points");
            foreach (Vec2 point in points)
            {
                BoardCodec.WriteVec2(writer, point);
            }
        }

        public static Vec2[] ReadStroke(PacketReader reader)
        {
            var points = new Vec2[reader.ReadCount(ProtocolLimits.MaxStrokePoints, "stroke points", BytesPerStrokePoint)];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = BoardCodec.ReadVec2(reader);
            }

            return points;
        }
    }
}
