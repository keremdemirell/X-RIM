using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Planning;

namespace XRim.Networking.Protocol
{
    /// <summary>What follows the header of each message kind, written and read field by field in the same order.</summary>
    internal static class MessageBodies
    {
        public static void Write(PacketWriter writer, NetMessage message)
        {
            switch (message)
            {
                case ClientHelloMessage hello:
                    writer.WriteInt((int)hello.Handedness);
                    BoardCodec.WriteSignatureIds(writer, hello.Signatures, "signature moves brought");
                    writer.WriteText(hello.ReconnectToken, ProtocolLimits.MaxTextBytes, "the reconnect token");
                    break;
                case LoadoutPickMessage pick:
                    BoardCodec.WriteWeapons(writer, pick.Weapons, "the loadout pick");
                    break;
                case PlanningCommandMessage command:
                    writer.WriteInt(command.Sequence);
                    writer.WriteInt(command.TurnIndex);
                    CommandCodec.Write(writer, command.Command);
                    break;
                case PlaybackFinishedMessage finished:
                    writer.WriteInt(finished.TurnIndex);
                    break;
                case PingMessage ping:
                    writer.WriteInt(ping.PingId);
                    writer.WriteDouble(ping.ClientSentSeconds);
                    break;
                case MatchFoundMessage found:
                    writer.WriteInt((int)found.YourSide);
                    writer.WriteText(found.ReconnectToken, ProtocolLimits.MaxTextBytes, "the reconnect token");
                    writer.WriteText(found.SettingsText, ProtocolLimits.MaxSettingsTextBytes, "the settings snapshot");
                    break;
                case MatchStartedMessage started:
                    BoardCodec.WriteSetup(writer, started.Setup);
                    BoardCodec.WriteBoard(writer, started.InitialBoard);
                    break;
                case PlanningStartedMessage planning:
                    WritePlanningStarted(writer, planning);
                    break;
                case PublicStateMessage state:
                    writer.WriteInt((int)state.Side);
                    BoardCodec.WritePublicState(writer, state.State);
                    break;
                case CommandAnswerMessage answer:
                    writer.WriteInt(answer.Sequence);
                    writer.WriteInt((int)answer.Rejection);
                    break;
                case PlanStateMessage plan:
                    writer.WriteInt(plan.TurnIndex);
                    BoardCodec.WriteWeapon(writer, plan.Weapon);
                    writer.WriteInt((int)plan.BodyMove);
                    CommandCodec.WriteStroke(writer, plan.Stroke);
                    BoardCodec.WriteSignature(writer, plan.Signature);
                    writer.WriteBool(plan.IsReady);
                    break;
                case PlanningLockedMessage locked:
                    writer.WriteInt(locked.TurnIndex);
                    break;
                case TurnResolvedMessage resolved:
                    TurnResultCodec.Write(writer, resolved.Result);
                    break;
                case MatchEndedMessage ended:
                    BoardCodec.WriteOutcome(writer, ended.Outcome);
                    break;
                case ConnectionNoticeMessage notice:
                    writer.WriteInt((int)notice.Side);
                    writer.WriteInt((int)notice.Status);
                    writer.WriteFloat(notice.WaitSecondsLeft);
                    break;
                case PongMessage pong:
                    writer.WriteInt(pong.PingId);
                    writer.WriteDouble(pong.ClientSentSeconds);
                    writer.WriteDouble(pong.ServerNowSeconds);
                    break;
                case GoodbyeMessage goodbye:
                    writer.WriteInt((int)goodbye.Reason);
                    writer.WriteText(goodbye.Detail, ProtocolLimits.MaxTextBytes, "the goodbye detail");
                    break;
                default:
                    throw new NotSupportedException($"The message {message.GetType().Name} has no protocol form: add it to {nameof(MessageBodies)}.");
            }
        }

