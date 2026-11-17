using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Turns a WeaponController into a close-range melee weapon (e.g. a knife).
    /// When the weapon "shoots", this casts a short sphere in front of the player's camera
    /// and damages the first things it hits, then plays a procedural slash animation.
    /// </summary>
    [RequireComponent(typeof(WeaponController))]
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("Melee")] [Tooltip("Damage dealt to each target hit by a swing")]
        public float Damage = 50f;

        [Tooltip("How far in front of the camera the swing reaches")]
        public float Range = 2.2f;

        [Tooltip("Thickness of the swing (radius of the sphere cast)")]
        public float Radius = 0.45f;

        [Tooltip("Layers the swing can hit")]
        public LayerMask HittableLayers = -1;

        [Header("Animation")] [Tooltip("Transform that is rotated to animate the slash (usually the knife model)")]
        public Transform SwingPivot;

        [Tooltip("Duration of the slash animation")]
        public float SwingDuration = 0.25f;

        [Tooltip("Rotation applied at the peak of the slash (euler angles)")]
        public Vector3 SwingRotation = new Vector3(65f, -35f, 20f);

        [Tooltip("Offset applied at the peak of the slash (local space)")]
        public Vector3 SwingOffset = new Vector3(-0.08f, 0.02f, 0.18f);

        [Header("Feedback")] [Tooltip("Sound played when the swing hits something")]
        public AudioClip HitSfx;

        [Tooltip("Optional VFX spawned at the hit point")]
        public GameObject ImpactVfx;

        WeaponController m_Weapon;
        Transform m_AimTransform;
        Vector3 m_PivotRestPosition;
        Quaternion m_PivotRestRotation;
        float m_SwingStartTime = Mathf.NegativeInfinity;

        readonly HashSet<Health> m_HitThisSwing = new HashSet<Health>();

        void Awake()
        {
            m_Weapon = GetComponent<WeaponController>();
            if (SwingPivot)
            {
                m_PivotRestPosition = SwingPivot.localPosition;
                m_PivotRestRotation = SwingPivot.localRotation;
            }
        }

        void OnEnable() => GetComponent<WeaponController>().OnShoot += Swing;
        void OnDisable() => GetComponent<WeaponController>().OnShoot -= Swing;

        Transform GetAimTransform()
        {
            if (m_AimTransform == null && m_Weapon.Owner != null)
            {
                Camera cam = m_Weapon.Owner.GetComponentInChildren<Camera>();
                m_AimTransform = cam ? cam.transform : null;
            }

            return m_AimTransform ? m_AimTransform : m_Weapon.WeaponMuzzle;
        }

        void Swing()
        {
            m_SwingStartTime = Time.time;
            m_HitThisSwing.Clear();

            Transform aim = GetAimTransform();
            RaycastHit[] hits = Physics.SphereCastAll(aim.position, Radius, aim.forward, Range, HittableLayers,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // never hit ourselves
                if (m_Weapon.Owner && hit.collider.transform.IsChildOf(m_Weapon.Owner.transform))
                    continue;

                Damageable damageable = hit.collider.GetComponent<Damageable>();
                if (damageable == null)
                {
                    // a wall/floor blocks the swing from reaching anything behind it
                    // (distance 0 means the sphere started overlapping it, e.g. the floor under us)
                    if (hit.distance > 0f)
                        break;
                    continue;
                }

                if (damageable.Health == null || m_HitThisSwing.Contains(damageable.Health))
                    continue;

                m_HitThisSwing.Add(damageable.Health);
                damageable.InflictDamage(Damage, false, m_Weapon.Owner);

                Vector3 point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(aim.position);
                if (ImpactVfx)
                    Destroy(Instantiate(ImpactVfx, point, Quaternion.LookRotation(-aim.forward)), 2f);
                if (HitSfx)
                    AudioUtility.CreateSFX(HitSfx, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            }
        }

        void LateUpdate()
        {
            if (!SwingPivot)
                return;

            float t = (Time.time - m_SwingStartTime) / Mathf.Max(0.01f, SwingDuration);
            if (t >= 1f || t < 0f)
            {
                SwingPivot.localPosition = m_PivotRestPosition;
                SwingPivot.localRotation = m_PivotRestRotation;
                return;
            }

            // fast strike then slower recovery
            float weight = t < 0.35f ? Mathf.SmoothStep(0f, 1f, t / 0.35f) : Mathf.SmoothStep(1f, 0f, (t - 0.35f) / 0.65f);
            SwingPivot.localPosition = m_PivotRestPosition + SwingOffset * weight;
            SwingPivot.localRotation = m_PivotRestRotation * Quaternion.Euler(SwingRotation * weight);
        }
    }
}
