using UnityEngine;

/// <summary>
/// Smooth, Input-System-independent chase camera for the drone prefab.
/// </summary>
public sealed class DroneCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 3f, -6f);
    [SerializeField, Min(0f)] private float smoothTime = 0.18f;
    [SerializeField, Min(0f)] private float lookHeight = 0.8f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.TransformPoint(followOffset);
        transform.position = smoothTime <= 0f
            ? desiredPosition
            : Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        Vector3 lookDirection = lookPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
    }
}