        public static NetMessage Read(MessageKind kind, PacketReader reader)
        {
            switch (kind)
            {
                case MessageKind.ClientHello:
                    return new ClientHelloMessage(reader.ReadEnum<Handedness>("a handedness"),
                        BoardCodec.ReadSignatureIds(reader, "signature moves brought"), reader.ReadText(ProtocolLimits.MaxTextBytes, "the reconnect token"));
                case MessageKind.LoadoutPick:
                    return new LoadoutPickMessage(BoardCodec.ReadWeapons(reader, "the loadout pick"));
                case MessageKind.PlanningCommand:
                    return new PlanningCommandMessage(reader.ReadInt("the command's number"), reader.ReadInt("the turn index"), CommandCodec.Read(reader));
                case MessageKind.PlaybackFinished:
                    return new PlaybackFinishedMessage(reader.ReadInt("the turn index"));
                case MessageKind.Ping:
                    return new PingMessage(reader.ReadInt("the ping's number"), reader.ReadDouble());
                case MessageKind.MatchFound:
                    return new MatchFoundMessage(reader.ReadEnum<Side>("your side"), reader.ReadText(ProtocolLimits.MaxTextBytes, "the reconnect token"),
                        reader.ReadText(ProtocolLimits.MaxSettingsTextBytes, "the settings snapshot"));
                case MessageKind.MatchStarted:
                    return new MatchStartedMessage(BoardCodec.ReadSetup(reader), BoardCodec.ReadBoard(reader));
                case MessageKind.PlanningStarted:
                    return ReadPlanningStarted(reader);
                case MessageKind.PublicState:
                    return new PublicStateMessage(reader.ReadEnum<Side>("a side"), BoardCodec.ReadPublicState(reader));
                case MessageKind.CommandAnswer:
                    return new CommandAnswerMessage(reader.ReadInt("the command's number"), reader.ReadEnum<CommandRejection>("a refusal"));
                case MessageKind.PlanState:
                    return new PlanStateMessage(reader.ReadInt("the turn index"), BoardCodec.ReadWeapon(reader), reader.ReadEnum<BodyMove>("a body move"),
                        CommandCodec.ReadStroke(reader), BoardCodec.ReadSignature(reader), reader.ReadBool("Ready"));
                case MessageKind.PlanningLocked:
                    return new PlanningLockedMessage(reader.ReadInt("the turn index"));
                case MessageKind.TurnResolved:
                    return new TurnResolvedMessage(TurnResultCodec.Read(reader));
                case MessageKind.MatchEnded:
                    return new MatchEndedMessage(BoardCodec.ReadOutcome(reader));
                case MessageKind.ConnectionNotice:
                    return new ConnectionNoticeMessage(reader.ReadEnum<Side>("a side"), reader.ReadEnum<ConnectionStatus>("a connection status"),
                        reader.ReadFloat());
                case MessageKind.Pong:
                    return new PongMessage(reader.ReadInt("the ping's number"), reader.ReadDouble(), reader.ReadDouble());
                case MessageKind.Goodbye:
                    return new GoodbyeMessage(reader.ReadEnum<GoodbyeReason>("a goodbye reason"), reader.ReadText(ProtocolLimits.MaxTextBytes, "the goodbye detail"));
                default:
                    throw new ProtocolException(ProtocolError.UnknownKind, $"A message of an unknown kind ({(byte)kind}).");
            }
        }

        private static void WritePlanningStarted(PacketWriter writer, PlanningStartedMessage planning)
        {
            writer.WriteInt(planning.TurnIndex);
            writer.WriteDouble(planning.RemainingSeconds);
            writer.WriteDouble(planning.OwnRemainingSeconds);
            writer.WriteBool(planning.IsSuddenDeath);
            BoardCodec.WriteConstraints(writer, planning.Constraints.Left);
            BoardCodec.WriteConstraints(writer, planning.Constraints.Right);
            BoardCodec.WriteBoard(writer, planning.Board);
            BoardCodec.WriteSignatureIds(writer, planning.OwnSignatures.Moves, "signature moves offered");
            BoardCodec.WriteCharge(writer, planning.OwnSignatures.Charge);
            writer.WriteBool(planning.OwnSignatures.CanUse);
            BoardCodec.WritePublicState(writer, planning.PublicStates.Left);
            BoardCodec.WritePublicState(writer, planning.PublicStates.Right);
        }

        private static PlanningStartedMessage ReadPlanningStarted(PacketReader reader)
        {
            int turnIndex = reader.ReadInt("the turn index");
            double remaining = reader.ReadDouble();
            double ownRemaining = reader.ReadDouble();
            bool suddenDeath = reader.ReadBool("sudden death");
            var constraints = new PerSide<PlanningConstraints>(BoardCodec.ReadConstraints(reader), BoardCodec.ReadConstraints(reader));
            var board = BoardCodec.ReadBoard(reader);
            var signatures = new SignatureOffer(BoardCodec.ReadSignatureIds(reader, "signature moves offered"), BoardCodec.ReadCharge(reader),
                reader.ReadBool("a signature move usable"));
            var publicStates = new PerSide<PublicPlanningState>(BoardCodec.ReadPublicState(reader), BoardCodec.ReadPublicState(reader));
            return new PlanningStartedMessage(turnIndex, remaining, ownRemaining, suddenDeath, constraints, board, signatures, publicStates);
        }
    }
}
