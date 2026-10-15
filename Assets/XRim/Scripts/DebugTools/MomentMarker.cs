using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Moments;
using XRim.Presentation.Playback;
using XRim.Simulation.Recording;

namespace XRim.DebugTools
{
    /// <summary>
    /// Debug drawing of the turn's legendary moment (Session 13): a ring where the detector found it and a label with its kind, time and
    /// reason, once playback has reached it (at once between turns), so a moment that was or was not replayed can be checked against what
    /// happened. While a replay plays, it marks the replayed moment. Toggle: Debug → Playback.
    /// </summary>
    internal sealed class MomentMarker
    {
        private const int RingPoints = 32;
        private const float RingRadiusUnits = 45f;
        private const float LineWidthWorld = 0.014f;
        private const int SortingOrder = 112;

        private static readonly Color MarkerColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        private readonly List<GizmoLabel> _labels = new List<GizmoLabel>();
        private LineRenderer _ring;
        private TurnResult _detectedTurn;
        private bool _hasMoment;
        private LegendaryMoment _moment;

        public IReadOnlyList<GizmoLabel> Labels => _labels;

        /// <summary>Redraws for the turn on screen now; a null bootstrap hides the marker.</summary>
        public void Update(MatchBootstrap bootstrap, Material material)
        {
            _labels.Clear();
            if (!TryGetShownMoment(bootstrap, out LegendaryMoment moment))
            {
                if (_ring != null) _ring.positionCount = 0;
                return;
            }

            if (_ring == null)
                _ring = DebugLines.Create(bootstrap.ArenaRoot, "GizmoLegendaryMoment", material, MarkerColor, LineWidthWorld, SortingOrder);
            ArenaSpace space = bootstrap.Space;
            _ring.positionCount = RingPoints + 1;
            for (int i = 0; i <= RingPoints; i++)
            {
                _ring.SetPosition(i, space.ToWorld(moment.FocusUnits + new Vec2(RingRadiusUnits, 0f).Rotated(360f * i / RingPoints)));
            }

            _labels.Add(new GizmoLabel(moment.FocusUnits + new Vec2(RingRadiusUnits, RingRadiusUnits), string.Format(CultureInfo.InvariantCulture,
                "★ {0} at {1:0.000} s\n{2}", moment.Kind, moment.Time.Seconds, moment.Reason), MarkerColor));
        }

        public void Destroy()
        {
            if (_ring != null) Object.Destroy(_ring.gameObject);
            _ring = null;
        }

        private bool TryGetShownMoment(MatchBootstrap bootstrap, out LegendaryMoment moment)
        {
            moment = default;
            if (bootstrap == null || !bootstrap.IsComposed || bootstrap.ArenaRoot == null) return false;
            if (bootstrap.Replay != null && bootstrap.Replay.IsPlaying)
            {
                moment = bootstrap.Replay.Moment;
                return true;
            }

            TurnPlayback playback = bootstrap.Playback;
            TurnResult turn = playback.Current;
            if (turn == null) return false;
            if (turn != _detectedTurn)
            {
                _detectedTurn = turn;
                FeelConfig feel = bootstrap.Tuning != null ? bootstrap.Tuning.Feel : null;
                var settings = feel != null ? MomentSettings.From(feel) : new MomentSettings();
                _hasMoment = new HeuristicMomentDetector(settings).TryDetect(turn, out _moment);
            }

            if (!_hasMoment) return false;
            bool midTurn = playback.IsPlaying || (playback.Player.CurrentTime > SimTime.Zero && playback.Player.CurrentTime < turn.Timeline.Duration);
            if (midTurn && playback.Player.CurrentTime < _moment.Time) return false;
            moment = _moment;
            return true;
        }
    }
}
