using UnityEngine;

/// <summary>Spawns a small, evenly spaced group of cars on a one-way city loop.</summary>
public sealed class MissionTrafficSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] carPrefabs;
    [SerializeField] private Material carMaterialOverride;
    [SerializeField] private Transform[] routePoints;
    [SerializeField, Min(1)] private int vehicleCount = 4;
    [SerializeField, Min(0.1f)] private float vehicleScale = 0.4f;
    [SerializeField, Min(0f)] private float cruiseSpeed = 3f;

    private void Start()
    {
        if (carPrefabs == null || carPrefabs.Length == 0 || routePoints == null || routePoints.Length < 2)
        {
            Debug.LogWarning("MissionTrafficSpawner needs car prefabs and at least two route points.", this);
            return;
        }

        float routeLength = 0f;
        for (int i = 0; i < routePoints.Length; i++)
            routeLength += Vector3.Distance(routePoints[i].position, routePoints[(i + 1) % routePoints.Length].position);

        for (int i = 0; i < vehicleCount; i++)
        {
            GameObject prefab = carPrefabs[i % carPrefabs.Length];
            if (prefab == null)
                continue;

            GameObject car = Instantiate(prefab, transform);
            car.name = $"Traffic Car {i + 1:00}";
            car.transform.localScale = Vector3.one * vehicleScale;
            ApplyCarMaterial(car);
            car.AddComponent<MissionObstacle>();
            EnsureCollisionBounds(car);

            MissionCarDriver driver = car.AddComponent<MissionCarDriver>();
            driver.Initialize(routePoints, routeLength * i / vehicleCount, cruiseSpeed);
        }
    }

    private void ApplyCarMaterial(GameObject car)
    {
        if (carMaterialOverride == null)
            return;

        Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
                continue;

            for (int i = 0; i < materials.Length; i++)
                materials[i] = carMaterialOverride;
            renderer.sharedMaterials = materials;
        }
    }

    private static void EnsureCollisionBounds(GameObject car)
    {
        if (car.GetComponentInChildren<Collider>(true) != null)
            return;

        BoxCollider collider = car.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.6f, 0f);
        collider.size = new Vector3(3f, 1.4f, 5f);
    }
}
