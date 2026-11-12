using UnityEngine;

/// <summary>
/// Moves the camera along with the target (the ball). Each frame the camera
/// first updates its position (offset from the ball), then rotates to look at it.
/// While the ball is hidden, the camera stays at its starting view.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("The object the camera follows (the ball).")]
    public Transform target;

    [Tooltip("Camera position relative to the target, in world space.")]
    public Vector3 offset = new Vector3(6f, 4f, -10f);

    [Tooltip("Time (seconds) the camera takes to catch up with the target. 0 = rigid follow.")]
    public float smoothTime = 0.3f;

    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 velocity;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    // LateUpdate runs after physics and Update, so the ball has already moved this frame.
    void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return;

        // 1. Update the camera position.
        Vector3 desired = target.position + offset;
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
            : desired;

        // 2. Then look at the ball from the new position.
        transform.LookAt(target);
    }

    /// <summary>Puts the camera back at its starting view.</summary>
    public void ResetToStart()
    {
        transform.SetPositionAndRotation(startPosition, startRotation);
        velocity = Vector3.zero;
    }
}
