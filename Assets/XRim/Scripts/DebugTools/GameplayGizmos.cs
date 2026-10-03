using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.DebugTools
{
    /// <summary>
    /// In-game debug drawing of the turn being shown (Session 07; ARCHITECTURE §10): every contact a held item made, once
    /// playback has reached it (all of them between turns). At each contact point it draws the contact normal (bright) and the
    /// relative motion (dim), and labels it with what the rules made of it: a hit's zone and damage, a clash's angle (§10 stage
    /// 1), both powers and outcome, a block's angle against the shield face and its kind; every label carries the refined
    /// time-to-impact (§9) and the weapon's distance along its path. Toggle: Debug → Playback. Hit zones and the path's ink
    /// are drawn elsewhere (the sandbox draws paths) or later.
    /// </summary>
    public sealed class GameplayGizmos : MonoBehaviour
    {
        private const int MaxMarks = 24;
        private const float NormalLengthUnits = 60f;
        private const float MotionLengthUnits = 45f;
        private const float LineWidthWorld = 0.012f;
        private const int SortingOrder = 110;
        private const float LabelWidth = 220f;
        private const float LabelHeight = 54f;
        private const float LabelOffsetPixels = 8f;
        private const float MinMotionUnitsPerSecond = 1e-3f;

        private static readonly Color HitColor = new Color(1f, 0.35f, 0.25f, 0.95f);
        private static readonly Color ClashColor = new Color(1f, 0.7f, 0.15f, 0.95f);
        private static readonly Color BlockColor = new Color(0.35f, 0.85f, 1f, 0.95f);
        private static readonly Color ContactColor = new Color(0.85f, 0.85f, 0.85f, 0.8f);
        private static readonly Color MotionTint = new Color(1f, 1f, 1f, 0.4f);

        [SerializeField] private bool _showContacts = true;

        private readonly List<Mark> _marks = new List<Mark>();
        private readonly List<LineRenderer> _normals = new List<LineRenderer>();
        private readonly List<LineRenderer> _motions = new List<LineRenderer>();
        private MatchBootstrap _bootstrap;
        private Material _material;
        private GUIStyle _labelStyle;
        private TurnResult _shownTurn;
        private long _shownMicroseconds = -1;

        public bool ShowContacts
        {
            get => _showContacts;
            set => _showContacts = value;
        }

        public void Init(MatchBootstrap bootstrap) => _bootstrap = bootstrap;

        private void LateUpdate()
        {
            if (!TryGetTurn(out TurnResult turn, out SimTime until) || !_showContacts)
            {
                _marks.Clear();
                _shownTurn = null;
                HideLines(0);
                return;
            }

            if (turn != _shownTurn || until.Microseconds != _shownMicroseconds)
            {
                _shownTurn = turn;
                _shownMicroseconds = until.Microseconds;
                CollectMarks(turn, until);
            }

            DrawLines();
        }

        private void OnGUI()
        {
            if (!_showContacts || _marks.Count == 0 || _bootstrap == null) return;
            Camera view = Camera.main;
            if (view == null) return;

            if (_labelStyle == null) _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = false };
            foreach (Mark mark in _marks)
            {
                Vector3 screen = view.WorldToScreenPoint(ToWorld(mark.Point));
                if (screen.z < 0f) continue;
                var rect = new Rect(screen.x + LabelOffsetPixels, Screen.height - screen.y - LabelHeight - LabelOffsetPixels, LabelWidth, LabelHeight);
                _labelStyle.normal.textColor = mark.Color;
                GUI.Label(rect, mark.Text, _labelStyle);
            }
        }

        private void OnDestroy()
        {
            // The lines live under the scene's arena root, which leaving Play mode may already have destroyed.
            foreach (LineRenderer line in _normals)
            {
                if (line != null) Destroy(line.gameObject);
            }

            foreach (LineRenderer line in _motions)
            {
                if (line != null) Destroy(line.gameObject);
            }
        }

        /// <summary>The turn being shown and how far into it: playback's time while it plays, the whole turn between turns.</summary>
        private bool TryGetTurn(out TurnResult turn, out SimTime until)
        {
            turn = null;
            until = default;
            if (_bootstrap == null || !_bootstrap.IsComposed) return false;
            TurnPlayback playback = _bootstrap.Playback;
            turn = playback != null ? playback.Current : null;
            if (turn == null) return false;

            bool inProgress = playback.IsPlaying || playback.Player.CurrentTime > SimTime.Zero;
            until = inProgress ? playback.Player.CurrentTime : turn.Timeline.Duration;
            return true;
        }

        private void CollectMarks(TurnResult turn, SimTime until)
        {
            _marks.Clear();
            IReadOnlyList<MatchEvent> events = turn.Timeline.Events;
            foreach (MatchEvent matchEvent in events)
            {
                if (_marks.Count >= MaxMarks || matchEvent.Time > until) break;
                if (!(matchEvent is ContactEvent contactEvent)) continue;
                TurnContact contact = contactEvent.Contact;
                if (!contact.WeaponSide.HasValue) continue;
                _marks.Add(Describe(contact, events));
            }
        }

        /// <summary>What the rules made of a held item's contact, found among the turn's events at the same instant.</summary>
        private static Mark Describe(TurnContact contact, IReadOnlyList<MatchEvent> events)
        {
            ContactFacts facts = contact.Facts;
            string timing = string.Format(CultureInfo.InvariantCulture, "t {0:0.0} ms   d {1:0}", contact.Time.Milliseconds, contact.PathDistanceUnits);
            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent.Time != contact.Time) continue;
                switch (matchEvent)
                {
                    case WeaponClashEvent clash when clash.ContactPointUnits == facts.PointUnits:
                        return new Mark(contact, ClashColor, ClashLine(clash.Result) + "\n" + timing);
                    case ShieldBlockEvent block when block.ContactPointUnits == facts.PointUnits:
                        return new Mark(contact, BlockColor, BlockLine(block) + "\n" + timing);
                    case HitLandedEvent hit when IsHitOf(hit, facts):
                        return new Mark(contact, HitColor, string.Format(CultureInfo.InvariantCulture, "HIT {0}: {1:0.#} HP\n{2}",
                            hit.Hit.Part, hit.Damage.HpDamage, timing));
                }
            }

            return new Mark(contact, ContactColor, string.Format(CultureInfo.InvariantCulture, "contact {0:0}° (no rule)\n{1}",
                contact.ContactAngleDegrees, timing));
        }

        private static bool IsHitOf(HitLandedEvent hit, ContactFacts facts)
        {
            BodyTag body = facts.A.Role == BodyRole.BodyPart ? facts.A : facts.B;
            BodyTag item = facts.A.Role == BodyRole.HeldItem ? facts.A : facts.B;
            return body.Role == BodyRole.BodyPart && item.Owner == hit.Hit.Attacker && body.Owner == hit.Hit.Victim && body.Part == hit.Hit.Part;
        }

        private static string ClashLine(ClashResult result)
        {
            string outcome;
            switch (result.Kind)
            {
                case ClashKind.CrushThrough:
                    outcome = $"{result.Winner} CRUSHES";
                    break;
                case ClashKind.BothRebound:
                    outcome = "REBOUND";
                    break;
                case ClashKind.LighterDeflectsHeavier:
                    outcome = $"{result.Winner} DEFLECTS";
                    break;
                default:
                    outcome = "SLIDE";
                    break;
            }

            return string.Format(CultureInfo.InvariantCulture, "CLASH {0:0}° {1}: {2}\nP {3:0.##} vs {4:0.##}", result.ContactAngleDegrees,
                result.IsHardClash ? "hard" : "glancing", outcome, result.LeftPower, result.RightPower);
        }

        private static string BlockLine(ShieldBlockEvent block) => string.Format(CultureInfo.InvariantCulture, "BLOCK {0} {1:0}°, face {2:0.00}",
            block.Result.AttackStopped ? "FULL" : "partial ×" + block.Result.DamageMultiplier.ToString("0.##", CultureInfo.InvariantCulture),
            block.ContactAngleDegrees, block.FacePositionFraction);

        private void DrawLines()
        {
            EnsureLines();
            ArenaSpace space = _bootstrap.Space;
            int drawn = Mathf.Min(_marks.Count, _normals.Count);
            for (int i = 0; i < drawn; i++)
            {
                Mark mark = _marks[i];
                ShowSegment(_normals[i], mark.Point, mark.Point + mark.Normal * NormalLengthUnits, mark.Color, space);
                Vec2 motion = mark.Motion.Length >= MinMotionUnitsPerSecond ? mark.Motion.Normalized * MotionLengthUnits : Vec2.Zero;
                ShowSegment(_motions[i], mark.Point, mark.Point + motion, mark.Color * MotionTint, space);
            }

            HideLines(drawn);
        }

        private void EnsureLines()
        {
            if (_normals.Count >= MaxMarks || _bootstrap.ArenaRoot == null) return;
            if (_material == null) _material = DebugLines.CreateMaterial();
            for (int i = _normals.Count; i < MaxMarks; i++)
            {
                _normals.Add(DebugLines.Create(_bootstrap.ArenaRoot, "GizmoContactNormal" + i, _material, Color.white, LineWidthWorld, SortingOrder));
                _motions.Add(DebugLines.Create(_bootstrap.ArenaRoot, "GizmoContactMotion" + i, _material, Color.white, LineWidthWorld, SortingOrder));
            }
        }

        private static void ShowSegment(LineRenderer line, Vec2 from, Vec2 to, Color color, ArenaSpace space)
        {
            if (line == null) return;
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 2;
            line.SetPosition(0, space.ToWorld(from));
            line.SetPosition(1, space.ToWorld(to));
        }

        private void HideLines(int fromIndex)
        {
            for (int i = fromIndex; i < _normals.Count; i++)
            {
                if (_normals[i] != null) _normals[i].positionCount = 0;
                if (_motions[i] != null) _motions[i].positionCount = 0;
            }
        }

        private Vector3 ToWorld(Vec2 arenaPoint)
        {
            Vector2 local = _bootstrap.Space.ToWorld(arenaPoint);
            Transform root = _bootstrap.ArenaRoot;
            return root != null ? root.TransformPoint(local) : (Vector3)local;
        }

        /// <summary>One contact to draw: where, its normal and relative motion, its colour and label.</summary>
        private readonly struct Mark
        {
            public Vec2 Point { get; }
            public Vec2 Normal { get; }
            public Vec2 Motion { get; }
            public Color Color { get; }
            public string Text { get; }

            public Mark(TurnContact contact, Color color, string text)
            {
                Point = contact.Facts.PointUnits;
                Normal = contact.Facts.Normal;
                Motion = contact.Facts.RelativeVelocityUnitsPerSecond;
                Color = color;
                Text = text;
            }
        }
    }
}
