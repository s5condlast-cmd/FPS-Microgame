using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Keeps an objective (usually a reach point) hidden and untouchable until
    /// another objective is completed. Used for "clear the area, then extract".
    /// </summary>
    public class ExtractionGate : MonoBehaviour
    {
        [Tooltip("Objective that must be completed before this one unlocks")]
        public Objective RequiredObjective;

        [Tooltip("Message shown when the gate unlocks")]
        public string UnlockMessage = "Extraction point unlocked";

        Collider[] m_Colliders;
        Renderer[] m_Renderers;

        void Awake()
        {
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            SetOpen(RequiredObjective == null);
            Objective.OnObjectiveCompleted += OnObjectiveCompleted;
        }

        void OnObjectiveCompleted(Objective objective)
        {
            if (objective != RequiredObjective)
                return;

            SetOpen(true);
            DisplayMessageEvent msg = Events.DisplayMessageEvent;
            msg.Message = UnlockMessage;
            msg.DelayBeforeDisplay = 1f;
            EventManager.Broadcast(msg);
        }

        void SetOpen(bool open)
        {
            foreach (var c in m_Colliders)
                if (c) c.enabled = open;
            foreach (var r in m_Renderers)
                if (r) r.enabled = open;
        }

        void OnDestroy()
        {
            Objective.OnObjectiveCompleted -= OnObjectiveCompleted;
        }
    }
}
