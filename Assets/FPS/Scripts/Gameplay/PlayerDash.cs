using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Quick burst of movement in the direction the player is moving (or forward if standing still).
    /// Default: F on keyboard, Left Bumper on gamepad.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerCharacterController), typeof(PlayerInputHandler))]
    public class PlayerDash : MonoBehaviour
    {
        [Tooltip("Keyboard key that triggers the dash")]
        public Key DashKey = Key.F;

        [Tooltip("Distance covered by one dash (meters)")]
        public float DashDistance = 7f;

        [Tooltip("How long the dash lasts (seconds)")]
        public float DashDuration = 0.18f;

        [Tooltip("Time before the player can dash again (seconds)")]
        public float DashCooldown = 1.2f;

        [Tooltip("Can the player dash while in the air")]
        public bool AllowAirDash = true;

        [Tooltip("Make the player immune to damage during the dash")]
        public bool InvincibleWhileDashing = true;

        [Tooltip("Sound played when dashing")]
        public AudioClip DashSfx;

        public bool IsDashing { get; private set; }

        /// <summary>0 right after a dash, 1 when the dash is ready again.</summary>
        public float CooldownRatio => Mathf.Clamp01((Time.time - m_LastDashTime) / DashCooldown);

        CharacterController m_Controller;
        PlayerCharacterController m_Character;
        PlayerInputHandler m_Input;
        Health m_Health;
        AudioSource m_AudioSource;

        Vector3 m_DashDirection;
        float m_DashStartTime;
        float m_LastDashTime = Mathf.NegativeInfinity;

        void Start()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Character = GetComponent<PlayerCharacterController>();
            m_Input = GetComponent<PlayerInputHandler>();
            m_Health = GetComponent<Health>();
            m_AudioSource = GetComponent<AudioSource>();
        }

        bool DashPressed()
        {
            if (!m_Input.CanProcessInput())
                return false;

            bool pressed = Keyboard.current != null && Keyboard.current[DashKey].wasPressedThisFrame;
            if (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)
                pressed = true;
            return pressed;
        }

        void Update()
        {
            if (m_Character.IsDead)
            {
                EndDash();
                return;
            }

            if (!IsDashing && DashPressed() && CooldownRatio >= 1f && (AllowAirDash || m_Character.IsGrounded))
                StartDash();

            if (IsDashing)
            {
                float speed = DashDistance / DashDuration;
                m_Controller.Move(m_DashDirection * speed * Time.deltaTime);

                if (Time.time - m_DashStartTime >= DashDuration)
                    EndDash();
            }
        }

        void StartDash()
        {
            Vector3 input = m_Input.GetMoveInput();
            Vector3 dir = input.sqrMagnitude > 0.01f ? transform.TransformVector(input) : transform.forward;
            dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            if (dir == Vector3.zero)
                return;

            m_DashDirection = dir;
            m_DashStartTime = Time.time;
            m_LastDashTime = Time.time;
            IsDashing = true;

            // cancel falling speed so the dash feels snappy in the air
            Vector3 v = m_Character.CharacterVelocity;
            if (v.y < 0f)
                m_Character.CharacterVelocity = new Vector3(v.x, 0f, v.z);

            if (InvincibleWhileDashing && m_Health)
                m_Health.Invincible = true;

            if (DashSfx && m_AudioSource)
                m_AudioSource.PlayOneShot(DashSfx);
        }

        void EndDash()
        {
            if (!IsDashing)
                return;

            IsDashing = false;
            if (InvincibleWhileDashing && m_Health)
                m_Health.Invincible = false;
        }
    }
}
