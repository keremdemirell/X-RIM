using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Input;
using XRim.Presentation.Arena;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.App
{
    /// <summary>
    /// One human's live path preview (GDD §6): builds the path the rules will execute from the stroke being drawn (or the last one
    /// set), with the weapon tip and ink from the planning window, and draws it in the arena: clipped at reach, cut where the ink
    /// runs out, red after a too-sharp turn, and a faint copy where the body move will carry it. Only the player planning sees theirs.
    /// </summary>
    public sealed class PlanPreviewPresenter : IDisposable
    {
        /// <summary>A hop's path frame is shown at the top of the hop, halfway through the move.</summary>
        private const double HopTopShareOfDuration = 0.5;

        private readonly PathPreviewView _view;
        private readonly PathPreviewBuilder _builder;
        private readonly RulesSettings _rules;
        private readonly SimulationSettings _simulation;
        private readonly ArenaSpace _space;
        private readonly Func<BodyMove, BodyMoveStats> _bodyMoves;

        /// <param name="bodyMoves">A body move's data (the sandbox passes the live tuning; a match its snapshot).</param>
        public PlanPreviewPresenter(Transform arenaRoot, Material material, string name, RulePolicies policies, RulesSettings rules,
            SimulationSettings simulation, ArenaSpace space, Func<BodyMove, BodyMoveStats> bodyMoves)
        {
            _view = new PathPreviewView(arenaRoot, material, name);
            _builder = new PathPreviewBuilder(Guard.NotNull(policies, nameof(policies)));
            _rules = Guard.NotNull(rules, nameof(rules));
            _simulation = Guard.NotNull(simulation, nameof(simulation));
            _space = space;
            _bodyMoves = Guard.NotNull(bodyMoves, nameof(bodyMoves));
        }

        /// <summary>The path last built; null when nothing is shown (the HUD then counts all the ink as left).</summary>
        public PathPreview Preview { get; private set; }

        /// <param name="visible">False hides it (another player's turn, or not planning).</param>
        public void Update(TouchPlanSource source, bool visible)
        {
            Guard.NotNull(source, nameof(source));
            WeaponStats weapon = visible && source.IsPlanning ? Weapon(source) : null;
            if (weapon == null)
            {
                Clear();
                return;
            }

            bool drawing = source.ActiveStroke.Count > 0;
            IReadOnlyList<Vec2> stroke = drawing ? source.ActiveStroke : source.Draft.Stroke.Points;
            Vec2 tip = source.Window.WeaponTips[source.Side].TipLocal(source.Draft.Weapon);
            Preview = _builder.Build(stroke, tip, weapon, _rules.Paths);
            BoardSnapshot board = source.Window.Board;
            _view.Show(Preview, TurnStartRoot.Of(board.Pose.Get(source.Side)), source.Side, _space, MoveFrame(source), drawing);
        }

        public void Clear()
        {
            Preview = null;
            _view.Clear();
        }

        public void Dispose() => _view.Destroy();

        /// <summary>The weapon set so far, with this turn's ink (a status effect may cut it).</summary>
        private WeaponStats Weapon(TouchPlanSource source)
        {
            WeaponStats stats = _rules.FindWeapon(source.Draft.Weapon);
            return stats?.WithInkLengthMultiplier(source.Window.Constraints[source.Side].InkLengthMultiplier);
        }

        /// <summary>
        /// Where the path frame will be once the body move is at full extent (a hop: at its top), as the simulation plays it; null
        /// without a body move.
        /// </summary>
        private BodyPose? MoveFrame(TouchPlanSource source)
        {
            BodyMove move = source.Draft.BodyMove;
            BodyMoveStats stats = move != BodyMove.None ? _bodyMoves(move) : null;
            if (stats == null) return null;

            Side side = source.Side;
            BoardSnapshot board = source.Window.Board;
            var start = BodyMoveStart.Of(side, board.Pose.Get(side), board.State.Fighters[side].Handedness, _simulation.Ragdoll);
            var driver = new StanceBodyMoveDriver();
            driver.Begin(stats, start);
            bool isHop = stats.DisplacementUnits.Y > 0f && !stats.IsLeanInPlace;
            BodyPose root = driver.Evaluate(SimTime.FromSeconds(isHop ? stats.DurationSeconds * HopTopShareOfDuration : stats.DurationSeconds)).Root;
            return _rules.Paths.PathTiltsWithTorsoLean ? root : new BodyPose(root.PositionUnits, start.Root.RotationDegrees);
        }
    }
}
