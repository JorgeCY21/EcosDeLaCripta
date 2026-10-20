using EcosDeLaCripta.Combat;
using EcosDeLaCripta.Core;
using EcosDeLaCripta.Player;
using UnityEngine;

namespace EcosDeLaCripta.Enemy
{
    public enum EnemyState { Patrol, Chase }

    /// <summary>
    /// IA de enemigo: máquina de estados Patrulla (Wander) -> Detección -> Persecución (Seek/Arrive).
    /// Implementa a mano los vectores pos/vel/acc y el steering del laboratorio 2
    /// (no delega el movimiento a NavMeshAgent.SetDestination).
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterController))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Movimiento (steering)")]
        [SerializeField] private float maxSpeed = 2.1f;
        [SerializeField] private float maxForce = 4.5f;
        [SerializeField] private float detectionRadius = 5.5f;
        [SerializeField] private float loseRadius = 7.5f; // histéresis: evita oscilar en el borde
        [SerializeField] private float slowRadius = 2.2f;

        [Header("Wander")]
        [SerializeField] private float wanderDistance = 2.4f;
        [SerializeField] private float wanderRadius = 1.6f;
        [SerializeField] private float wanderJitterDeg = 120f;
        [SerializeField] private float patrolLeashRadius = 4.5f;

        [Header("Combate")]
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float attackRange = 1.3f;
        [SerializeField] private float attackCooldown = 1.3f;
        [SerializeField] private int scoreReward = 100;
        [SerializeField] public bool isBoss = false;

        [Header("Ataque a distancia")]
        [SerializeField] private bool ranged;
        [SerializeField] private EnemyBolt boltPrefab;
        [SerializeField] private float fireRange = 7f;
        [SerializeField] private float preferredDistance = 4.2f;
        [SerializeField] private float projectileCooldown = 1.65f;
        [SerializeField] private bool bossRadialAttack;

        [Header("Límites opcionales de arena")]
        [SerializeField] private bool constrainToArena;
        [SerializeField] private Vector2 arenaMin;
        [SerializeField] private Vector2 arenaMax;

        [Header("Ruta opcional de waypoints")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float waypointReachDistance = 0.55f;
        [SerializeField] private bool alwaysFollowWaypoints;

        [Header("Ataque predictivo opcional")]
        [SerializeField] private bool predictiveRanged;
        [SerializeField] private float predictiveProjectileSpeed = 11f;
        [SerializeField] private float predictiveGravity = 9.8f;
        [SerializeField] private float predictiveChargeDuration = 0.65f;
        [SerializeField] private float predictiveCooldown = 3.2f;

        [Header("Audio")]
        [SerializeField] private AudioClip attackClip;

        public EnemyState State { get; private set; } = EnemyState.Patrol;

        /// <summary>Usado por el LevelBuilder de editor para ajustar el jefe de la sala final.</summary>
        public void ConfigureAsBoss(float speed, float force, int damage, int score, float detection, float lose)
        {
            isBoss = true;
            maxSpeed = speed;
            maxForce = force;
            attackDamage = damage;
            scoreReward = score;
            detectionRadius = detection;
            loseRadius = lose;
        }

        public void ConfigureAudio(AudioClip attack) => attackClip = attack;

        public void ConfigureRanged(EnemyBolt prefab, float range = 7f, float preferred = 4.2f, float cooldown = 1.65f)
        {
            ranged = true;
            boltPrefab = prefab;
            fireRange = range;
            preferredDistance = preferred;
            projectileCooldown = cooldown;
        }

        public void ConfigureBossSpecial(EnemyBolt prefab)
        {
            boltPrefab = prefab;
            bossRadialAttack = true;
        }

        public void ConfigureArenaBounds(float minX, float maxX, float minZ, float maxZ)
        {
            constrainToArena = true;
            arenaMin = new Vector2(minX, minZ);
            arenaMax = new Vector2(maxX, maxZ);
        }

        public void ConfigureWaypoints(Transform[] route, bool routeOnly = false)
        {
            waypoints = route;
            alwaysFollowWaypoints = routeOnly;
            waypointIndex = 0;
        }

        public void ConfigurePredictiveRanged(EnemyBolt prefab, float range = 10f, float projectileSpeed = 11f, float gravity = 12f, float cooldown = 2.8f)
        {
            ranged = true;
            predictiveRanged = true;
            boltPrefab = prefab;
            fireRange = range;
            predictiveProjectileSpeed = projectileSpeed;
            predictiveGravity = gravity;
            predictiveCooldown = cooldown;
            EnsureTrajectoryLine();
        }

        public void ConfigureVariant(float speed, float force, int damage, int score, float detection, float attackDistance = 1.3f)
        {
            maxSpeed = speed;
            maxForce = force;
            attackDamage = damage;
            scoreReward = score;
            detectionRadius = detection;
            loseRadius = detection + 2f;
            attackRange = attackDistance;
        }

        private Vector3 velocity;
        private Vector3 desired;
        private Vector3 wanderTarget;
        private float wanderAngle;
        private Vector3 patrolCenter;
        private Transform player;
        private Health health;
        private CharacterController controller;
        private Animator animator;
        private float attackTimer;
        private bool enraged;
        private float specialTimer = 3.5f;
        private int waypointIndex;
        private float predictiveTimer;
        private float predictiveChargeTimer;
        private bool predictiveCharging;
        private LineRenderer trajectoryLine;
        private readonly Collider[] separationBuffer = new Collider[12];
        private float separationRefresh;
        private Vector3 cachedSeparation;

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            patrolCenter = transform.position;
            wanderAngle = Random.Range(0f, 360f);
            separationRefresh = Random.Range(0f, 0.12f);
            EnsureTrajectoryLine();
        }

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) player = playerGo.transform;
            health.OnDied += HandleDeath;
            health.OnDamaged += HandleDamaged;
        }

        private void Update()
        {
            if (health.IsDead) return;
            if (player == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            float dt = Time.deltaTime;
            float dist = Vector3.Distance(transform.position, player.position);

            bool routeOnly = alwaysFollowWaypoints && waypoints != null && waypoints.Length > 0;
            if (!routeOnly)
            {
                if (State == EnemyState.Patrol && dist <= detectionRadius) State = EnemyState.Chase;
                else if (State == EnemyState.Chase && dist >= loseRadius) State = EnemyState.Patrol;
            }
            else
            {
                State = EnemyState.Patrol;
            }

            if (predictiveCharging && State != EnemyState.Chase && !routeOnly)
            {
                predictiveCharging = false;
                ClearTrajectory();
            }

            Vector3 steer;
            if (State == EnemyState.Chase)
            {
                if (ranged)
                {
                    attackTimer -= dt;
                    if (predictiveRanged)
                    {
                        steer = SteeringUtility.SeekArrive(transform.position, velocity, player.position,
                            maxSpeed, maxForce, preferredDistance, out desired);
                        UpdatePredictiveAttack(dist, dt);
                        ApplyMovement(steer, dt);
                        return;
                    }
                    if (dist < preferredDistance * 0.72f)
                    {
                        Vector3 fleeTarget = transform.position + (transform.position - player.position).normalized * 3f;
                        steer = SteeringUtility.Seek(transform.position, velocity, fleeTarget, maxSpeed, maxForce);
                        desired = (fleeTarget - transform.position).normalized * maxSpeed;
                    }
                    else
                    {
                        steer = SteeringUtility.SeekArrive(transform.position, velocity, player.position, maxSpeed, maxForce, preferredDistance, out desired);
                    }
                    if (dist <= fireRange && attackTimer <= 0f)
                    {
                        attackTimer = projectileCooldown;
                        FireBolt((player.position + Vector3.up * 0.45f) - (transform.position + Vector3.up * 0.65f));
                    }
                }
                else
                {
                    steer = SteeringUtility.SeekArrive(transform.position, velocity, player.position, maxSpeed, maxForce, slowRadius, out desired);
                    if (dist <= attackRange)
                    {
                        attackTimer -= dt;
                        if (attackTimer <= 0f)
                        {
                            attackTimer = attackCooldown;
                            var playerHealth = player.GetComponent<Health>();
                            if (playerHealth != null) playerHealth.TakeDamage(attackDamage);
                            if (animator != null) animator.SetTrigger("Attack");
                            if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.6f);
                        }
                    }
                }
            }
            else
            {
                if (waypoints != null && waypoints.Length > 0)
                {
                    Transform waypoint = waypoints[Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1)];
                    if (waypoint != null && Vector3.Distance(transform.position, waypoint.position) <= waypointReachDistance)
                        waypointIndex = (waypointIndex + 1) % waypoints.Length;

                    waypoint = waypoints[Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1)];
                    Vector3 waypointTarget = waypoint != null ? waypoint.position : transform.position;
                    steer = SteeringUtility.SeekArrive(transform.position, velocity, waypointTarget,
                        maxSpeed, maxForce, slowRadius, out desired);
                    wanderTarget = waypointTarget;
                    if (routeOnly && predictiveRanged)
                        UpdatePredictiveAttack(dist, dt);
                }
                else
                {
                    steer = SteeringUtility.WanderImproved(transform.position, velocity, ref wanderAngle,
                        wanderDistance, wanderRadius, wanderJitterDeg, dt, maxSpeed, maxForce, out desired, out wanderTarget);
                }

                // La ruta ya está acotada por sus propios puntos y por la arena.
                // La correa solo aplica al wander libre; si se usa sobre una ruta
                // puede impedir que el agente alcance los puntos más alejados.
                float leashDist = Vector3.Distance(transform.position, patrolCenter);
                if (!routeOnly && leashDist > patrolLeashRadius)
                {
                    steer += SteeringUtility.Seek(transform.position, velocity, patrolCenter, maxSpeed, maxForce) * 0.6f;
                }
            }

            separationRefresh -= dt;
            if (separationRefresh <= 0f)
            {
                separationRefresh = 0.12f;
                cachedSeparation = CalculateSeparation();
            }
            steer += cachedSeparation * 0.8f;

            if (isBoss && !enraged && health.Current <= Mathf.CeilToInt(health.MaxHealth * 0.5f))
            {
                enraged = true;
                maxSpeed *= 1.35f;
                maxForce *= 1.3f;
                attackCooldown *= 0.65f;
            }

            if (bossRadialAttack && State == EnemyState.Chase && boltPrefab != null)
            {
                specialTimer -= dt;
                if (specialTimer <= 0f)
                {
                    specialTimer = enraged ? 2.7f : 4.2f;
                    int count = enraged ? 14 : 10;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = i * Mathf.PI * 2f / count;
                        FireBolt(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                    }
                }
            }

            velocity = Vector3.ClampMagnitude(velocity + steer * dt, maxSpeed);
            Vector3 delta = velocity * dt;
            delta.y = -0.05f; // pequeño empuje hacia abajo para mantener contacto con el suelo
            if (constrainToArena)
            {
                Vector3 next = transform.position + delta;
                next.x = Mathf.Clamp(next.x, arenaMin.x, arenaMax.x);
                next.z = Mathf.Clamp(next.z, arenaMin.y, arenaMax.y);
                delta.x = next.x - transform.position.x;
                delta.z = next.z - transform.position.z;
            }
            controller.Move(delta);

            if (velocity.sqrMagnitude > 0.02f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity.normalized, Vector3.up), 10f * dt);
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", velocity.magnitude / Mathf.Max(0.01f, maxSpeed));
            }

        }

        private void ApplyMovement(Vector3 steer, float dt)
        {
            separationRefresh -= dt;
            if (separationRefresh <= 0f)
            {
                separationRefresh = 0.12f;
                cachedSeparation = CalculateSeparation();
            }
            steer += cachedSeparation * 0.8f;
            velocity = Vector3.ClampMagnitude(velocity + steer * dt, maxSpeed);
            Vector3 delta = velocity * dt;
            delta.y = -0.05f;
            if (constrainToArena)
            {
                Vector3 next = transform.position + delta;
                next.x = Mathf.Clamp(next.x, arenaMin.x, arenaMax.x);
                next.z = Mathf.Clamp(next.z, arenaMin.y, arenaMax.y);
                delta.x = next.x - transform.position.x;
                delta.z = next.z - transform.position.z;
            }
            controller.Move(delta);
            if (velocity.sqrMagnitude > 0.02f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity.normalized, Vector3.up), 10f * dt);
            if (animator != null) animator.SetFloat("Speed", velocity.magnitude / Mathf.Max(0.01f, maxSpeed));
        }

        private void UpdatePredictiveAttack(float distance, float dt)
        {
            if (predictiveCharging)
            {
                predictiveChargeTimer -= dt;
                DrawPredictiveTrajectory();
                if (predictiveChargeTimer <= 0f)
                {
                    predictiveCharging = false;
                    predictiveTimer = predictiveCooldown;
                    FirePredictiveBolt();
                    ClearTrajectory();
                }
                return;
            }

            predictiveTimer -= dt;
            if (distance <= fireRange && predictiveTimer <= 0f)
            {
                predictiveCharging = true;
                predictiveChargeTimer = predictiveChargeDuration;
                if (animator != null) animator.SetTrigger("Attack");
            }
            else
            {
                ClearTrajectory();
            }
        }

        private void FirePredictiveBolt()
        {
            if (boltPrefab == null || player == null) return;
            Vector3 origin = transform.position + Vector3.up * 1.05f;
            Vector3 launchVelocity;
            CalculateBallisticVelocity(origin, out launchVelocity, out _);
            EnemyBolt bolt = Instantiate(boltPrefab, origin, Quaternion.LookRotation(launchVelocity.normalized, Vector3.up));
            bolt.LaunchBallistic(launchVelocity, attackDamage, predictiveGravity);
            if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.45f);
        }

        private bool CalculateBallisticVelocity(Vector3 origin, out Vector3 launchVelocity, out float flightTime)
        {
            Vector3 target = GetPlayerAimPoint();
            PlayerController playerController = player.GetComponent<PlayerController>();
            Vector3 targetVelocity = playerController != null ? playerController.CurrentVelocity : Vector3.zero;
            launchVelocity = Vector3.zero;
            flightTime = 0.25f;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                target = GetPlayerAimPoint() + targetVelocity * flightTime;
                if (!SolveBallisticVelocity(origin, target, out launchVelocity, out flightTime))
                {
                    Vector3 fallback = target - origin;
                    launchVelocity = fallback.normalized * predictiveProjectileSpeed;
                    flightTime = Mathf.Clamp(fallback.magnitude / Mathf.Max(1f, predictiveProjectileSpeed), 0.25f, 2f);
                    return false;
                }
            }
            return true;
        }

        private bool SolveBallisticVelocity(Vector3 origin, Vector3 target, out Vector3 launchVelocity, out float flightTime)
        {
            Vector3 offset = target - origin;
            Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
            float distance = planar.magnitude;
            float speedSquared = predictiveProjectileSpeed * predictiveProjectileSpeed;
            float discriminant = speedSquared * speedSquared - predictiveGravity *
                (predictiveGravity * distance * distance + 2f * offset.y * speedSquared);

            if (distance < 0.05f || discriminant < 0f)
            {
                launchVelocity = Vector3.zero;
                flightTime = 0.25f;
                return false;
            }

            // Solución de arco bajo: conserva la caída física, pero evita el arco
            // excesivamente alto e inestable de la solución superior.
            // El denominador correcto de la ecuación es gravedad * distancia.
            float tanAngle = (speedSquared - Mathf.Sqrt(discriminant)) / (predictiveGravity * distance);
            float cos = 1f / Mathf.Sqrt(1f + tanAngle * tanAngle);
            float sin = tanAngle * cos;
            launchVelocity = planar.normalized * (predictiveProjectileSpeed * cos) + Vector3.up * (predictiveProjectileSpeed * sin);
            flightTime = distance / Mathf.Max(0.01f, predictiveProjectileSpeed * cos);
            return true;
        }

        private Vector3 GetPlayerAimPoint()
        {
            Camera mainCamera = Camera.main;
            CameraFollow cameraRig = mainCamera != null ? mainCamera.GetComponent<CameraFollow>() : null;
            if (cameraRig != null && cameraRig.IsFirstPerson)
                return mainCamera.transform.position;
            return player.position + Vector3.up * 0.9f;
        }

        private void EnsureTrajectoryLine()
        {
            if (!predictiveRanged || trajectoryLine != null) return;
            trajectoryLine = gameObject.GetComponent<LineRenderer>();
            if (trajectoryLine == null) trajectoryLine = gameObject.AddComponent<LineRenderer>();
            trajectoryLine.enabled = false;
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.startWidth = 0.08f;
            trajectoryLine.endWidth = 0.02f;
            trajectoryLine.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            trajectoryLine.sharedMaterial.color = new Color(1f, 0.25f, 0.06f, 0.8f);
        }

        private void DrawPredictiveTrajectory()
        {
            if (trajectoryLine == null) return;
            Vector3 origin = transform.position + Vector3.up * 1.05f;
            CalculateBallisticVelocity(origin, out Vector3 launchVelocity, out float duration);
            const int samples = 24;
            duration = Mathf.Clamp(duration, 0.35f, 2.2f);
            trajectoryLine.positionCount = samples;
            trajectoryLine.enabled = true;
            for (int i = 0; i < samples; i++)
            {
                float t = duration * i / (samples - 1f);
                trajectoryLine.SetPosition(i, origin + launchVelocity * t + Vector3.down * (0.5f * predictiveGravity * t * t));
            }
        }

        private void ClearTrajectory()
        {
            if (trajectoryLine == null) return;
            trajectoryLine.enabled = false;
            trajectoryLine.positionCount = 0;
        }

        private void HandleDeath()
        {
            if (animator != null) animator.SetTrigger("Die");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEnemyDefeated(scoreReward, isBoss);
            }
        }

        private void HandleDamaged(int amount)
        {
            if (!health.IsDead && !alwaysFollowWaypoints) State = EnemyState.Chase;
        }

        private void FireBolt(Vector3 direction)
        {
            if (boltPrefab == null) return;
            direction.y = 0f;
            Vector3 origin = transform.position + Vector3.up * 0.65f + direction.normalized * 0.45f;
            EnemyBolt bolt = Instantiate(boltPrefab, origin, Quaternion.LookRotation(direction.normalized, Vector3.up));
            bolt.Launch(direction, attackDamage);
            if (animator != null) animator.SetTrigger("Attack");
            if (attackClip != null) AudioSource.PlayClipAtPoint(attackClip, transform.position, 0.45f);
        }

        private Vector3 CalculateSeparation()
        {
            int nearbyCount = Physics.OverlapSphereNonAlloc(transform.position, 1.15f, separationBuffer);
            Vector3 separation = Vector3.zero;
            int count = 0;
            for (int i = 0; i < nearbyCount; i++)
            {
                Collider col = separationBuffer[i];
                EnemyAI other = col.GetComponent<EnemyAI>();
                if (other == null || other == this) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f)
                {
                    separation += away.normalized / Mathf.Max(0.2f, away.magnitude);
                    count++;
                }
            }
            if (count == 0) return Vector3.zero;
            Vector3 wanted = separation.normalized * maxSpeed;
            return Vector3.ClampMagnitude(wanted - velocity, maxForce);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDied -= HandleDeath;
                health.OnDamaged -= HandleDamaged;
            }
        }

    }
}
