using UnityEngine;

/// <summary>
/// Handles the Play and Reset buttons.
/// Play: shows the ball at the spawn point and lets it fall.
/// Reset: hides the ball again and puts the camera back at its starting view.
/// </summary>
public class BallDropController : MonoBehaviour
{
    [Tooltip("The ball's Rigidbody.")]
    public Rigidbody ball;

    [Tooltip("Where the ball is dropped from.")]
    public Transform spawnPoint;

    [Tooltip("The camera follow script, reset together with the scene.")]
    public CameraFollow cameraFollow;

    void Start()
    {
        HideBall();
    }

    /// <summary>Called by the Play button.</summary>
    public void Play()
    {
        if (ball == null || spawnPoint == null)
            return;

        // Move the ball while it is inactive, then show it and drop it.
        ball.gameObject.SetActive(false);
        ball.transform.SetPositionAndRotation(spawnPoint.position, Quaternion.identity);
        ball.gameObject.SetActive(true);
        ball.isKinematic = false;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.WakeUp();
    }

    /// <summary>Called by the Reset button.</summary>
    public void ResetScene()
    {
        HideBall();
        if (cameraFollow != null)
            cameraFollow.ResetToStart();
    }

    void HideBall()
    {
        if (ball == null)
            return;

        if (ball.gameObject.activeSelf && !ball.isKinematic)
        {
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
        }
        if (spawnPoint != null)
            ball.transform.SetPositionAndRotation(spawnPoint.position, Quaternion.identity);
        ball.gameObject.SetActive(false);
    }
}
