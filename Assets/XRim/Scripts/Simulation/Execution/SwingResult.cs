using System.Collections.Generic;
using XRim.Core;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Execution
{
    /// <summary>A simulated swing, ready to play back: the recorded poses, the measured contacts and how it ended.</summary>
    public sealed class SwingResult
    {
        public TurnTimeline Timeline { get; }

        /// <summary>In the order they happened: by reporting step, then by refined time, then by the engine's stable order.</summary>
        public IReadOnlyList<SwingContact> Contacts { get; }

        public int StepsSimulated { get; }
        public SwingEndReason EndReason { get; }
        public PoseSnapshot FinalPose { get; }

        public SwingResult(TurnTimeline timeline, IReadOnlyList<SwingContact> contacts, int stepsSimulated, SwingEndReason endReason,
            PoseSnapshot finalPose)
        {
            Timeline = Guard.NotNull(timeline, nameof(timeline));
            Contacts = Guard.NotNull(contacts, nameof(contacts));
            StepsSimulated = stepsSimulated;
            EndReason = endReason;
            FinalPose = Guard.NotNull(finalPose, nameof(finalPose));
        }
    }
}
