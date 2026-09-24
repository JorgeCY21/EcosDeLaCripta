using EcosDeLaCripta.Combat;
using EcosDeLaCripta.Core;
using EcosDeLaCripta.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EcosDeLaCripta.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float moveSmoothTime = 0.12f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.15f;

        [Header("Impulso evasivo")]
        [SerializeField] private float dashSpeed = 14f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 1.1f;

        [Header("Combate")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.32f;
        [SerializeField] private int projectileDamage = 1;

        [Header("Power-ups")]
        [SerializeField] private float powerUpDuration = 8f;

        [Header("Audio")]
        [SerializeField] private AudioClip shootClip;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private float footstepInterval = 0.35f;
        private float footstepTimer;

        private CharacterController controller;
        private Health health;
        private Camera cam;
        private Animator animator;
        private float fireTimer;
        private float verticalVelocity;
        private Vector3 facing = Vector3.forward;
        private Vector3 aimDirection = Vector3.forward;

        private Vector3 currentMoveVelocity;
        private Vector3 moveVelocityRef;

        private float speedMultiplier = 1f;
        private int damageBonus = 0;
        private float speedBoostTimer;
        private float damageBoostTimer;
        private float dashTimer;
        private float dashCooldownTimer;
        private Vector3 dashDirection;
        private TrailRenderer dashTrail;

        public string ActivePowerUpLabel { get; private set; } = "";
        public float DashCooldown01 => dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(dashCooldownTimer / dashCooldown);
        public event System.Action<string> OnPowerUpCollected;

        /// <summary>Usado por el LevelBuilder de editor para enlazar el prefab de proyectil y el punto de disparo.</summary>
        public void Configure(Projectile projectile, Transform muzzleTransform)
        {
            projectilePrefab = projectile;
            muzzle = muzzleTransform;
        }

        public void ConfigureAudio(AudioClip shoot, AudioClip[] footsteps)
        {
            shootClip = shoot;
            footstepClips = footsteps;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            cam = Camera.main;
            animator = GetComponentInChildren<Animator>();
            dashTrail = GetComponent<TrailRenderer>();
            if (dashTrail != null) dashTrail.emitting = false;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            HandleAiming();
            HandleMovement();
            HandleShooting();
            TickPowerUps();
            if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
        }

        /// <summary>El jugador siempre mira/apunta hacia el punto del suelo bajo el cursor (esquema twin-stick),
        /// independiente de hacia dónde se mueva, para poder disparar con precisión sin tener que "correr apuntando".</summary>
        private void HandleAiming()
        {
            if (cam == null) return;
            CameraFollow cameraRig = cam.GetComponent<CameraFollow>();

            if (cameraRig != null && cameraRig.IsFirstPerson)
            {
                aimDirection = cam.transform.forward.normalized;
                Vector3 planarFacing = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
                if (planarFacing.sqrMagnitude > 0.01f) facing = planarFacing.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing, Vector3.up), turnSpeed * Time.deltaTime);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, transform.position);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                Vector3 aimDir = hitPoint - transform.position;
                aimDir.y = 0f;
                if (aimDir.sqrMagnitude > 0.01f)
                {
                    facing = aimDir.normalized;
                    aimDirection = facing;
                }
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing, Vector3.up), turnSpeed * Time.deltaTime);
        }

        private void HandleMovement()
        {
            var kb = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 camForward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;

            Vector3 moveDir = camForward * input.y + camRight * input.x;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            bool dashPressed = kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame);
            if (dashPressed && dashCooldownTimer <= 0f && moveDir.sqrMagnitude > 0.01f)
            {
                dashDirection = moveDir.normalized;
                dashTimer = dashDuration;
                dashCooldownTimer = dashCooldown;
                health.SetInvulnerable(dashDuration + 0.08f);
                if (dashTrail != null) dashTrail.emitting = true;
                CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
                if (follow != null) follow.Shake(0.1f, 0.08f);
            }

            // Suaviza aceleración/frenado en vez de moverse a velocidad máxima de golpe (se sentía brusco).
            if (dashTimer > 0f)
            {
                dashTimer -= Time.deltaTime;
                currentMoveVelocity = dashDirection * dashSpeed;
            }
            else
            {
                if (dashTrail != null) dashTrail.emitting = false;
                Vector3 targetVelocity = moveDir * (moveSpeed * speedMultiplier);
                currentMoveVelocity = Vector3.SmoothDamp(currentMoveVelocity, targetVelocity, ref moveVelocityRef, moveSmoothTime);
            }

            bool jumpPressed = kb != null && kb.spaceKey.wasPressedThisFrame;
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -1f;
                if (jumpPressed) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = currentMoveVelocity + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat("Speed", currentMoveVelocity.magnitude / Mathf.Max(0.01f, moveSpeed));
            }

            HandleFootsteps(currentMoveVelocity.magnitude);
        }

        private void HandleFootsteps(float speed)
        {
            if (footstepClips == null || footstepClips.Length == 0) return;
            if (speed < 0.5f)
            {
                footstepTimer = 0f;
                return;
            }

            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                footstepTimer = footstepInterval;
                var clip = footstepClips[Random.Range(0, footstepClips.Length)];
                AudioSource.PlayClipAtPoint(clip, transform.position, 0.5f);
            }
        }

        private void HandleShooting()
        {
            fireTimer -= Time.deltaTime;
            var mouse = Mouse.current;
            bool wantsFire = mouse != null && mouse.leftButton.isPressed;

            if (wantsFire && fireTimer <= 0f && projectilePrefab != null)
            {
                fireTimer = fireCooldown;
                CameraFollow cameraRig = cam != null ? cam.GetComponent<CameraFollow>() : null;
                bool firstPerson = cameraRig != null && cameraRig.IsFirstPerson;
                Vector3 shotDirection = firstPerson ? cam.transform.forward.normalized : aimDirection.normalized;
                Vector3 spawnPos = firstPerson
                    ? cam.transform.position + shotDirection * 0.52f
                    : (muzzle != null ? muzzle.position : transform.position + facing * 0.8f + Vector3.up * 0.9f);
                Projectile p = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shotDirection, Vector3.up));
                p.Launch(shotDirection, projectileDamage + damageBonus, gameObject);
                if (animator != null) animator.SetTrigger("Attack");
                if (shootClip != null) AudioSource.PlayClipAtPoint(shootClip, spawnPos, 0.7f);
            }
        }

        public void ApplyPowerUp(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Health:
                    health.Heal(2);
                    ActivePowerUpLabel = "Vida +2";
                    break;
                case PowerUpType.Damage:
                    damageBonus = 1;
                    damageBoostTimer = powerUpDuration;
                    ActivePowerUpLabel = "Daño aumentado";
                    break;
                case PowerUpType.Speed:
                    speedMultiplier = 1.6f;
                    speedBoostTimer = powerUpDuration;
                    ActivePowerUpLabel = "Velocidad aumentada";
                    break;
            }
            OnPowerUpCollected?.Invoke(ActivePowerUpLabel);
        }

        private void TickPowerUps()
        {
            if (damageBoostTimer > 0f)
            {
                damageBoostTimer -= Time.deltaTime;
                if (damageBoostTimer <= 0f)
                {
                    damageBonus = 0;
                    if (ActivePowerUpLabel == "Daño aumentado") ActivePowerUpLabel = "";
                }
            }
            if (speedBoostTimer > 0f)
            {
                speedBoostTimer -= Time.deltaTime;
                if (speedBoostTimer <= 0f)
                {
                    speedMultiplier = 1f;
                    if (ActivePowerUpLabel == "Velocidad aumentada") ActivePowerUpLabel = "";
                }
            }
        }

        private void OnHealthDied()
        {
            if (animator != null) animator.SetTrigger("Die");
            GameManager.Instance?.OnPlayerDied();
        }

        private void OnEnable()
        {
            health = GetComponent<Health>();
            health.OnDied += OnHealthDied;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDied -= OnHealthDied;
        }
    }
}
