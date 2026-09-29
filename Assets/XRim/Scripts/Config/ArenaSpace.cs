using System;
using UnityEngine;
using XRim.Core;

namespace XRim.Config
{
    /// <summary>Converts between abstract arena units (rules, paths) and Unity world units (physics, visuals).</summary>
    public readonly struct ArenaSpace
    {
        public float WorldUnitsPerArenaUnit { get; }

        public ArenaSpace(float worldUnitsPerArenaUnit)
        {
            if (worldUnitsPerArenaUnit <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerArenaUnit), worldUnitsPerArenaUnit, "Must be greater than zero.");
            }

            WorldUnitsPerArenaUnit = worldUnitsPerArenaUnit;
        }

        public Vector2 ToWorld(Vec2 arenaUnits) => new Vector2(arenaUnits.X * WorldUnitsPerArenaUnit, arenaUnits.Y * WorldUnitsPerArenaUnit);

        public Vec2 ToArena(Vector2 world) => new Vec2(world.x / WorldUnitsPerArenaUnit, world.y / WorldUnitsPerArenaUnit);

        public float ToWorldLength(float arenaUnits) => arenaUnits * WorldUnitsPerArenaUnit;

        public float ToArenaLength(float worldUnits) => worldUnits / WorldUnitsPerArenaUnit;
    }
}
