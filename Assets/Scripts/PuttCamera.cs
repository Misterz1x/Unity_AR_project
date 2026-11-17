using UnityEngine;

/// <summary>
/// Keeps the camera behind the ball, looking along the aim direction.
/// Turns with the arrow while aiming and follows the ball while it rolls.
/// </summary>
public class PuttCamera : MonoBehaviour
{
    public Transform ball;
    public AimPowerLauncher launcher;

    [Tooltip("Distance behind the ball (m).")]
    public float distance = 1.8f;

    [Tooltip("Height above the ball (m).")]
    public float height = 1.3f;

    [Tooltip("The camera looks at this point in front of the ball (m).")]
    public float lookAhead = 2.2f;

    [Tooltip("Time to catch up (s). 0 = no smoothing.")]
    public float smoothTime = 0.3f;

    Vector3 velocity;
    Vector3 lookPoint;
    Vector3 lookVelocity;

    void Start() => Snap();

    /// <summary>Jumps straight to the target view.</summary>
    public void Snap()
    {
        if (!Target(out Vector3 pos, out Vector3 look))
            return;
        transform.position = pos;
        lookPoint = look;
        velocity = lookVelocity = Vector3.zero;
        transform.LookAt(lookPoint);
    }

    void LateUpdate()
    {
        if (!Target(out Vector3 pos, out Vector3 look))
            return;
        transform.position = Vector3.SmoothDamp(transform.position, pos, ref velocity, smoothTime);
        lookPoint = Vector3.SmoothDamp(lookPoint, look, ref lookVelocity, smoothTime);
        transform.LookAt(lookPoint);
    }

    bool Target(out Vector3 pos, out Vector3 look)
    {
        pos = look = default;
        if (ball == null)
            return false;
        Vector3 dir = launcher != null ? launcher.AimDirection : Vector3.forward;
        pos = ball.position - dir * distance + Vector3.up * height;
        look = ball.position + dir * lookAhead;
        return true;
    }
}
