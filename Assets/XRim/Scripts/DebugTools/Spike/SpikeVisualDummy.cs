using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Presentation.Dummy;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.DebugTools.Spike
{
    /// <summary>
    /// A visible dummy for the feel spike: a copy of the physics ragdoll prefab with every physics component removed, so
    /// what is drawn always has exactly the shapes the hidden simulation used. It only shows recorded poses.
    /// </summary>
    internal sealed class SpikeVisualDummy
    {
        private readonly Dictionary<string, Transform> _heldItems = new Dictionary<string, Transform>();

        public GameObject Root { get; }
        public DummyView View { get; }

        private SpikeVisualDummy(GameObject root, DummyView view)
        {
            Root = root;
            View = view;
        }

        public static SpikeVisualDummy Create(Ragdoll prefab, Transform parent, string name)
        {
            // Instantiate under an inactive holder so no physics body is ever created in the visual scene.
            var holder = new GameObject(name + "Holder");
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);
            Ragdoll copy = Object.Instantiate(prefab, holder.transform);

            var parts = new Transform[BodyParts.Count];
            var lowerParts = new Transform[BodyParts.Count];
            foreach (BodyPart part in BodyParts.All)
            {
                parts[(int)part] = copy.GetBody(part) != null ? copy.GetBody(part).transform : null;
                Rigidbody2D lower = copy.GetLowerBody(part);
                lowerParts[(int)part] = lower != null ? lower.transform : null;
            }

            // Read every reference before stripping: the Ragdoll and the held items' Rigidbody2D are destroyed with it.
            var heldItems = new List<KeyValuePair<string, Transform>>();
            foreach (HeldItemSlot slot in copy.HeldItems)
            {
                if (slot.Body != null) heldItems.Add(new KeyValuePair<string, Transform>(slot.WeaponId, slot.Body.transform));
            }

            GameObject root = copy.gameObject;
            StripPhysics(root);

            root.name = name;
            root.transform.SetParent(parent, false);
            Object.DestroyImmediate(holder);
            var view = root.AddComponent<DummyView>();
            view.Configure(parts, lowerParts);

            var dummy = new SpikeVisualDummy(root, view);
            foreach (KeyValuePair<string, Transform> item in heldItems)
            {
                dummy._heldItems[item.Key] = item.Value;
                item.Value.gameObject.SetActive(false);
            }

            return dummy;
        }

        /// <summary>Shows a weapon in the hand, sized to its live stats; null shows none.</summary>
        public void Hold(WeaponStats weapon, ArenaSpace space)
        {
            Transform item = null;
            if (weapon != null && _heldItems.TryGetValue(weapon.Id, out item))
            {
                PlaceholderRagdollBuilder.FitHeldItemVisual(item, weapon, space);
            }

            View.SetHeldItem(item);
        }

        public void Destroy() => Object.Destroy(Root);

        /// <summary>Joints and tags need their Rigidbody2D, so they go first.</summary>
        private static void StripPhysics(GameObject root)
        {
            DestroyAll<Joint2D>(root);
            DestroyAll<PhysicsBodyTag>(root);
            DestroyAll<Collider2D>(root);
            DestroyAll<Rigidbody2D>(root);
            DestroyAll<Ragdoll>(root);
        }

        private static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
