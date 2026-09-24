using EcosDeLaCripta.Combat;
using EcosDeLaCripta.Core;
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

            if (State == EnemyState.Patrol && dist <= detectionRadius) State = EnemyState.Chase;
            else if (State == EnemyState.Chase && dist >= loseRadius) State = EnemyState.Patrol;

            Vector3 steer;
            if (State == EnemyState.Chase)
            {
                if (ranged)
                {
                    attackTimer -= dt;
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
                steer = SteeringUtility.WanderImproved(transform.position, velocity, ref wanderAngle,
                    wanderDistance, wanderRadius, wanderJitterDeg, dt, maxSpeed, maxForce, out desired, out wanderTarget);

                // Correa suave hacia el centro de patrulla para no perderse en otras salas.
                float leashDist = Vector3.Distance(transform.position, patrolCenter);
                if (leashDist > patrolLeashRadius)
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
            if (!health.IsDead) State = EnemyState.Chase;
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
