using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Presentation.Dummy;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.App
{
    /// <summary>
    /// Builds a visible dummy from the physics ragdoll prefab with every physics component removed, so what is drawn has
    /// exactly the shapes the hidden simulation uses (placeholder art until Session 17). It only shows recorded poses.
    /// </summary>
    public static class VisualDummyFactory
    {
        public static DummyView Create(Ragdoll prefab, Transform parent, string name, IEnumerable<WeaponStats> weapons, ArenaSpace space)
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
            var heldItems = new Dictionary<string, Transform>();
            foreach (HeldItemSlot slot in copy.HeldItems)
            {
                if (slot.Body != null) heldItems[slot.WeaponId] = slot.Body.transform;
            }

            GameObject root = copy.gameObject;
            StripPhysics(root);
            root.name = name;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            Object.DestroyImmediate(holder);

            var view = root.AddComponent<DummyView>();
            view.Configure(parts, lowerParts);
            foreach (WeaponStats weapon in weapons)
            {
                if (!heldItems.TryGetValue(weapon.Id, out Transform item)) continue;
                PlaceholderRagdollBuilder.FitHeldItemVisual(item, weapon, space);
                view.AddHeldItem(weapon.WeaponId, item);
            }

            return view;
        }

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
