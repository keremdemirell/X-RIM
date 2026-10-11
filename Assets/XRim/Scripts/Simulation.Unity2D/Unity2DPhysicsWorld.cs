using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;
using Object = UnityEngine.Object;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// <see cref="IPhysicsWorld"/> on Unity 2D physics (Rigidbody2D ragdolls joined by HingeJoint2D, GDD §18).
    /// Runs in its own hidden scene with a local PhysicsScene2D that only moves when <see cref="Step"/> is called,
    /// so a whole turn can be simulated faster than real time and nothing in the visual scene is affected.
    /// Requires Play mode (SceneManager.CreateScene is a runtime API).
    /// <para>
    /// Every <see cref="Load"/> builds a fresh physics scene and creates the bodies in a fixed order, so the same board and
    /// plans always build the same physics world: a turn simulated twice repeats exactly (re-simulation, replays, tests).
    /// </para>
    /// <para>
    /// Standing: each torso is pulled toward an invisible kinematic root anchor by a strength-limited RelativeJoint2D
    /// (upright included), and every joint holds its target angle with a hinge-motor servo: the rest pose, or the bent legs
    /// a body move asks for (<see cref="SetLimbTarget"/>). Both are solved inside the physics solver, so standing is stable,
    /// yet a hard hit can still knock the dummy (pillar 4). A kinematic-torso fallback exists (<see cref="RootDriveMode.Kinematic"/>).
    /// </para>
    /// <para>
    /// The held weapon is not jointed to the body: its driver moves it, and the weapon arm follows it with a hand spring
    /// (<see cref="Ragdoll.UpdateGrip"/>), so the arm can never block or yank the weapon. Held items report contacts even
    /// when both are kinematic (D1), so two blades meeting is a contact for the clash rules.
    /// </para>
    /// <para>
    /// Arena: a floor at y = 0 and, when the edge policy says so (D22), a solid invisible wall at each edge. Each player's electric wall
    /// (GDD §13) is a kinematic barrier of the same size, built at every load and switched on, moved or off by
    /// <see cref="SetElectricWall"/>; unlike the edges, its contacts are reported, with its owner's body too.
    /// </para>
    /// <para>
    /// Contacts: after each step every body's contacts are polled; a pair of bodies from different owners that was not
    /// touching after the previous step is a new contact. Floor and edge contacts are not reported and a dummy never collides
    /// with itself. Relative velocity is each body's motion over the step before (Box2D reports a contact one step after
    /// the motion that made it), measured from positions so it is right for kinematic bodies too: the approach, not the bounce.
    /// Order: by load-time body index, which is stable (Left parts and torso halves, Left weapon, Right parts and torso halves,
    /// Right weapon, severed limbs, Left wall, Right wall).
    /// </para>
    /// <para>
    /// Lost limbs (GDD §11, §12): the weapon is held in the arm <c>FighterState.HeldItemArm</c> names (the other hand once the
    /// dominant arm is gone); a broken limb falls loose and is captured among the severed limbs from then on; a dropped item falls
    /// and is captured as the dropped item; an armless dummy's head is driven as its held item (the item convention turns it 90°,
    /// <see cref="HeldItemShape.HeadAsItem"/>); a dead dummy goes limp and its pre-split torso splits. Severed limbs on the floor are
    /// rebuilt loose, in two pieces when they kept their lower segment.
    /// </para>
    /// </summary>
    public sealed class Unity2DPhysicsWorld : IPhysicsWorld
    {
        private const string SceneNamePrefix = "XRim.SimulationPhysics.";
        private const string ContainerName = "Bodies";
        private const string FloorName = "Floor";
        private const string LeftEdgeName = "ArenaEdgeLeft";
        private const string RightEdgeName = "ArenaEdgeRight";
        private const string RootAnchorName = "RootAnchor";
        private const string ElectricWallName = "ElectricWall";
        private const int SideCount = 2;

        /// <summary>Thickness of the floor below y = 0 and of each edge wall: thick enough that nothing tunnels through.</summary>
        private const float BoundaryThicknessUnits = 100f;

        /// <summary>Height of each edge wall above the floor: far above any dummy, jump or flying limb.</summary>
        private const float EdgeWallHeightUnits = 4000f;

        /// <summary>Each collider keeps its own contact offset, so two bodies count as touching at twice that gap.</summary>
        private const int ContactOffsetsPerPair = 2;

        /// <summary>
        /// Unity reports a contact normal pointing from <see cref="ContactPoint2D.collider"/> toward
        /// <see cref="ContactPoint2D.otherCollider"/>. The PlayMode test ThrustIntoTorso_ReportsATimedContactFromWeaponToTorso checks it.
        /// </summary>
        private static readonly bool NormalPointsFromColliderToOtherCollider = true;

        private readonly RagdollPrefabSet _prefabs;
        private readonly List<ContactFacts> _pendingContacts = new List<ContactFacts>();
        private readonly Fighter[] _fighters = new Fighter[SideCount];
        private readonly List<SeveredLimb> _severedLimbs = new List<SeveredLimb>();
        private readonly Rigidbody2D[] _electricWalls = new Rigidbody2D[SideCount];
        private readonly List<TrackedBody> _tracked = new List<TrackedBody>();
        private readonly Dictionary<Collider2D, int> _indexByCollider = new Dictionary<Collider2D, int>();
        private readonly List<Collider2D> _colliderBuffer = new List<Collider2D>();
        private readonly List<ContactPoint2D> _contactBuffer = new List<ContactPoint2D>();
        private readonly List<NewPair> _newPairs = new List<NewPair>();
        private HashSet<long> _touching = new HashSet<long>();
        private HashSet<long> _touchingNow = new HashSet<long>();
        private SimulationSettings _simulation = new SimulationSettings();
        private PhysicsScene2D _physicsScene;
        private GameObject _container;

        /// <summary>The hidden scene the bodies live in. <see cref="Load"/> replaces it with a fresh one.</summary>
        public Scene Scene { get; private set; }

        public ArenaSpace Space { get; }

        /// <param name="space">Arena-to-world scale.</param>
        /// <param name="prefabs">The ragdoll prefabs <see cref="Load"/> spawns; null for a world that is only stepped (tests).</param>
        public Unity2DPhysicsWorld(ArenaSpace space, RagdollPrefabSet prefabs = null)
        {
            Space = space;
            _prefabs = prefabs;
            CreateScene();
        }

        public float TouchDistanceUnits => Space.ToArenaLength(ContactOffsetsPerPair * Physics2D.defaultContactOffset);

        /// <summary>A fighter's ragdoll in the hidden scene (debug tools and tests); null before <see cref="Load"/>.</summary>
        public Ragdoll GetRagdoll(Side side) => _fighters[(int)side]?.Ragdoll;

        public void Load(PoseSnapshot pose, MatchState state, RulesSettings rules, SimulationSettings simulation, ArenaEdges edges)
        {
            Guard.NotNull(pose, nameof(pose));
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(rules, nameof(rules));
            _simulation = Guard.NotNull(simulation, nameof(simulation));
            if (_prefabs == null) throw new InvalidOperationException("This physics world was created without ragdoll prefabs.");

            Clear();
            UnloadScene();
            CreateScene();
            _container = new GameObject(ContainerName);
            SceneManager.MoveGameObjectToScene(_container, Scene);

            Ragdoll template = _prefabs.For(simulation.Segmentation);
            float gravityScale = GravityScale(simulation);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                _fighters[(int)side] = LoadFighter(side, template, pose.Get(side), state.Fighters[side], rules, simulation, gravityScale);
            }

            foreach (SeveredLimbPose limb in pose.SeveredLimbs)
            {
                _severedLimbs.Add(LoadSeveredLimb(limb, rules.Paths, simulation.Ragdoll, gravityScale));
            }

            IgnoreFormerOwners();
            CreateArenaBounds(edges);
            CreateElectricWalls(state);
            TrackBodies();
        }

        public void SetElectricWall(Side owner, float? xUnits)
        {
            Rigidbody2D wall = _electricWalls[(int)owner];
            if (wall == null) return;
            if (!xUnits.HasValue)
            {
                wall.gameObject.SetActive(false);
                return;
            }

            Vector2 centre = ElectricWallCentre(owner, xUnits.Value);
            if (wall.gameObject.activeSelf)
            {
                wall.MovePosition(centre);
                return;
            }

            wall.transform.localPosition = centre;
            wall.gameObject.SetActive(true);
            wall.position = centre;
        }

        public void SetHeldItemTarget(Side side, BodyPose target)
        {
            Rigidbody2D item = HeldItem(side);
            if (item == null) return;
            if (item.bodyType != RigidbodyType2D.Kinematic) item.bodyType = RigidbodyType2D.Kinematic;
            if (IsStriking(side)) target = HeldItemShape.HeadFromItem(target);
            item.MovePosition(Space.ToWorld(target.PositionUnits));
            item.MoveRotation(target.RotationDegrees);
        }

        public void PushHeldItem(Side side, Vec2 accelerationUnitsPerSecondSquared, float angularAccelerationDegreesPerSecondSquared)
        {
            Fighter fighter = _fighters[(int)side];
            Rigidbody2D item = HeldItem(side);
            if (item == null) return;
            if (item.bodyType != RigidbodyType2D.Dynamic) item.bodyType = RigidbodyType2D.Dynamic;
            fighter.PushAcceleration = Space.ToWorld(accelerationUnitsPerSecondSquared);
            fighter.PushAngularAccelerationDegrees = angularAccelerationDegreesPerSecondSquared;
        }

        public void ReleaseHeldItem(Side side, Vec2 velocityUnitsPerSecond, float angularVelocityDegreesPerSecond)
        {
            Rigidbody2D item = HeldItem(side);
            if (item == null) return;
            if (item.bodyType != RigidbodyType2D.Dynamic) item.bodyType = RigidbodyType2D.Dynamic;
            item.linearVelocity = Space.ToWorld(velocityUnitsPerSecond);
            item.angularVelocity = angularVelocityDegreesPerSecond;
        }

        public BodyState GetHeldItemState(Side side)
        {
            Rigidbody2D item = HeldItem(side);
            if (item == null) return default;
            return new BodyState(HeldItemPose(side, item), Space.ToArena(item.linearVelocity), item.angularVelocity);
        }

        public void SetRootTarget(Side side, BodyPose target)
        {
            Fighter fighter = _fighters[(int)side];
            if (fighter == null) return;
            Rigidbody2D driven = fighter.KinematicRoot ? fighter.Torso : fighter.RootAnchor;
            driven.MovePosition(Space.ToWorld(target.PositionUnits));
            driven.MoveRotation(target.RotationDegrees);
        }

        public void SetLimbTarget(Side side, BodyPart limb, LimbAngles target) => _fighters[(int)side]?.Ragdoll.SetLimbTarget(limb, target);

        public void Step(float deltaSeconds)
        {
            RememberMotion(deltaSeconds);
            foreach (Fighter fighter in _fighters)
            {
                if (fighter == null) continue;
                fighter.Ragdoll.UpdateServos(_simulation.Ragdoll.JointServoGainPerSecond);
                fighter.Ragdoll.UpdateGrip();
                ApplyPush(fighter);
            }

            _physicsScene.Simulate(deltaSeconds);
            CollectNewContacts();
        }

        public void DrainContacts(List<ContactFacts> into)
        {
            into.AddRange(_pendingContacts);
            _pendingContacts.Clear();
        }

        /// <summary>
        /// Breaks the limb's joint by logic (GDD §18), then pops the limb off backward and up with a backward spin
        /// (<see cref="SimulationSettings.SeverReaction"/>), so it always falls over instead of standing on its end.
        /// </summary>
        public void BreakJoint(Side side, BodyPart part)
        {
            Ragdoll ragdoll = _fighters[(int)side]?.Ragdoll;
            if (ragdoll == null || !ragdoll.BreakJoint(part)) return;

            SeverReactionSettings pop = _simulation.SeverReaction;
            float facing = side.FacingSign();
            Vector2 kick = Space.ToWorld(new Vec2(-facing * pop.PopBackSpeedUnitsPerSecond, pop.PopUpSpeedUnitsPerSecond));
            foreach (Rigidbody2D segment in new[] { ragdoll.GetBody(part), ragdoll.GetLowerBody(part) })
            {
                if (segment == null) continue;
                segment.linearVelocity += kick;
                segment.angularVelocity += facing * pop.PopSpinDegreesPerSecond;
            }
        }

        public void DropHeldItem(Side side) => _fighters[(int)side]?.Ragdoll.DropHeldItem();

        public void SplitTorso(Side side)
        {
            Ragdoll ragdoll = _fighters[(int)side]?.Ragdoll;
            if (ragdoll == null || ragdoll.SplitTorso()) return;
            if (!ragdoll.HasPreSplitTorso)
                Debug.LogWarning("[XRim] The ragdoll prefab has no torso halves, so the dead dummy only goes limp. Run XRim/Setup/Build Placeholder Dummies.");
        }

        public void StrikeWithBodyPart(Side side, BodyPart part)
        {
            Ragdoll ragdoll = _fighters[(int)side]?.Ragdoll;
            if (ragdoll == null) return;
            if (part != BodyPart.Head) throw new NotSupportedException($"Only the head can strike (§12 default), not the {part}.");
            ragdoll.StrikeWith(part);
        }

        public void ApplyImpulse(Side side, BodyPart part, Vec2 impulse)
        {
            Rigidbody2D body = _fighters[(int)side]?.Ragdoll.GetBody(part);
            if (body != null) body.AddForce(Space.ToWorld(impulse), ForceMode2D.Impulse);
        }

        public BodyPose GetPose(Side side, BodyPart part)
        {
            Rigidbody2D body = _fighters[(int)side]?.Ragdoll.GetBody(part);
            return body != null ? PoseOf(body) : default;
        }

        public PoseSnapshot CapturePose()
        {
            var snapshot = new PoseSnapshot();
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Fighter fighter = _fighters[(int)side];
                if (fighter != null) Capture(fighter, snapshot.Get(side));
            }

            foreach (SeveredLimb limb in _severedLimbs)
            {
                snapshot.SeveredLimbs.Add(limb.Lower != null
                    ? new SeveredLimbPose(limb.Owner, limb.Part, PoseOf(limb.Body), PoseOf(limb.Lower))
                    : new SeveredLimbPose(limb.Owner, limb.Part, PoseOf(limb.Body)));
            }

            // Limbs severed during this turn still hang off their ragdoll, loose: they lie in the arena from the moment of the cut.
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Ragdoll ragdoll = _fighters[(int)side]?.Ragdoll;
                if (ragdoll == null) continue;
                foreach (BodyPart part in ragdoll.SeveredSinceLoad)
                {
                    Rigidbody2D lower = ragdoll.GetLowerBody(part);
                    BodyPose upper = PoseOf(ragdoll.GetBody(part));
                    snapshot.SeveredLimbs.Add(lower != null
                        ? new SeveredLimbPose(side, part, upper, PoseOf(lower))
                        : new SeveredLimbPose(side, part, upper));
                }
            }

            return snapshot;
        }

        public bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond)
        {
            float maxSpeedWorld = Space.ToWorldLength(linearSpeedUnitsPerSecond);
            foreach (TrackedBody tracked in _tracked)
            {
                Rigidbody2D body = tracked.Body;
                if (tracked.IsArena || body == null || !body.gameObject.activeInHierarchy) continue;
                if (body.linearVelocity.magnitude >= maxSpeedWorld) return false;
                if (Mathf.Abs(body.angularVelocity) >= angularSpeedDegreesPerSecond) return false;
            }

            return true;
        }

        public void Dispose()
        {
            Clear();
            UnloadScene();
        }

        private void CreateScene()
        {
            Scene = SceneManager.CreateScene(SceneNamePrefix + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physicsScene = Scene.GetPhysicsScene2D();
        }

        private void UnloadScene()
        {
            if (Scene.IsValid() && Scene.isLoaded) SceneManager.UnloadSceneAsync(Scene);
        }

        private Fighter LoadFighter(Side side, Ragdoll template, FighterPose pose, FighterState state, RulesSettings rules,
            SimulationSettings simulation, float gravityScale)
        {
            Ragdoll ragdoll = Object.Instantiate(template, _container.transform);
            ragdoll.name = side + "Dummy";
            if (side == Side.Right) ragdoll.MirrorForRightSide();
            if (!ragdoll.gameObject.activeSelf) ragdoll.gameObject.SetActive(true);
            if (!ragdoll.MatchesReach(rules.Paths, Space))
                Debug.LogWarning("[XRim] The ragdoll prefab was built with another shoulder, arm length or scale than the " +
                                 "current settings. Run XRim/Setup/Build Placeholder Dummies.");
            ragdoll.AssignOwner(side);

            // GDD §12: the dominant hand while that arm is intact, then the other; a body blow holds nothing (the head strikes).
            WeaponStats weapon = rules.FindWeapon(state.CurrentWeapon);
            BodyPart? holdingArm = state.HeldItemArm;
            BodyPart arm = holdingArm ?? BodyParts.DominantArm(state.Handedness);
            Rigidbody2D item = null;
            if (pose.HasHeldItem && weapon != null && weapon.Kind != WeaponKind.BodyBlow && holdingArm.HasValue)
            {
                item = ragdoll.Hold(weapon.Id, arm);
                if (item == null)
                {
                    Debug.LogWarning($"[XRim] The ragdoll prefab has no body for '{weapon.Id}'. Run XRim/Setup/Build Placeholder Dummies.");
                }
                else
                {
                    PlaceholderRagdollBuilder.FitHeldItem(item, weapon, Space);
                    item.bodyType = simulation.WeaponDriver == WeaponDriverKind.Kinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                    item.useFullKinematicContacts = true;
                }
            }

            ragdoll.ApplyTuning(simulation.Ragdoll, gravityScale, item != null ? arm : (BodyPart?)null);
            ragdoll.IgnoreSelfCollisions();

            var fighter = new Fighter(ragdoll, ragdoll.GetBody(BodyPart.Torso), CreateRootAnchor(side, pose.Get(BodyPart.Torso)), pose.Clone());
            if (simulation.RootDrive.Mode == RootDriveMode.Kinematic)
            {
                fighter.Torso.bodyType = RigidbodyType2D.Kinematic;
                fighter.KinematicRoot = true;
            }
            else
            {
                AttachToRoot(fighter, simulation.RootDrive);
            }

            ragdoll.ApplyPose(pose, Space);
            foreach (BodyPart part in BodyParts.All)
            {
                if (part != BodyPart.Torso && state.IsSevered(part)) ragdoll.LeaveOff(part);
            }

            return fighter;
        }

        /// <summary>
        /// A severed limb lying in the arena (GDD §12): a loose body of the limb's shape, or two hinged ones when it kept its lower
        /// segment (ten bodies, S1), at rest where it lay.
        /// </summary>
        private SeveredLimb LoadSeveredLimb(SeveredLimbPose limb, PathSettings paths, RagdollSettings body, float gravityScale)
        {
            if (!limb.HasLower || !limb.Part.IsSeverable())
            {
                Rigidbody2D whole = PlaceholderRagdollBuilder.BuildSeveredLimb(limb.Owner, limb.Part, body, paths, Space);
                PlaceLoose(whole, limb.Pose, gravityScale);
                return new SeveredLimb(whole, null, limb.Owner, limb.Part);
            }

            Rigidbody2D[] pieces = PlaceholderRagdollBuilder.BuildSeveredLimbSegments(limb.Owner, limb.Part, body, paths, Space);
            PlaceLoose(pieces[0], limb.Pose, gravityScale);
            PlaceLoose(pieces[1], limb.Lower, gravityScale);
            return new SeveredLimb(pieces[0], pieces[1], limb.Owner, limb.Part);
        }

        private void PlaceLoose(Rigidbody2D rigidbody, BodyPose pose, float gravityScale)
        {
            rigidbody.transform.SetParent(_container.transform, false);
            rigidbody.gravityScale = gravityScale;
            Vector2 position = Space.ToWorld(pose.PositionUnits);
            rigidbody.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, pose.RotationDegrees));
            rigidbody.position = position;
            rigidbody.rotation = pose.RotationDegrees;
            rigidbody.linearVelocity = Vector2.zero;
            rigidbody.angularVelocity = 0f;
        }

        /// <summary>
        /// A severed limb on the floor never collides with the dummy it came from, as it did not while it fell: it lies through that
        /// dummy's feet instead of being shoved out of the legs it landed in. The opponent and every weapon still meet it.
        /// </summary>
        private void IgnoreFormerOwners()
        {
            var ownerColliders = new List<Collider2D>();
            var limbColliders = new List<Collider2D>();
            var bodies = new List<Rigidbody2D>();
            foreach (SeveredLimb limb in _severedLimbs)
            {
                Ragdoll owner = _fighters[(int)limb.Owner]?.Ragdoll;
                if (owner == null) continue;
                bodies.Clear();
                owner.GetPartBodies(bodies);
                owner.GetTorsoHalves(bodies);
                if (owner.ActiveHeldItem != null) bodies.Add(owner.ActiveHeldItem);
                ownerColliders.Clear();
                foreach (Rigidbody2D body in bodies)
                {
                    _colliderBuffer.Clear();
                    body.GetAttachedColliders(_colliderBuffer);
                    ownerColliders.AddRange(_colliderBuffer);
                }

                limbColliders.Clear();
                limb.Body.GetAttachedColliders(limbColliders);
                if (limb.Lower != null)
                {
                    _colliderBuffer.Clear();
                    limb.Lower.GetAttachedColliders(_colliderBuffer);
                    limbColliders.AddRange(_colliderBuffer);
                }

                foreach (Collider2D mine in limbColliders)
                {
                    foreach (Collider2D theirs in ownerColliders)
                    {
                        Physics2D.IgnoreCollision(mine, theirs, true);
                    }
                }
            }
        }

        private Rigidbody2D CreateRootAnchor(Side side, BodyPose torsoPose)
        {
            var anchor = new GameObject(side + RootAnchorName);
            anchor.transform.SetParent(_container.transform, false);
            Vector2 position = Space.ToWorld(torsoPose.PositionUnits);
            anchor.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, torsoPose.RotationDegrees));
            var body = anchor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.position = position;
            body.rotation = torsoPose.RotationDegrees;
            return body;
        }

        private void AttachToRoot(Fighter fighter, RootDriveSettings drive)
        {
            var joint = fighter.Torso.gameObject.AddComponent<RelativeJoint2D>();
            joint.connectedBody = fighter.RootAnchor;
            joint.autoConfigureOffset = false;
            joint.linearOffset = Vector2.zero;
            joint.angularOffset = 0f;
            joint.enableCollision = false;
            joint.maxForce = fighter.Torso.mass * Space.ToWorldLength(drive.MaxAccelerationUnitsPerSecondSquared);
            joint.maxTorque = fighter.Torso.inertia * drive.MaxAngularAccelerationDegreesPerSecondSquared * Mathf.Deg2Rad;
            joint.correctionScale = drive.CorrectionFraction;
        }

        /// <summary>The floor (its top at y = 0) and, for solid edges, a wall standing on it at each edge.</summary>
        private void CreateArenaBounds(ArenaEdges edges)
        {
            float wall = edges.IsSolid ? BoundaryThicknessUnits : 0f;
            CreateStaticBox(FloorName, BodyRole.Floor, new Vec2(edges.CentreXUnits, LegGeometry.FloorYUnits - BoundaryThicknessUnits * 0.5f),
                edges.WidthUnits + wall * 2f, BoundaryThicknessUnits);
            if (!edges.IsSolid) return;

            float wallCentreY = (EdgeWallHeightUnits - BoundaryThicknessUnits) * 0.5f;
            float wallHeight = EdgeWallHeightUnits + BoundaryThicknessUnits;
            CreateStaticBox(LeftEdgeName, BodyRole.ArenaEdge, new Vec2(edges.LeftXUnits - wall * 0.5f, wallCentreY), wall, wallHeight);
            CreateStaticBox(RightEdgeName, BodyRole.ArenaEdge, new Vec2(edges.RightXUnits + wall * 0.5f, wallCentreY), wall, wallHeight);
        }

        /// <summary>
        /// Each player's electric wall (GDD §13): a kinematic barrier as big as an edge wall, tagged as its owner's, standing where the
        /// state has it up and switched off otherwise. Both always exist, so the tracked bodies never change during a turn.
        /// </summary>
        private void CreateElectricWalls(MatchState state)
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                var gameObject = new GameObject(side + ElectricWallName);
                gameObject.transform.SetParent(_container.transform, false);
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(Space.ToWorldLength(BoundaryThicknessUnits), Space.ToWorldLength(EdgeWallHeightUnits + BoundaryThicknessUnits));
                gameObject.AddComponent<PhysicsBodyTag>().Configure(side, BodyRole.ElectricWall, BodyPart.Torso);
                _electricWalls[(int)side] = body;

                ElectricWallState wall = state.Walls[side];
                if (wall.IsUp)
                {
                    Vector2 centre = ElectricWallCentre(side, wall.PositionUnits);
                    gameObject.transform.localPosition = centre;
                    body.position = centre;
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>The wall's face stands at <paramref name="faceXUnits"/>; its body lies behind it, away from its owner's opponent.</summary>
        private Vector2 ElectricWallCentre(Side owner, float faceXUnits) =>
            Space.ToWorld(new Vec2(faceXUnits - owner.FacingSign() * BoundaryThicknessUnits * 0.5f,
                (EdgeWallHeightUnits - BoundaryThicknessUnits) * 0.5f));

        private void CreateStaticBox(string name, BodyRole role, Vec2 centreUnits, float widthUnits, float heightUnits)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(_container.transform, false);
            gameObject.transform.localPosition = Space.ToWorld(centreUnits);
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(Space.ToWorldLength(widthUnits), Space.ToWorldLength(heightUnits));
            gameObject.AddComponent<PhysicsBodyTag>().Configure(null, role, BodyPart.Torso);
        }

        /// <summary>
        /// Assigns every body a stable index: Left parts, Left weapon, Right parts, Right weapon, severed limbs, the electric walls (off or
        /// on), then the arena (floor and edges).
        /// </summary>
        private void TrackBodies()
        {
            _tracked.Clear();
            _indexByCollider.Clear();
            _touching.Clear();
            _pendingContacts.Clear();
            var bodies = new List<Rigidbody2D>();
            foreach (Fighter fighter in _fighters)
            {
                if (fighter == null) continue;
                fighter.Ragdoll.GetPartBodies(bodies);
                fighter.Ragdoll.GetTorsoHalves(bodies);
                if (fighter.Ragdoll.ActiveHeldItem != null) bodies.Add(fighter.Ragdoll.ActiveHeldItem);
            }

            foreach (SeveredLimb limb in _severedLimbs)
            {
                bodies.Add(limb.Body);
                if (limb.Lower != null) bodies.Add(limb.Lower);
            }

            foreach (Rigidbody2D body in bodies)
            {
                Track(body, false);
            }

            // A wall that is off has no attached colliders yet, so its own collider is mapped.
            foreach (Rigidbody2D wall in _electricWalls)
            {
                if (wall == null) continue;
                _indexByCollider[wall.GetComponent<Collider2D>()] = _tracked.Count;
                _tracked.Add(new TrackedBody(wall, wall.GetComponent<PhysicsBodyTag>().Tag, false));
            }

            foreach (string arenaName in new[] { FloorName, LeftEdgeName, RightEdgeName })
            {
                Transform arenaBody = _container.transform.Find(arenaName);
                if (arenaBody != null) Track(arenaBody.GetComponent<Rigidbody2D>(), true);
            }
        }

        private void Track(Rigidbody2D body, bool isArena)
        {
            int index = _tracked.Count;
            BodyTag tag = body.TryGetComponent(out PhysicsBodyTag bodyTag) ? bodyTag.Tag : default;
            _tracked.Add(new TrackedBody(body, tag, isArena));
            _colliderBuffer.Clear();
            body.GetAttachedColliders(_colliderBuffer);
            foreach (Collider2D collider in _colliderBuffer)
            {
                _indexByCollider[collider] = index;
            }
        }

        private void RememberMotion(float deltaSeconds)
        {
            foreach (TrackedBody tracked in _tracked)
            {
                tracked.RememberMotion(deltaSeconds);
            }
        }

        private void ApplyPush(Fighter fighter)
        {
            Rigidbody2D item = fighter.Ragdoll.ActiveHeldItem;
            if (item != null && item.bodyType == RigidbodyType2D.Dynamic)
            {
                item.AddForce(fighter.PushAcceleration * item.mass);
                item.AddTorque(fighter.PushAngularAccelerationDegrees * Mathf.Deg2Rad * item.inertia);
            }

            fighter.PushAcceleration = Vector2.zero;
            fighter.PushAngularAccelerationDegrees = 0f;
        }

        private void CollectNewContacts()
        {
            _touchingNow.Clear();
            _newPairs.Clear();
            for (int self = 0; self < _tracked.Count; self++)
            {
                TrackedBody tracked = _tracked[self];
                if (tracked.IsArena || !tracked.Body.gameObject.activeInHierarchy) continue;
                tracked.Body.GetContacts(_contactBuffer);
                foreach (ContactPoint2D contact in _contactBuffer)
                {
                    if (!_indexByCollider.TryGetValue(contact.collider, out int colliderIndex) ||
                        !_indexByCollider.TryGetValue(contact.otherCollider, out int otherColliderIndex))
                    {
                        continue;
                    }

                    bool selfIsCollider = colliderIndex == self;
                    int other = selfIsCollider ? otherColliderIndex : colliderIndex;
                    if (other <= self || _tracked[other].IsArena) continue;

                    long key = (long)self * _tracked.Count + other;
                    _touchingNow.Add(key);
                    if (_touching.Contains(key)) continue;

                    bool normalPointsAwayFromSelf = selfIsCollider == NormalPointsFromColliderToOtherCollider;
                    AddToNewPair(key, self, other, contact.point, normalPointsAwayFromSelf ? contact.normal : -contact.normal);
                }
            }

            _newPairs.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (NewPair pair in _newPairs)
            {
                _pendingContacts.Add(ToFacts(pair));
            }

            HashSet<long> previous = _touching;
            _touching = _touchingNow;
            _touchingNow = previous;
        }

        private void AddToNewPair(long key, int a, int b, Vector2 point, Vector2 normalFromAToB)
        {
            for (int i = 0; i < _newPairs.Count; i++)
            {
                if (_newPairs[i].Key != key) continue;
                _newPairs[i] = _newPairs[i].With(point, normalFromAToB);
                return;
            }

            _newPairs.Add(new NewPair(key, a, b, point, normalFromAToB));
        }

        private ContactFacts ToFacts(NewPair pair)
        {
            TrackedBody a = _tracked[pair.A];
            TrackedBody b = _tracked[pair.B];
            Vector2 point = pair.PointSum / pair.Count;
            Vector2 relative = b.VelocityAt(point) - a.VelocityAt(point);
            Vector2 normal = pair.NormalSum.normalized;
            return new ContactFacts(a.Tag, b.Tag, Space.ToArena(point), new Vec2(normal.x, normal.y), Space.ToArena(relative));
        }

        /// <summary>
        /// Every body's pose; a part left off (severed) keeps the pose it was loaded with. The held item (or striking head, in the
        /// item convention), a dropped item, and a split torso (its bottom half as the torso, its top half beside it).
        /// </summary>
        private void Capture(Fighter fighter, FighterPose pose)
        {
            Ragdoll ragdoll = fighter.Ragdoll;
            pose.HasLowerSegments = ragdoll.HasLowerSegments;
            foreach (BodyPart part in BodyParts.All)
            {
                Rigidbody2D upper = ragdoll.GetBody(part);
                if (upper != null) pose.Set(part, upper.gameObject.activeSelf ? PoseOf(upper) : fighter.LoadedPose.Get(part));
                Rigidbody2D lower = ragdoll.GetLowerBody(part);
                if (lower != null) pose.SetLower(part, lower.gameObject.activeSelf ? PoseOf(lower) : fighter.LoadedPose.GetLower(part));
            }

            Rigidbody2D item = ragdoll.ActiveHeldItem;
            pose.HasHeldItem = item != null;
            if (item != null) pose.HeldItem = ragdoll.StrikingPart.HasValue ? HeldItemShape.HeadAsItem(PoseOf(item)) : PoseOf(item);

            Rigidbody2D dropped = ragdoll.DroppedItem;
            pose.HasDroppedItem = dropped != null;
            if (dropped != null) pose.DroppedItem = PoseOf(dropped);

            pose.IsTorsoSplit = ragdoll.IsTorsoSplit;
            if (!ragdoll.IsTorsoSplit) return;
            pose.Set(BodyPart.Torso, PoseOf(ragdoll.TorsoBottom));
            pose.TorsoTop = PoseOf(ragdoll.TorsoTop);
        }

        private bool IsStriking(Side side) => _fighters[(int)side]?.Ragdoll.StrikingPart != null;

        /// <summary>The held item's pose in the item convention: a striking head turned onto its neck-to-crown axis.</summary>
        private BodyPose HeldItemPose(Side side, Rigidbody2D item) =>
            IsStriking(side) ? HeldItemShape.HeadAsItem(PoseOf(item)) : PoseOf(item);

        private BodyPose PoseOf(Rigidbody2D body) => new BodyPose(Space.ToArena(body.position), body.rotation);

        private Rigidbody2D HeldItem(Side side) => _fighters[(int)side]?.Ragdoll.ActiveHeldItem;

        private float GravityScale(SimulationSettings simulation)
        {
            float engineGravity = Physics2D.gravity.magnitude;
            if (engineGravity <= 0f)
            {
                Debug.LogWarning("[XRim] Physics 2D gravity is zero, so the simulation's own gravity cannot be applied.");
                return 0f;
            }

            return Space.ToWorldLength(simulation.GravityUnitsPerSecondSquared) / engineGravity;
        }

        private void Clear()
        {
            if (_container != null) Object.DestroyImmediate(_container);
            _container = null;
            for (int i = 0; i < SideCount; i++)
            {
                _fighters[i] = null;
            }

            _severedLimbs.Clear();
            for (int i = 0; i < SideCount; i++)
            {
                _electricWalls[i] = null;
            }

            _tracked.Clear();
            _indexByCollider.Clear();
            _touching.Clear();
            _pendingContacts.Clear();
        }

        private sealed class Fighter
        {
            public Ragdoll Ragdoll { get; }
            public Rigidbody2D Torso { get; }
            public Rigidbody2D RootAnchor { get; }

            /// <summary>The pose this fighter was loaded with (parts left off keep it).</summary>
            public FighterPose LoadedPose { get; }

            public bool KinematicRoot { get; set; }
            public Vector2 PushAcceleration { get; set; }
            public float PushAngularAccelerationDegrees { get; set; }

            public Fighter(Ragdoll ragdoll, Rigidbody2D torso, Rigidbody2D rootAnchor, FighterPose loadedPose)
            {
                Ragdoll = ragdoll;
                Torso = torso;
                RootAnchor = rootAnchor;
                LoadedPose = loadedPose;
            }
        }

        private readonly struct SeveredLimb
        {
            /// <summary>The limb, or its upper segment.</summary>
            public Rigidbody2D Body { get; }

            /// <summary>Its forearm or shin when it kept one; null otherwise.</summary>
            public Rigidbody2D Lower { get; }

            public Side Owner { get; }
            public BodyPart Part { get; }

            public SeveredLimb(Rigidbody2D body, Rigidbody2D lower, Side owner, BodyPart part)
            {
                Body = body;
                Lower = lower;
                Owner = owner;
                Part = part;
            }
        }

        private sealed class TrackedBody
        {
            private Vector2 _lastPosition;
            private float _lastRotationDegrees;
            private Vector2 _velocity;
            private float _angularVelocityDegrees;

            public Rigidbody2D Body { get; }
            public BodyTag Tag { get; }

            /// <summary>The floor or an edge wall: static, never reported as a contact.</summary>
            public bool IsArena { get; }

            public TrackedBody(Rigidbody2D body, BodyTag tag, bool isArena)
            {
                Body = body;
                Tag = tag;
                IsArena = isArena;
                _lastPosition = body.position;
                _lastRotationDegrees = body.rotation;
            }

            /// <summary>Call at the start of a step: the motion since the start of the previous step becomes the velocity.</summary>
            public void RememberMotion(float deltaSeconds)
            {
                Vector2 position = Body.position;
                float rotation = Body.rotation;
                _velocity = (position - _lastPosition) / deltaSeconds;
                _angularVelocityDegrees = Mathf.DeltaAngle(_lastRotationDegrees, rotation) / deltaSeconds;
                _lastPosition = position;
                _lastRotationDegrees = rotation;
            }

            /// <summary>The body's velocity at a world point, over the step before the current one.</summary>
            public Vector2 VelocityAt(Vector2 point)
            {
                Vector2 arm = point - _lastPosition;
                float omega = _angularVelocityDegrees * Mathf.Deg2Rad;
                return _velocity + new Vector2(-omega * arm.y, omega * arm.x);
            }
        }

        private readonly struct NewPair
        {
            public long Key { get; }
            public int A { get; }
            public int B { get; }
            public Vector2 PointSum { get; }
            public Vector2 NormalSum { get; }
            public int Count { get; }

            public NewPair(long key, int a, int b, Vector2 point, Vector2 normal) : this(key, a, b, point, normal, 1)
            {
            }

            private NewPair(long key, int a, int b, Vector2 pointSum, Vector2 normalSum, int count)
            {
                Key = key;
                A = a;
                B = b;
                PointSum = pointSum;
                NormalSum = normalSum;
                Count = count;
            }

            public NewPair With(Vector2 point, Vector2 normal) => new NewPair(Key, A, B, PointSum + point, NormalSum + normal, Count + 1);
        }
    }
}
