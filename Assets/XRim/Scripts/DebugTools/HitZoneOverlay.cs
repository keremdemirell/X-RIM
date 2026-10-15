using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.DebugTools
{
    /// <summary>
    /// Debug drawing of the hit zones (Session 13): every body of both dummies outlined where the pose on screen has it, from the layout the
    /// ragdoll is built from (<see cref="DummyLayout"/>, with the current tuning), and each zone labelled with its damage multiplier (GDD
    /// §11) and, for an arm or leg, the damage it has taken against its durability (§12). The damage is the board being planned, or the end
    /// of the turn being shown. Toggle: Debug → Playback.
    /// </summary>
    internal sealed class HitZoneOverlay
    {
        private const int MaxOutlines = 24;
        private const int CirclePoints = 24;
        private const float LineWidthWorld = 0.008f;
        private const int SortingOrder = 105;

        private static readonly Color OutlineColor = new Color(0.35f, 1f, 0.5f, 0.85f);
        private static readonly Color HeadColor = new Color(1f, 0.55f, 0.3f, 0.95f);
        private static readonly Color LimbAtRiskColor = new Color(1f, 0.3f, 0.3f, 0.95f);

        /// <summary>A limb that has taken at least this share of its durability is labelled in red: one more hit may take it off.</summary>
        private const float AtRiskShare = 0.5f;

        private static readonly BodyPart[] Zones =
            { BodyPart.Head, BodyPart.Torso, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };

        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<GizmoLabel> _labels = new List<GizmoLabel>();
        private int _used;

        public IReadOnlyList<GizmoLabel> Labels => _labels;

        /// <summary>Redraws for the pose on screen now; a null bootstrap hides everything.</summary>
        public void Update(MatchBootstrap bootstrap, Material material)
        {
            _labels.Clear();
            _used = 0;
            PoseSnapshot pose = bootstrap != null && bootstrap.IsComposed ? bootstrap.Playback.ShownPose : null;
            if (pose != null && bootstrap.ArenaRoot != null)
            {
                DummyLayout layout = DummyLayout.Of(bootstrap.SimulationSettings.Ragdoll, bootstrap.RulesSettings.Paths,
                    bootstrap.SimulationSettings.Segmentation);
                MatchState state = ShownState(bootstrap);
                foreach (Side side in new[] { Side.Left, Side.Right })
                {
                    Draw(bootstrap, material, layout, pose.Get(side), side, state);
                }
            }

            for (int i = _used; i < _lines.Count; i++)
            {
                if (_lines[i] != null) _lines[i].positionCount = 0;
            }
        }

        public void Destroy()
        {
            foreach (LineRenderer line in _lines)
            {
                if (line != null) Object.Destroy(line.gameObject);
            }

            _lines.Clear();
        }

        /// <summary>The board being planned, or the end of the turn shown (its limb damage).</summary>
        private static MatchState ShownState(MatchBootstrap bootstrap)
        {
            ClientMatchFlow flow = bootstrap.Flow;
            if (flow != null && flow.Phase == ClientPhase.Planning && flow.CurrentWindow != null) return flow.CurrentWindow.Board.State;
            return bootstrap.Playback.Current?.FinalBoard.State;
        }

        private void Draw(MatchBootstrap bootstrap, Material material, DummyLayout layout, FighterPose pose, Side side, MatchState state)
        {
            RulesSettings rules = bootstrap.RulesSettings;
            foreach (BodyPart zone in Zones)
            {
                if (pose.IsSevered(zone)) continue;
                bool first = true;
                foreach (DummyBodyLayout body in layout.BodiesOf(zone))
                {
                    if (!TryPoseOf(body, pose, out BodyPose at)) continue;
                    Outline(bootstrap, material, body, at, zone == BodyPart.Head ? HeadColor : OutlineColor);
                    if (!first) continue;
                    first = false;
                    _labels.Add(Label(zone, side, body, at, rules, state));
                }
            }

            if (pose.IsTorsoSplit) Outline(bootstrap, material, layout.Find(DummyLayout.TorsoTopName), pose.TorsoTop, OutlineColor);
        }

        /// <summary>Where a body is in the pose: an arm's or leg's lower segment has its own entry; a split torso is drawn by its halves.</summary>
        private static bool TryPoseOf(DummyBodyLayout body, FighterPose pose, out BodyPose at)
        {
            bool lower = body.Name.EndsWith(DummyLayout.LowerSuffix);
            if (lower && !pose.HasLowerSegments)
            {
                at = default;
                return false;
            }

            at = lower ? pose.GetLower(body.Part) : pose.Get(body.Part);
            return true;
        }

        private static GizmoLabel Label(BodyPart zone, Side side, DummyBodyLayout body, BodyPose at, RulesSettings rules, MatchState state)
        {
            float multiplier = rules.HitZones.MultiplierFor(zone.ToHitZone());
            string text = string.Format(CultureInfo.InvariantCulture, "{0} ×{1:0.##}", zone, multiplier);
            Color color = zone == BodyPart.Head ? HeadColor : OutlineColor;
            bool limb = zone.IsArm() || zone.IsLeg();
            if (limb && state != null)
            {
                float durability = zone.IsArm() ? rules.Damage.ArmDurability : rules.Damage.LegDurability;
                float taken = state.Fighters[side].GetLimbDamage(zone);
                text += string.Format(CultureInfo.InvariantCulture, "  {0:0}/{1:0}", taken, durability);
                if (durability > 0f && taken >= durability * AtRiskShare) color = LimbAtRiskColor;
            }

            return new GizmoLabel(HeldItemShape.ToArena(body.CentreLocalUnits, at), text, color);
        }

        private void Outline(MatchBootstrap bootstrap, Material material, DummyBodyLayout body, BodyPose at, Color color)
        {
            LineRenderer line = Next(bootstrap, material);
            if (line == null) return;
            ArenaSpace space = bootstrap.Space;
            line.startColor = color;
            line.endColor = color;
            if (body.IsRound)
            {
                float radius = body.SizeUnits.X * 0.5f;
                Vec2 centre = HeldItemShape.ToArena(body.CentreLocalUnits, at);
                line.positionCount = CirclePoints + 1;
                for (int i = 0; i <= CirclePoints; i++)
                {
                    Vec2 rim = centre + new Vec2(radius, 0f).Rotated(360f * i / CirclePoints);
                    line.SetPosition(i, space.ToWorld(rim));
                }

                return;
            }

            float halfWidth = body.SizeUnits.X * 0.5f;
            float bottom = body.Rises ? 0f : -body.SizeUnits.Y;
            float top = body.Rises ? body.SizeUnits.Y : 0f;
            Vec2[] corners = { new Vec2(-halfWidth, bottom), new Vec2(halfWidth, bottom), new Vec2(halfWidth, top), new Vec2(-halfWidth, top) };
            line.positionCount = corners.Length + 1;
            for (int i = 0; i <= corners.Length; i++)
            {
                line.SetPosition(i, space.ToWorld(HeldItemShape.ToArena(corners[i % corners.Length], at)));
            }
        }

        private LineRenderer Next(MatchBootstrap bootstrap, Material material)
        {
            if (_used >= MaxOutlines) return null;
            if (_used == _lines.Count)
                _lines.Add(DebugLines.Create(bootstrap.ArenaRoot, "GizmoHitZone" + _used, material, Color.white, LineWidthWorld, SortingOrder));
            return _lines[_used++];
        }
    }
}
