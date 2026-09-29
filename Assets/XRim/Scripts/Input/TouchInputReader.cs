using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace XRim.Input
{
    /// <summary>
    /// Turns on the Input System's EnhancedTouch API (multi-finger touch with per-finger history) while enabled.
    /// Input schemes read touches through it. The legacy UnityEngine.Input class is never used.
    /// </summary>
    public sealed class TouchInputReader : MonoBehaviour
    {
        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();
    }
}
