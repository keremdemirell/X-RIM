using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Rules.SuddenDeath;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The authoritative match flow (see <see cref="MatchPhase"/>). Pure C#, driven by <see cref="Tick"/> with an
    /// injected <see cref="IClock"/>, so tests control time and a server can host it unchanged.
    /// Whoever owns this object is the authority; clients only mirror its events.
    /// <code>
    /// MatchSetup ──Start──→ TurnStart ──BeginPlanning──→ Planning ──(both Ready or deadlines)──→ Locked
    ///   Locked ──BeginExecution──→ Executing ──CompleteExecution──→ Resolving ──→ TurnStart
    ///                                                                          ├→ SuddenDeathSetup
    ///                                                                          └→ MatchOver
    /// </code>
    /// <list type="bullet">
    /// <item><b>TurnStart and SuddenDeathSetup</b> are where the authority waits between turns, for example for the
    /// client's playback to finish, then calls <see cref="BeginPlanning"/>. The planning timer starts at that call,
    /// not when the turn resolved, so playback never eats planning time (ARCHITECTURE §5). SuddenDeathSetup is a
    /// TurnStart whose setup (<see cref="SuddenDeathSetup"/>) was already applied; Session 12 adds the board reset here.</item>
    /// <item><b>Planning</b> ends as soon as both players are Ready or the timer ends, whichever comes first
    /// (§3, Decided). Each side's timer is its own constraints' planning duration, normally the same 12 s.</item>
    /// <item><b>Locked:</b> every plan is validated again by <see cref="PlanValidator"/>. A plan that fails runs as an
    /// idle plan with the same weapon (not in the GDD; it cannot happen with plans built by the sessions).</item>
    /// <item><b>Resolving</b> happens inside <see cref="CompleteExecution"/>: the resolved state is adopted, the idle
    /// counters are updated through <see cref="IIdleTurnPolicy"/>, and <see cref="EndConditionEvaluator"/> decides.</item>
    /// </list>
    /// </summary>
    public sealed class MatchStateMachine
    {
        private readonly EndConditionEvaluator _evaluator = new EndConditionEvaluator();
        private readonly SuddenDeathSetup _suddenDeathSetup = new SuddenDeathSetup();
        private readonly PlanValidator _validator;

        public MatchPhase Phase { get; private set; } = MatchPhase.MatchSetup;
        public MatchSetup Setup { get; }

        /// <summary>The rules state between turns. Replaced by the resolved state when a turn completes.</summary>
        public MatchState State { get; private set; }

        public RulesSettings Settings { get; }

        /// <summary>This turn's planning sessions, one per side. Null before the first <see cref="BeginPlanning"/>.</summary>
        public PerSide<PlanningSession> Sessions { get; private set; }

        /// <summary>
        /// This turn's limits per side, built when the turn starts from the default plus the fighter's status effects
        /// (stun and stagger, Session 06). The authority may adjust them before <see cref="BeginPlanning"/>.
        /// </summary>
        public PerSide<PlanningConstraints> Constraints { get; private set; }

        /// <summary>When planning is over for both sides at the latest, on the clock's scale. Valid from <see cref="BeginPlanning"/>.</summary>
        public double PlanningDeadlineSeconds { get; private set; }

        /// <summary>What each side executes. Valid from Locked until the next <see cref="BeginPlanning"/>.</summary>
        public PerSide<TurnPlan> LockedPlans { get; private set; }

        /// <summary>The validator's verdict on each side's plan at lock time, for diagnostics. Rejected plans run as idle plans.</summary>
        public PerSide<CommandResult> LockValidation { get; private set; }

        /// <summary>Set once the phase is <see cref="MatchPhase.MatchOver"/>.</summary>
        public MatchOutcome Outcome { get; private set; }

        private RulePolicies Policies { get; }
        private IClock Clock { get; }

        public MatchStateMachine(MatchSetup setup, RulesSettings settings, RulePolicies policies, IClock clock)
        {
            Setup = Guard.NotNull(setup, nameof(setup));
            Settings = Guard.NotNull(settings, nameof(settings));
            Policies = Guard.NotNull(policies, nameof(policies));
            Clock = Guard.NotNull(clock, nameof(clock));
            State = MatchState.CreateInitial(setup, settings);
            _validator = new PlanValidator(settings, policies);
        }

        /// <summary>
        /// Leaves MatchSetup and starts turn 1 (turn index 0). Both loadouts are revealed to both players here
        /// (§6); they are public in <see cref="Setup"/>. A loadout that breaks the §6 rules stops the match.
        /// </summary>
        public void Start()
        {
            RequirePhase(nameof(Start), MatchPhase.MatchSetup);

            var issues = new List<string>();
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                var sideIssues = new List<string>();
                LoadoutValidator.Validate(Setup.Fighters[side].Loadout, Settings, sideIssues);
                foreach (string issue in sideIssues) issues.Add($"{side}: {issue}");
            }

            if (issues.Count > 0) throw new InvalidOperationException("Invalid loadout. " + string.Join(" ", issues));

            PrepareTurn();
            Phase = MatchPhase.TurnStart;
        }

        /// <summary>
        /// TurnStart or SuddenDeathSetup → Planning: opens one <see cref="PlanningSession"/> per side and starts
        /// the planning timer. The authority supplies where each weapon tip rests, read from the frozen board.
        /// </summary>
        public void BeginPlanning(PerSide<IWeaponTipSource> weaponTips)
        {
            Guard.NotNull(weaponTips, nameof(weaponTips));
            RequirePhase(nameof(BeginPlanning), MatchPhase.TurnStart, MatchPhase.SuddenDeathSetup);

            double now = Clock.NowSeconds;
            Sessions = PerSide<PlanningSession>.Create(side =>
            {
                PlanningConstraints constraints = Constraints[side];
                return new PlanningSession(side, State.Fighters[side].CurrentWeapon, Setup.Fighters[side].Loadout,
                    weaponTips[side], constraints, now + constraints.PlanningDurationSeconds, Settings, Policies);
            });
            PlanningDeadlineSeconds = Math.Max(Sessions.Left.DeadlineSeconds, Sessions.Right.DeadlineSeconds);
            LockedPlans = null;
            LockValidation = null;
            Phase = MatchPhase.Planning;
        }

        /// <summary>Planning → Locked as soon as both players are Ready or their timers have ended.</summary>
        public void Tick()
        {
            LockIfPlanningIsOver();
        }

        /// <summary>
        /// Routes one planning command to its side's session, timed by this machine's clock so clients cannot fake
        /// timing (§18). The second Ready locks the plans at once.
        /// </summary>
        public CommandResult Apply(Side side, PlanningCommand command)
        {
            Guard.NotNull(command, nameof(command));
            if (Phase != MatchPhase.Planning) return CommandResult.Reject(CommandRejection.NotInPlanningPhase);

            CommandResult result = Sessions[side].Apply(command, Clock.NowSeconds);
            if (result.Accepted) LockIfPlanningIsOver();
            return result;
        }

        /// <summary>The opponent's view of one side during planning: weapon and Ready (§3, §6).</summary>
        public PublicPlanningState PublicStateOf(Side side) => Sessions[side].PublicState;

        /// <summary>Locked → Executing. Returns the plans to simulate.</summary>
        public PerSide<TurnPlan> BeginExecution()
        {
            RequirePhase(nameof(BeginExecution), MatchPhase.Locked);
            Phase = MatchPhase.Executing;
            return LockedPlans;
        }

        /// <summary>
        /// Executing → Resolving → the next phase. Adopts the simulated state, counts idle turns, runs the end
        /// conditions and moves on: TurnStart (the next turn, or a repeated sudden-death turn), SuddenDeathSetup
        /// (setup applied, the turn counter advanced) or MatchOver. The returned result says which.
        /// </summary>
        public EndCheckResult CompleteExecution(ExecutionReport report)
        {
            Guard.NotNull(report, nameof(report));
            RequirePhase(nameof(CompleteExecution), MatchPhase.Executing);
            if (report.ResolvedState.TurnIndex != State.TurnIndex)
            {
                throw new ArgumentException(
                    $"The report is for turn {report.ResolvedState.TurnIndex}, but turn {State.TurnIndex} is executing.", nameof(report));
            }

            Phase = MatchPhase.Resolving;
            State = report.ResolvedState.Clone();
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterState fighter = State.Fighters[side];
                fighter.ConsecutiveIdleTurns = Policies.IdleTurn.IsIdle(LockedPlans[side]) ? fighter.ConsecutiveIdleTurns + 1 : 0;
            }

            EndCheckResult result = _evaluator.Evaluate(
                new ExecutionReport(State, report.FirstValidHitTime, report.FirstHitsOnSameStep), Settings);

            switch (result.Resolution)
            {
                case TurnResolution.MatchOver:
                    Outcome = result.Outcome;
                    Phase = MatchPhase.MatchOver;
                    break;
                case TurnResolution.EnterSuddenDeath:
                    _suddenDeathSetup.Apply(State);
                    AdvanceTurn();
                    Phase = MatchPhase.SuddenDeathSetup;
                    break;
                default: // NextTurn, RepeatSuddenDeathTurn
                    AdvanceTurn();
                    Phase = MatchPhase.TurnStart;
                    break;
            }

            return result;
        }

        private void AdvanceTurn()
        {
            State.TurnIndex++;
            PrepareTurn();
        }

        /// <summary>The default limits, then every status effect's edit (stun and stagger arrive in Session 06).</summary>
        private void PrepareTurn()
        {
            Constraints = PerSide<PlanningConstraints>.Create(side =>
            {
                PlanningConstraints constraints = PlanningConstraints.CreateDefault(Settings.Match);
                foreach (IStatusEffect status in State.Fighters[side].Statuses)
                {
                    status.ApplyToNextTurn(constraints);
                }

                return constraints;
            });
        }

        private void LockIfPlanningIsOver()
        {
            if (Phase != MatchPhase.Planning) return;

            double now = Clock.NowSeconds;
            if (!IsDone(Sessions.Left, now) || !IsDone(Sessions.Right, now)) return;

            LockValidation = PerSide<CommandResult>.Create(side => _validator.Validate(Sessions[side]));
            LockedPlans = PerSide<TurnPlan>.Create(side =>
                LockValidation[side].Accepted ? Sessions[side].CurrentPlan : IdlePlanFor(side));

            // A weapon switch was public the moment it happened, so it stays even when the rest of the plan is dropped.
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                State.Fighters[side].CurrentWeapon = LockedPlans[side].Weapon;
            }

            // SESSION 12 HOOK (§13): the electric wall reacts to the locked body moves here (ElectricWallRules.OnPlansLocked).
            Phase = MatchPhase.Locked;
        }

        /// <summary>A side is done when it pressed Ready or its own timer ended.</summary>
        private static bool IsDone(PlanningSession session, double nowSeconds) =>
            session.IsReady || !session.IsOpen(nowSeconds);

        private TurnPlan IdlePlanFor(Side side)
        {
            PlanningSession session = Sessions[side];
            foreach (WeaponId weapon in session.Loadout)
            {
                if (weapon == session.Weapon) return TurnPlan.Empty(session.Weapon);
            }

            return TurnPlan.Empty(State.Fighters[side].CurrentWeapon);
        }

        private void RequirePhase(string operation, params MatchPhase[] allowed)
        {
            foreach (MatchPhase phase in allowed)
            {
                if (Phase == phase) return;
            }

            throw new InvalidOperationException(
                $"{operation} needs the match in {string.Join(" or ", allowed)}, but it is in {Phase}.");
        }
    }
}
