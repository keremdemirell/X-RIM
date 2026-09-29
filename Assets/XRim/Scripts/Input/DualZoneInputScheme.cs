using System;
using XRim.Config;
using XRim.Core;
using XRim.Networking;

namespace XRim.Input
{
    /// <summary>
    /// GDD §4: a small body zone over the player's own dummy (swipes → body move) and a large weapon zone (drawing →
    /// weapon path). No virtual D-pad or direction buttons (Rejected). Only emits commands; the rules decide.
    /// </summary>
    public sealed class DualZoneInputScheme : IInputScheme
    {
        private InputConfig Config { get; }
        private ISwipeClassifier SwipeClassifier { get; }
        private ScreenToArenaMapper Mapper { get; }

        public DualZoneInputScheme(InputConfig config, ISwipeClassifier swipeClassifier, ScreenToArenaMapper mapper)
        {
            Config = Guard.NotNull(config, nameof(config));
            SwipeClassifier = Guard.NotNull(swipeClassifier, nameof(swipeClassifier));
            Mapper = Guard.NotNull(mapper, nameof(mapper));
        }

        public void Begin(ScreenLayout layout, IPlanningCommandSink sink)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("DualZoneInputScheme.Begin is not implemented yet.");
        }

        public void Tick()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("DualZoneInputScheme.Tick is not implemented yet.");
        }

        public void End()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("DualZoneInputScheme.End is not implemented yet.");
        }
    }
}
