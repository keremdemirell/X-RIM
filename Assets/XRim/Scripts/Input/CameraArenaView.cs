using UnityEngine;
using XRim.Config;
using XRim.Core;

namespace XRim.Input
{
    /// <summary>Reads what an orthographic camera shows as an <see cref="ArenaViewFrame"/>. Read it each time: the camera may move.</summary>
    public static class CameraArenaView
    {
        private const float FullHeightPerOrthographicSize = 2f;

        public static ArenaViewFrame Read(Camera camera, ArenaSpace space)
        {
            Vector3 position = camera.transform.position;
            return new ArenaViewFrame(space.ToArena(new Vector2(position.x, position.y)),
                space.ToArenaLength(camera.orthographicSize * FullHeightPerOrthographicSize));
        }
    }
}
