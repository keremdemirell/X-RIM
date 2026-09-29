using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>
    /// Scale between arena units and Unity world units. Box2D behaves best with bodies of roughly 0.1 to 10 world units,
    /// which this scale should respect once the dummy's size in arena units is known.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Arena Space", fileName = "ArenaSpace")]
    public sealed class ArenaSpaceConfig : ScriptableObject
    {
        public const float DefaultWorldUnitsPerArenaUnit = 0.01f;

        [Tooltip("Unity world units per arena unit. 0.01 makes a 600-unit rapier path 6 world units. GDD §18: the real scale is TBD.")]
        [GddTbd("§18", "Arena unit scale and reference resolution")]
        [Placeholder("§18 the real scale of an arena unit is TBD")]
        [SerializeField] private float _worldUnitsPerArenaUnit = DefaultWorldUnitsPerArenaUnit;

        public ArenaSpace CreateSpace() => new ArenaSpace(_worldUnitsPerArenaUnit);
    }
}
