using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Input;
using XRim.Networking;

namespace XRim.App
{
    /// <summary>
    /// Offline debug mode: one person enters both players' inputs on one device. The left side plans, a cover screen
    /// hides it, then the right side plans, so the prototype needs neither networking nor a bot.
    /// </summary>
    public sealed class HotSeatCoordinator
    {
        private IInputScheme Scheme { get; }
        private ScreenLayout Layout { get; }

        public HotSeatCoordinator(IInputScheme scheme, ScreenLayout layout)
        {
            Scheme = Guard.NotNull(scheme, nameof(scheme));
            Layout = Guard.NotNull(layout, nameof(layout));
        }

        public IReadOnlyList<IPlanSource> CreateSources()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("HotSeatCoordinator.CreateSources is not implemented yet.");
        }
    }
}
