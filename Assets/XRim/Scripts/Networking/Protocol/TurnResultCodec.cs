using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A whole <see cref="TurnResult"/>: its turn index and end reason, every frame (<see cref="TimelineCodec"/>, rounded), every event
    /// (<see cref="EventCodec"/>, exact), the exact final board and the execution report. The board and report carry what the server put in
    /// them; hiding the opponent's signature charge is the server's job before it sends a result.
    /// </summary>
    internal static class TurnResultCodec
    {
        public static void Write(PacketWriter writer, TurnResult result)
        {
            writer.WriteInt(result.TurnIndex);
            writer.WriteInt((int)result.EndReason);
            TimelineCodec.Write(writer, result.Timeline.Frames);
            EventCodec.Write(writer, result.Timeline.Events);
            BoardCodec.WriteBoard(writer, result.FinalBoard);
            BoardCodec.WriteReport(writer, result.Report);
        }

        public static TurnResult Read(PacketReader reader)
        {
            int turnIndex = reader.ReadInt("the turn index");
            var endReason = reader.ReadEnum<TurnEndReason>("the turn's end");
            TimelineFrame[] frames = TimelineCodec.Read(reader);
            var timeline = new TurnTimeline(frames, EventCodec.Read(reader));
            BoardSnapshot finalBoard = BoardCodec.ReadBoard(reader);
            ExecutionReport report = BoardCodec.ReadReport(reader);
            return new TurnResult(turnIndex, timeline, finalBoard, report, endReason);
        }
    }
}
