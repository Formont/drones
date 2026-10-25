using System.Collections.Generic;
using UnityEngine;

/// <summary>Spins the BumbleBee's mesh-only propellers in lvl3 around their local Z axes.</summary>
public sealed class BumbleBeePropellerSpin : MonoBehaviour
{
    [SerializeField, Min(0f)] private float rotationSpeed = 1800f;
    [SerializeField] private DroneKeyboardController flightController;

    private Transform[] propellers;
    private float[] directions;

    private void Awake()
    {
        if (flightController == null)
            flightController = GetComponent<DroneKeyboardController>();

        var found = new List<Transform>();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform || child.GetComponent<Animator>() != null)
                continue;

            bool isPropeller = child.name.IndexOf("propeller", System.StringComparison.OrdinalIgnoreCase) >= 0
                               || child.name.IndexOf("rotor", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isPropeller && child.GetComponent<Renderer>() != null)
                found.Add(child);
        }

        propellers = found.ToArray();
        directions = new float[propellers.Length];
        for (int i = 0; i < directions.Length; i++)
            directions[i] = i % 2 == 0 ? 1f : -1f;
    }

    private void Update()
    {
        if (flightController == null || !flightController.MotorsRunning)
            return;

        float angle = rotationSpeed * Time.deltaTime;
        for (int i = 0; i < propellers.Length; i++)
        {
            Transform propeller = propellers[i];
            if (propeller != null && propeller.gameObject.activeInHierarchy)
                propeller.localRotation *= Quaternion.AngleAxis(angle * directions[i], Vector3.forward);
        }
    }
}
