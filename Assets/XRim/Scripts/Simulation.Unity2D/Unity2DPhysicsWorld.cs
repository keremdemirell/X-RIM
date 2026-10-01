using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
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
    /// Standing: each torso is pulled toward an invisible kinematic root anchor by a strength-limited RelativeJoint2D
    /// (upright included), and every joint holds its rest angle with a hinge-motor servo. Both are solved inside the
    /// physics solver, so standing is stable, yet a hard hit can still knock the dummy (pillar 4). A kinematic-torso
    /// fallback exists (<see cref="RootDriveMode.Kinematic"/>).
    /// </para>
    /// <para>
    /// Contacts: after each step every body's contacts are polled; a pair of bodies from different owners that was not
    /// touching after the previous step is a new contact. Floor contacts are not reported and a dummy never collides
    /// with itself. Relative velocity is each body's motion over the step before (Box2D reports a contact one step after
    /// the motion that made it), measured from positions so it is right for kinematic bodies too: the approach, not the bounce.
    /// Order: by load-time body index, which is stable (Left parts, Left weapon, Right parts, Right weapon).
    /// </para>
    /// </summary>
    public sealed class Unity2DPhysicsWorld : IPhysicsWorld
    {
        private const string SceneNamePrefix = "XRim.SimulationPhysics.";
        private const string ContainerName = "Bodies";
        private const string FloorName = "Floor";
        private const string RootAnchorName = "RootAnchor";
        private const int SideCount = 2;

        /// <summary>The floor's thickness below the arena line y = 0; thick enough that nothing tunnels through it.</summary>
        private const float FloorThicknessUnits = 100f;

        /// <summary>Each collider keeps its own contact offset, so two bodies count as touching at twice that gap.</summary>
        private const int ContactOffsetsPerPair = 2;

        /// <summary>
        /// Unity reports a contact normal pointing from <see cref="ContactPoint2D.collider"/> toward
        /// <see cref="ContactPoint2D.otherCollider"/>. The PlayMode test ThrustIntoTorso_ReportsATimedContactFromWeaponToTorso checks it.
        /// </summary>
        private static readonly bool NormalPointsFromColliderToOtherCollider = true;

        private readonly PhysicsScene2D _physicsScene;
        private readonly RagdollPrefabSet _prefabs;
        private readonly List<ContactFacts> _pendingContacts = new List<ContactFacts>();
        private readonly Fighter[] _fighters = new Fighter[SideCount];
        private readonly List<TrackedBody> _tracked = new List<TrackedBody>();
        private readonly Dictionary<Collider2D, int> _indexByCollider = new Dictionary<Collider2D, int>();
        private readonly List<Collider2D> _colliderBuffer = new List<Collider2D>();
        private readonly List<ContactPoint2D> _contactBuffer = new List<ContactPoint2D>();
        private readonly List<NewPair> _newPairs = new List<NewPair>();
        private HashSet<long> _touching = new HashSet<long>();
        private HashSet<long> _touchingNow = new HashSet<long>();
        private SimulationSettings _simulation = new SimulationSettings();
        private GameObject _container;

        public Scene Scene { get; }
        public ArenaSpace Space { get; }

        /// <param name="space">Arena-to-world scale.</param>
        /// <param name="prefabs">The ragdoll prefabs <see cref="Load"/> spawns; null for a world that is only stepped (tests).</param>
        public Unity2DPhysicsWorld(ArenaSpace space, RagdollPrefabSet prefabs = null)
        {
            Space = space;
            _prefabs = prefabs;
            Scene = SceneManager.CreateScene(SceneNamePrefix + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physicsScene = Scene.GetPhysicsScene2D();
        }

        public float TouchDistanceUnits => Space.ToArenaLength(ContactOffsetsPerPair * Physics2D.defaultContactOffset);

        /// <summary>A fighter's ragdoll in the hidden scene (debug tools and tests); null before <see cref="Load"/>.</summary>
        public Ragdoll GetRagdoll(Side side) => _fighters[(int)side]?.Ragdoll;

        public void Load(PoseSnapshot pose, MatchState state, RulesSettings rules, SimulationSettings simulation)
        {
            Guard.NotNull(pose, nameof(pose));
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(rules, nameof(rules));
            _simulation = Guard.NotNull(simulation, nameof(simulation));
            if (_prefabs == null) throw new InvalidOperationException("This physics world was created without ragdoll prefabs.");

            Clear();
            _container = new GameObject(ContainerName);
            SceneManager.MoveGameObjectToScene(_container, Scene);

            Ragdoll template = _prefabs.For(simulation.Segmentation);
            float gravityScale = GravityScale(simulation);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                _fighters[(int)side] = LoadFighter(side, template, pose.Get(side), state.Fighters[side], rules, simulation, gravityScale);
            }

            CreateFloor(rules.Arena.WidthUnits);
            TrackBodies();
        }

        public void SetHeldItemTarget(Side side, BodyPose target)
        {
            Rigidbody2D item = HeldItem(side);
            if (item == null) return;
            if (item.bodyType != RigidbodyType2D.Kinematic) item.bodyType = RigidbodyType2D.Kinematic;
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

        public BodyState GetHeldItemState(Side side)
        {
            Rigidbody2D item = HeldItem(side);
            if (item == null) return default;
            return new BodyState(PoseOf(item), Space.ToArena(item.linearVelocity), item.angularVelocity);
        }

        public void SetRootTarget(Side side, BodyPose target)
        {
            Fighter fighter = _fighters[(int)side];
            if (fighter == null) return;
            Rigidbody2D driven = fighter.KinematicRoot ? fighter.Torso : fighter.RootAnchor;
            driven.MovePosition(Space.ToWorld(target.PositionUnits));
            driven.MoveRotation(target.RotationDegrees);
        }

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

        public void BreakJoint(Side side, BodyPart part) => _fighters[(int)side]?.Ragdoll.BreakJoint(part);

        public void DropHeldItem(Side side) => _fighters[(int)side]?.Ragdoll.DropHeldItem();

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
                if (fighter != null) Capture(fighter.Ragdoll, snapshot.Get(side));
            }

            return snapshot;
        }

        public bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond)
        {
            float maxSpeedWorld = Space.ToWorldLength(linearSpeedUnitsPerSecond);
            foreach (TrackedBody tracked in _tracked)
            {
                Rigidbody2D body = tracked.Body;
                if (tracked.IsFloor || body == null || !body.gameObject.activeInHierarchy) continue;
                if (body.linearVelocity.magnitude >= maxSpeedWorld) return false;
                if (Mathf.Abs(body.angularVelocity) >= angularSpeedDegreesPerSecond) return false;
            }

            return true;
        }

        public void Dispose()
        {
            Clear();
            if (Scene.IsValid() && Scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(Scene);
            }
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
                                 "current settings. Run XRim/Spike/Build Placeholder Dummies.");
            ragdoll.AssignOwner(side);

            WeaponStats weapon = rules.FindWeapon(state.CurrentWeapon);
            BodyPart arm = BodyParts.DominantArm(state.Handedness);
            Rigidbody2D item = null;
            if (pose.HasHeldItem && weapon != null)
            {
                item = ragdoll.Hold(weapon.Id, arm);
                if (item == null)
                {
                    Debug.LogWarning($"[XRim] The ragdoll prefab has no body for '{weapon.Id}'. Run XRim/Spike/Build Placeholder Dummies.");
                }
                else
                {
                    PlaceholderRagdollBuilder.FitHeldItem(item, weapon, Space);
                    item.bodyType = simulation.WeaponDriver == WeaponDriverKind.Kinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                }
            }

            ragdoll.ApplyTuning(simulation.Ragdoll, gravityScale, item != null ? arm : (BodyPart?)null);
            ragdoll.IgnoreSelfCollisions();

            var fighter = new Fighter(ragdoll, ragdoll.GetBody(BodyPart.Torso), CreateRootAnchor(side, pose.Get(BodyPart.Torso)));
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
            return fighter;
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

        private void CreateFloor(float widthUnits)
        {
            var floor = new GameObject(FloorName);
            floor.transform.SetParent(_container.transform, false);
            floor.transform.localPosition = new Vector3(0f, -Space.ToWorldLength(FloorThicknessUnits) * 0.5f, 0f);
            var body = floor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = floor.AddComponent<BoxCollider2D>();
            box.size = new Vector2(Space.ToWorldLength(widthUnits), Space.ToWorldLength(FloorThicknessUnits));
            floor.AddComponent<PhysicsBodyTag>().Configure(null, BodyRole.Floor, BodyPart.Torso);
        }

        /// <summary>Assigns every body a stable index: Left parts, Left weapon, Right parts, Right weapon, floor.</summary>
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
                if (fighter.Ragdoll.ActiveHeldItem != null) bodies.Add(fighter.Ragdoll.ActiveHeldItem);
            }

            foreach (Rigidbody2D body in bodies)
            {
                Track(body, false);
            }

            Transform floor = _container.transform.Find(FloorName);
            if (floor != null) Track(floor.GetComponent<Rigidbody2D>(), true);
        }

        private void Track(Rigidbody2D body, bool isFloor)
        {
            int index = _tracked.Count;
            BodyTag tag = body.TryGetComponent(out PhysicsBodyTag bodyTag) ? bodyTag.Tag : default;
            _tracked.Add(new TrackedBody(body, tag, isFloor));
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
                if (tracked.IsFloor || !tracked.Body.gameObject.activeInHierarchy) continue;
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
                    if (other <= self || _tracked[other].IsFloor) continue;

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

        private void Capture(Ragdoll ragdoll, FighterPose pose)
        {
            pose.HasLowerSegments = ragdoll.HasLowerSegments;
            foreach (BodyPart part in BodyParts.All)
            {
                Rigidbody2D upper = ragdoll.GetBody(part);
                if (upper != null) pose.Set(part, PoseOf(upper));
                Rigidbody2D lower = ragdoll.GetLowerBody(part);
                if (lower != null) pose.SetLower(part, PoseOf(lower));
            }

            Rigidbody2D item = ragdoll.ActiveHeldItem;
            pose.HasHeldItem = item != null;
            if (item != null) pose.HeldItem = PoseOf(item);
        }

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
            public bool KinematicRoot { get; set; }
            public Vector2 PushAcceleration { get; set; }
            public float PushAngularAccelerationDegrees { get; set; }

            public Fighter(Ragdoll ragdoll, Rigidbody2D torso, Rigidbody2D rootAnchor)
            {
                Ragdoll = ragdoll;
                Torso = torso;
                RootAnchor = rootAnchor;
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
            public bool IsFloor { get; }

            public TrackedBody(Rigidbody2D body, BodyTag tag, bool isFloor)
            {
                Body = body;
                Tag = tag;
                IsFloor = isFloor;
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
