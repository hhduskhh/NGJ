using UnityEngine;

namespace Echoes
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        private void LateUpdate()
        {
            if (!Target) return;
            var p = Target.position;
            // Match the sprite grid; never feed snapped coordinates back into physics.
            transform.position = new Vector3(Mathf.Round(p.x * 16) / 16,
                Mathf.Round(p.y * 16) / 16, -10);
        }
    }
}
