using System;
using UnityEngine;

namespace XRim.Simulation.Unity2D
{
    /// <summary>One weapon, shield or club body a ragdoll can hold, by weapon id. Only the held one is active.</summary>
    [Serializable]
    public struct HeldItemSlot
    {
        [SerializeField] private string _weaponId;
        [SerializeField] private Rigidbody2D _body;

        public string WeaponId => _weaponId;
        public Rigidbody2D Body => _body;

        public HeldItemSlot(string weaponId, Rigidbody2D body)
        {
            _weaponId = weaponId;
            _body = body;
        }
    }
}
