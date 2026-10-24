using UnityEngine;

/// <summary>Attached delivery cargo which becomes a physical package when released.</summary>
public sealed class MissionDeliveryPackage : MonoBehaviour
{
    private Lvl2MissionController mission;
    private Rigidbody body;
    private Collider[] packageColliders;
    private bool released;
    private bool resolved;

    public bool IsReleased => released;

    public void AttachToDrone(Lvl2MissionController owner, Transform drone)
    {
        mission = owner;
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        packageColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider packageCollider in packageColliders)
            packageCollider.enabled = false;

        transform.SetParent(drone, false);
        transform.localScale = Vector3.one * 0.42f;
        transform.localPosition = new Vector3(0f, -1.05f, 0f);
        transform.localRotation = Quaternion.identity;
    }

    public void Release(Transform drone)
    {
        if (released) return;
        released = true;
        transform.SetParent(null, true);
        foreach (Collider packageCollider in packageColliders)
        {
            packageCollider.enabled = true;
            foreach (Collider droneCollider in drone.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(packageCollider, droneCollider, true);
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.WakeUp();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!released || resolved || mission == null)
            return;

        resolved = true;
        mission.OnPackageLanded(this, transform.position, collision.collider);
    }
}
