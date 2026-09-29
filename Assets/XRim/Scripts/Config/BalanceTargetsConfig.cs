using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>
    /// Targets that tuning aims for, measured by telemetry and bot-vs-bot batches. Never read by the rules.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Root + "Balance Targets", fileName = "BalanceTargets")]
    public sealed class BalanceTargetsConfig : ScriptableObject
    {
        [Tooltip("GDD §11 / Appendix A: severing should happen in about 10–20% of matches. Lower bound.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dismembermentMatchRateMin = 0.10f;

        [Tooltip("GDD §11 / Appendix A: upper bound of the dismemberment target.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dismembermentMatchRateMax = 0.20f;

        [Tooltip("GDD §1: target match length is TBD. 0 = not decided.")]
        [GddTbd("§1", "Target match length")]
        [SerializeField] private float _targetMatchLengthSeconds = 0f;

        public float DismembermentMatchRateMin => _dismembermentMatchRateMin;
        public float DismembermentMatchRateMax => _dismembermentMatchRateMax;
        public float TargetMatchLengthSeconds => _targetMatchLengthSeconds;
    }
}
