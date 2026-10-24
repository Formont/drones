using UnityEngine;

/// <summary>Moves a spawned car along the scene-authored one-way traffic loop.</summary>
public sealed class MissionCarDriver : MonoBehaviour
{
    private Transform[] route;
    private float[] cumulativeLengths;
    private float routeLength;
    private float distanceTravelled;
    private float speed;

    public void Initialize(Transform[] routePoints, float startDistance, float cruiseSpeed)
    {
        route = routePoints;
        speed = cruiseSpeed;
        if (route == null || route.Length < 2)
            return;

        cumulativeLengths = new float[route.Length + 1];
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 from = route[i].position;
            Vector3 to = route[(i + 1) % route.Length].position;
            routeLength += Vector3.Distance(from, to);
            cumulativeLengths[i + 1] = routeLength;
        }

        distanceTravelled = Mathf.Repeat(startDistance, routeLength);
        UpdatePose();
    }

    private void Update()
    {
        if (routeLength <= 0.001f)
            return;

        distanceTravelled = Mathf.Repeat(distanceTravelled + speed * Time.deltaTime, routeLength);
        UpdatePose();
    }

    private void UpdatePose()
    {
        for (int i = 0; i < route.Length; i++)
        {
            if (distanceTravelled > cumulativeLengths[i + 1])
                continue;

            float segmentLength = cumulativeLengths[i + 1] - cumulativeLengths[i];
            float segmentProgress = segmentLength > 0f
                ? (distanceTravelled - cumulativeLengths[i]) / segmentLength
                : 0f;
            Vector3 from = route[i].position;
            Vector3 to = route[(i + 1) % route.Length].position;
            Vector3 direction = (to - from).normalized;
            transform.position = Vector3.Lerp(from, to, segmentProgress);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            return;
        }
    }
}
