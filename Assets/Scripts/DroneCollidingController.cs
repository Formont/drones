using UnityEngine;

namespace DroneGameplay
{
    public class DroneCollidingController : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("CheckPoint"))
                return;

            CheckAction action = other.GetComponent<CheckAction>();
            if (action != null)
                action.DieAction(source);
        }
    }
}
