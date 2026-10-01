using UnityEngine;

namespace Gameplay
{
    public static class BoardCameraFitter
    {
        // Fit the board and the queue while keeping the scene's existing camera angle.
        public static void Fit(Camera camera, int columns, int rows, Vector3 worldOffset)
        {
            if (camera == null) return;

            Vector3 center = new Vector3(worldOffset.x + (columns - 1) * 0.5f,
                worldOffset.y + 1f, worldOffset.z + (rows - 3) * 0.5f);
            float tangentVertical = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float tangentHorizontal = tangentVertical * camera.aspect;
            const float viewFraction = 0.82f;
            float distance = camera.nearClipPlane + 1f;

            foreach (float x in new[] { worldOffset.x - 0.65f, worldOffset.x + columns - 0.35f })
            foreach (float y in new[] { worldOffset.y, worldOffset.y + 2.5f })
            foreach (float z in new[] { worldOffset.z - 2f, worldOffset.z + rows - 0.35f })
            {
                Vector3 offset = new Vector3(x, y, z) - center;
                float right = Mathf.Abs(Vector3.Dot(camera.transform.right, offset));
                float up = Mathf.Abs(Vector3.Dot(camera.transform.up, offset));
                float forward = Vector3.Dot(camera.transform.forward, offset);
                distance = Mathf.Max(distance,
                    right / (tangentHorizontal * viewFraction) - forward,
                    up / (tangentVertical * viewFraction) - forward);
            }

            camera.transform.position = center - camera.transform.forward * distance;
        }
    }
}
