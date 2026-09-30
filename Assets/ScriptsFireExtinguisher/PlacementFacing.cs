using UnityEngine;

/// <summary>
/// A floor tap's hit pose carries no meaningful yaw, so anything placed at it faces a random
/// way. This turns a placed object about the vertical axis so its front points at the player.
/// </summary>
public static class PlacementFacing
{
    /// <param name="placed">The placed object (rotated in world space, around its own position).</param>
    /// <param name="localFront">The object's own front axis: Vector3.forward for +Z fronts, Vector3.right for +X.</param>
    public static void FaceCamera(Transform placed, Vector3 localFront)
    {
        Camera cam = Camera.main;
        if (placed == null || cam == null)
        {
            return;
        }

        Vector3 toCamera = cam.transform.position - placed.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion faceZ = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        // Rotate the chosen front axis onto +Z first, then turn +Z toward the player.
        Quaternion frontToZ = Quaternion.FromToRotation(localFront.normalized, Vector3.forward);
        placed.rotation = faceZ * frontToZ;
    }
}
