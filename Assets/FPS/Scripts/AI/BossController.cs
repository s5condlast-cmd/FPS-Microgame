using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AI
{
    /// <summary>
    /// Final boss behaviour layered on top of a regular EnemyController / EnemyMobile.
    ///  - Always knows where the player is (never idles behind cover) and un-sticks itself if it stops moving.
    ///  - Phases: at each health threshold it raises a shield and summons drones. The shield drops when the
    ///    drones are destroyed OR after ShieldMaxDuration seconds (so the fight can never lock).
    ///  - Drives an animated model (Idle/Walk/Run/Shoot/Slash/Hit/Taunt/Death) through an Animator.
    ///  - Close-range blade slam with a clear wind-up so the player can back off.
    /// </summary>
    [RequireComponent(typeof(EnemyController), typeof(Health))]
    public class BossController : MonoBehaviour
    {
        [System.Serializable]
        public class Phase
        {
            [Range(0f, 1f)] public float HealthThreshold = 0.66f;
            public int MinionsToSpawn = 2;
            public float FireDelayMultiplier = 0.75f;
            public float MoveSpeedMultiplier = 1f;
            public string Announcement = "";
        }

        [Header("Identity")]
        public string BossName = "THE OVERSEER";

        [Header("Phases / minions")]
        public GameObject MinionPrefab;
        [Range(0f, 1f)] public float MinionDropRate = 0.75f;
        public float MinionSpawnRadius = 9f;
        public GameObject SpawnVfx;
        [Tooltip("The shield drops by itself after this many seconds, even if drones are still alive")]
        public float ShieldMaxDuration = 20f;
        public List<Phase> Phases = new List<Phase>();

        [Header("Animation")]
        [Tooltip("Animator of the visual model (parameters: Speed, Shoot, Slash, Hit, Taunt, Dead)")]
        public Animator ModelAnimator;
        public float HitReactCooldown = 2.5f;

        [Tooltip("Visual model root: scaled at runtime so the animated mesh is exactly ModelHeight tall, feet on the ground")]
        public Transform ModelRoot;
        public float ModelHeight = 6.5f;

        [Header("Melee slam")]
        public float SlamRange = 6f;
        public float SlamDamage = 12f;
        public float SlamCooldown = 4f;
        public float SlamWindup = 0.6f;

        [Header("Stuck detection")]
        public float StuckTime = 2.5f;

        Health m_Health;
        EnemyController m_Controller;
        NavMeshAgent m_Agent;
        Actor m_PlayerActor;
        Health m_PlayerHealth;
        readonly List<Health> m_Minions = new List<Health>();
        int m_NextPhase;
        bool m_Shielded;
        float m_ShieldTime;
        bool m_Dead;
        float m_BaseSpeed;
        WeaponController[] m_Weapons;
        float[] m_BaseFireDelays;
        float m_LastHitReact = -99f;
        float m_LastSlam = -99f;
        float m_SlamHitTime = -1f;
        float m_StuckTimer;
        float m_NextAwareness;
        GUIStyle m_LabelStyle;
        Texture2D m_White;

        static readonly int k_Speed = Animator.StringToHash("Speed");
        static readonly int k_Shoot = Animator.StringToHash("Shoot");
        static readonly int k_Slash = Animator.StringToHash("Slash");
        static readonly int k_Hit = Animator.StringToHash("Hit");
        static readonly int k_Taunt = Animator.StringToHash("Taunt");
        static readonly int k_Dead = Animator.StringToHash("Dead");

        void Start()
        {
            m_Health = GetComponent<Health>();
            m_Controller = GetComponent<EnemyController>();
            m_Agent = GetComponent<NavMeshAgent>();
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;
            m_Controller.onAttack += OnAttack;

            m_Weapons = GetComponentsInChildren<WeaponController>(true);
            m_BaseFireDelays = new float[m_Weapons.Length];
            for (int i = 0; i < m_Weapons.Length; i++)
                m_BaseFireDelays[i] = m_Weapons[i].DelayBetweenShots;

            Phases.Sort((a, b) => b.HealthThreshold.CompareTo(a.HealthThreshold));

            // make sure we start on the NavMesh
            if (m_Agent && !m_Agent.isOnNavMesh &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                m_Agent.Warp(hit.position);

            if (ModelAnimator)
                ModelAnimator.SetTrigger(k_Taunt);
        }

        void Update()
        {
            if (m_Dead || m_Health.CurrentHealth <= 0f)
                return;

            if (m_BaseSpeed <= 0f && m_Agent && m_Agent.speed > 0f)
                m_BaseSpeed = m_Agent.speed; // read after EnemyController applied the NavigationModule values

            if (m_PlayerActor == null)
            {
                var actors = FindAnyObjectByType<ActorsManager>();
                if (actors && actors.Player)
                {
                    m_PlayerActor = actors.Player.GetComponent<Actor>();
                    m_PlayerHealth = actors.Player.GetComponent<Health>();
                }
            }

            KeepTrackOfPlayer();
            UnstickIfNeeded();
            UpdateSlam();

            if (ModelAnimator && m_Agent)
                ModelAnimator.SetFloat(k_Speed, m_Agent.velocity.magnitude);

            // shield
            m_Minions.RemoveAll(m => m == null || m.CurrentHealth <= 0f);
            if (m_Shielded && (m_Minions.Count == 0 || Time.time - m_ShieldTime > ShieldMaxDuration))
            {
                m_Shielded = false;
                m_Health.Invincible = false;
                Announce(BossName + "'S SHIELD IS DOWN! ATTACK NOW!");
            }

            if (m_NextPhase < Phases.Count && m_Health.GetRatio() <= Phases[m_NextPhase].HealthThreshold)
                EnterPhase(Phases[m_NextPhase++]);
        }

        // ---------------------------------------------------------------- model fitting
        // Skinned mesh bounds are only correct once the rig has been animated, so we measure in play mode.
        int m_FitAttempts;
        bool m_ModelFitted;

        void LateUpdate()
        {
            if (m_ModelFitted || ModelRoot == null)
                return;
            if (++m_FitAttempts < 3)
                return; // let the Animator pose the rig first

            Bounds b = default;
            bool has = false;
            foreach (var r in ModelRoot.GetComponentsInChildren<Renderer>())
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }

            if (!has || b.size.y < 0.001f)
            {
                if (m_FitAttempts > 60) m_ModelFitted = true; // give up quietly
                return;
            }

            // uniform scale around the model pivot, then put the feet on the boss root and centre it
            float s = ModelHeight / b.size.y;
            Vector3 pivot = ModelRoot.position;
            Vector3 scaledMin = pivot + (b.min - pivot) * s;
            Vector3 scaledCenter = pivot + (b.center - pivot) * s;
            ModelRoot.localScale *= s;
            Vector3 root = transform.position;
            ModelRoot.position += new Vector3(root.x - scaledCenter.x, root.y - scaledMin.y, root.z - scaledCenter.z);
            m_ModelFitted = true;
        }

        // The vanilla AI only reacts to what it can see; the boss always knows where the player is,
        // so it walks around cover instead of idling on the spot.
        void KeepTrackOfPlayer()
        {
            if (m_PlayerActor == null || Time.time < m_NextAwareness)
                return;
            m_NextAwareness = Time.time + 0.5f;

            if (m_PlayerHealth && m_PlayerHealth.CurrentHealth <= 0f)
                return;

            var dm = m_Controller.DetectionModule;
            if (dm != null && !dm.IsSeeingTarget)
                dm.OnDamaged(m_PlayerActor.AimPoint ? m_PlayerActor.AimPoint.gameObject : m_PlayerActor.gameObject);
        }

        void UnstickIfNeeded()
        {
            if (!m_Agent || !m_Agent.enabled)
                return;

            if (!m_Agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit h, 15f, NavMesh.AllAreas))
                    m_Agent.Warp(h.position);
                return;
            }

            bool wantsToMove = m_Agent.hasPath && m_Agent.remainingDistance > m_Agent.stoppingDistance + 1.5f;
            if (wantsToMove && m_Agent.velocity.sqrMagnitude < 0.05f)
                m_StuckTimer += Time.deltaTime;
            else
                m_StuckTimer = 0f;

            if (m_StuckTimer > StuckTime)
            {
                m_StuckTimer = 0f;
                Vector3 random = transform.position + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * 4f;
                if (NavMesh.SamplePosition(random, out NavMeshHit h, 6f, NavMesh.AllAreas))
                    m_Agent.Warp(h.position);
                m_Agent.ResetPath();
            }
        }

        void UpdateSlam()
        {
            if (m_PlayerActor == null)
                return;

            Vector3 playerPos = m_PlayerActor.transform.position;
            float dist = Vector3.Distance(Flat(playerPos), Flat(transform.position));

            if (m_SlamHitTime < 0f && dist < SlamRange && Time.time - m_LastSlam > SlamCooldown)
            {
                m_LastSlam = Time.time;
                m_SlamHitTime = Time.time + SlamWindup;
                if (ModelAnimator) ModelAnimator.SetTrigger(k_Slash);
            }

            if (m_SlamHitTime > 0f && Time.time >= m_SlamHitTime)
            {
                m_SlamHitTime = -1f;
                if (dist < SlamRange + 1f && m_PlayerHealth)
                    m_PlayerHealth.TakeDamage(SlamDamage, gameObject);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void EnterPhase(Phase phase)
        {
            for (int i = 0; i < m_Weapons.Length; i++)
                m_Weapons[i].DelayBetweenShots = m_BaseFireDelays[i] * phase.FireDelayMultiplier;

            if (m_Agent && m_BaseSpeed > 0f)
                m_Agent.speed = m_BaseSpeed * phase.MoveSpeedMultiplier;

            if (!string.IsNullOrEmpty(phase.Announcement))
                Announce(phase.Announcement);

            if (ModelAnimator) ModelAnimator.SetTrigger(k_Taunt);

            if (MinionPrefab != null && phase.MinionsToSpawn > 0)
            {
                for (int i = 0; i < phase.MinionsToSpawn; i++)
                    SpawnMinion(i, phase.MinionsToSpawn);

                if (m_Minions.Count > 0)
                {
                    m_Shielded = true;
                    m_ShieldTime = Time.time;
                    m_Health.Invincible = true;
                }
            }
        }

        void SpawnMinion(int index, int count)
        {
            float angle = (360f / count) * index + Random.Range(-20f, 20f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 pos = transform.position + dir * MinionSpawnRadius;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                pos = hit.position;
            else if (NavMesh.SamplePosition(transform.position, out hit, 6f, NavMesh.AllAreas))
                pos = hit.position;
            else
                return;

            var minion = Instantiate(MinionPrefab, pos, Quaternion.LookRotation(-dir));
            var ec = minion.GetComponent<EnemyController>();
            if (ec)
                ec.DropRate = MinionDropRate;
            var detection = minion.GetComponentInChildren<DetectionModule>();
            if (detection)
                detection.DetectionRange = Mathf.Max(detection.DetectionRange, 60f); // minions hunt the player
            var h = minion.GetComponent<Health>();
            if (h)
                m_Minions.Add(h);

            if (SpawnVfx)
                Destroy(Instantiate(SpawnVfx, pos, Quaternion.identity), 4f);
        }

        void OnAttack()
        {
            if (ModelAnimator) ModelAnimator.SetTrigger(k_Shoot);
        }

        void OnDamaged(float damage, GameObject source)
        {
            if (ModelAnimator && Time.time - m_LastHitReact > HitReactCooldown)
            {
                m_LastHitReact = Time.time;
                ModelAnimator.SetTrigger(k_Hit);
            }
        }

        void OnDie()
        {
            m_Dead = true;
            m_Health.Invincible = false;
            Announce(BossName + " HAS FALLEN");

            if (ModelAnimator) ModelAnimator.SetBool(k_Dead, true);

            // stop moving / shooting while the death animation plays
            var mobile = GetComponent<EnemyMobile>();
            if (mobile) mobile.enabled = false;
            if (m_Agent && m_Agent.isOnNavMesh) m_Agent.isStopped = true;

            // the remaining minions go down with their master
            foreach (var m in m_Minions)
                if (m != null && m.CurrentHealth > 0f)
                    m.Kill();
            m_Minions.Clear();
        }

        static void Announce(string message)
        {
            DisplayMessageEvent evt = Events.DisplayMessageEvent;
            evt.Message = message;
            evt.DelayBeforeDisplay = 0f;
            EventManager.Broadcast(evt);
        }

        // ---------------------------------------------------------------- boss health bar
        void OnGUI()
        {
            if (m_Health == null || m_Dead)
                return;

            if (m_White == null)
            {
                m_White = Texture2D.whiteTexture;
                m_LabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = Mathf.RoundToInt(Screen.height * 0.022f)
                };
                m_LabelStyle.normal.textColor = Color.white;
            }

            float w = Screen.width * 0.5f;
            float h = Mathf.Max(10f, Screen.height * 0.018f);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.07f;

            string label = BossName;
            if (m_Shielded)
                label += "  —  SHIELDED: destroy the drones (" +
                         Mathf.CeilToInt(Mathf.Max(0f, ShieldMaxDuration - (Time.time - m_ShieldTime))) + "s)";
            GUI.Label(new Rect(x, y - h * 2.2f, w, h * 2f), label, m_LabelStyle);

            DrawRect(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0f, 0f, 0f, 0.7f));
            float ratio = Mathf.Clamp01(m_Health.GetRatio());
            Color fill = m_Shielded
                ? new Color(0.3f, 0.7f, 1f)
                : Color.Lerp(new Color(1f, 0.15f, 0.1f), new Color(1f, 0.6f, 0.1f), ratio);
            DrawRect(new Rect(x, y, w * ratio, h), fill);

            foreach (var p in Phases)
                DrawRect(new Rect(x + w * p.HealthThreshold - 1, y, 2, h), new Color(1f, 1f, 1f, 0.6f));
        }

        void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, m_White);
            GUI.color = old;
        }
    }
}
