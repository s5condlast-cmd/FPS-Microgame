using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Trigger volume that damages the player over time (lava, acid...).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HazardZone : MonoBehaviour
    {
        [Tooltip("Damage per second dealt to the player while inside")]
        public float DamagePerSecond = 35f;

        Health m_PlayerHealth;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerCharacterController>())
                m_PlayerHealth = other.GetComponent<Health>();
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<PlayerCharacterController>())
                m_PlayerHealth = null;
        }

        void Update()
        {
            if (m_PlayerHealth != null)
                m_PlayerHealth.TakeDamage(DamagePerSecond * Time.deltaTime, gameObject);
        }
    }
}
