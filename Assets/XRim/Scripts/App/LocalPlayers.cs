using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Input;
using XRim.Rules.Paths;

namespace XRim.App
{
    /// <summary>
    /// The humans planning on this device for one match (one against the bot, two in hot-seat): each one's touch plan source and live
    /// path preview. Every source reads touches only while it is active; only the player the screen serves sees their preview.
    /// </summary>
    public sealed class LocalPlayers : IDisposable
    {
        private readonly Func<Side, TouchPlanSource> _createSource;
        private readonly Func<Side, PlanPreviewPresenter> _createPreview;
        private readonly List<TouchPlanSource> _sources = new List<TouchPlanSource>();
        private readonly List<PlanPreviewPresenter> _previews = new List<PlanPreviewPresenter>();

        public LocalPlayers(Func<Side, TouchPlanSource> createSource, Func<Side, PlanPreviewPresenter> createPreview)
        {
            _createSource = Guard.NotNull(createSource, nameof(createSource));
            _createPreview = Guard.NotNull(createPreview, nameof(createPreview));
        }

        public IReadOnlyList<TouchPlanSource> Sources => _sources;

        /// <summary>A human for this side (the plan-source factory's <c>createHuman</c>).</summary>
        public TouchPlanSource Add(Side side)
        {
            if (Find(side) != null) throw new InvalidOperationException($"A human already plays {side}.");
            TouchPlanSource source = _createSource(side);
            _sources.Add(source);
            _previews.Add(_createPreview(side));
            return source;
        }

        /// <summary>The human playing this side; null if a bot (or nobody) plays it.</summary>
        public TouchPlanSource Find(Side side)
        {
            foreach (TouchPlanSource source in _sources)
            {
                if (source.Side == side) return source;
            }

            return null;
        }

        /// <summary>Every active human reads this frame's touches and sends what they did as planning commands.</summary>
        public void TickInput()
        {
            foreach (TouchPlanSource source in _sources)
            {
                source.Tick();
            }
        }

        /// <summary>Shows the preview of the player the screen serves (null for none) and hides everyone else's.</summary>
        public void UpdatePreviews(TouchPlanSource shown)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                _previews[i].Update(_sources[i], _sources[i] == shown);
            }
        }

        /// <summary>The path the player has drawn so far, as the rules will run it; null when nothing is shown.</summary>
        public PathPreview PreviewOf(TouchPlanSource source)
        {
            int index = _sources.IndexOf(source);
            return index >= 0 ? _previews[index].Preview : null;
        }

        public void Dispose()
        {
            foreach (TouchPlanSource source in _sources)
            {
                source.IsActive = false;
            }

            foreach (PlanPreviewPresenter preview in _previews)
            {
                preview.Dispose();
            }

            _sources.Clear();
            _previews.Clear();
        }
    }
}
