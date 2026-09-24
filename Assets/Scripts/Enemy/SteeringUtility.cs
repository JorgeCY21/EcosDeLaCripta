using UnityEngine;

namespace EcosDeLaCripta.Enemy
{
    /// <summary>
    /// Algoritmos de movimiento (steering behaviors) del Laboratorio 2:
    /// Seek / Arrive y Wander, adaptados de Python+Pygame a C#+Unity (plano XZ).
    /// steering = velocidad_deseada - velocidad_actual, limitado por MaxForce.
    /// </summary>
    public static class SteeringUtility
    {
        /// <summary>Seek puro: va directo al target a velocidad máxima.</summary>
        public static Vector3 Seek(Vector3 position, Vector3 velocity, Vector3 target, float maxSpeed, float maxForce)
        {
            Vector3 desired = (target - position);
            if (desired.sqrMagnitude < 0.0001f) return Vector3.zero;
            desired = desired.normalized * maxSpeed;
            Vector3 steer = desired - velocity;
            return Vector3.ClampMagnitude(steer, maxForce);
        }

        /// <summary>
        /// Seek con desaceleración (Arrive): dentro de slowRadius reduce la velocidad
        /// deseada proporcionalmente a la distancia, para no frenar en seco.
        /// </summary>
        public static Vector3 SeekArrive(Vector3 position, Vector3 velocity, Vector3 target, float maxSpeed, float maxForce, float slowRadius, out Vector3 desiredOut)
        {
            Vector3 offset = target - position;
            float dist = offset.magnitude;
            if (dist < 0.001f)
            {
                desiredOut = Vector3.zero;
                return -velocity;
            }

            float speed = maxSpeed;
            if (dist < slowRadius)
            {
                speed = maxSpeed * (dist / slowRadius);
            }

            Vector3 desired = offset.normalized * speed;
            desiredOut = desired;
            Vector3 steer = desired - velocity;
            return Vector3.ClampMagnitude(steer, maxForce);
        }

        /// <summary>
        /// Wander mejorado: un círculo se proyecta delante del agente y el objetivo
        /// se desplaza sobre ese círculo con un pequeño jitter angular acumulativo,
        /// para un deambular suave y coherente (no aleatorio frame a frame).
        /// </summary>
        public static Vector3 WanderImproved(
            Vector3 position, Vector3 velocity, ref float wanderAngleDeg,
            float wanderDistance, float wanderRadius, float jitterDegPerSec, float dt,
            float maxSpeed, float maxForce, out Vector3 desiredOut, out Vector3 wanderTargetOut)
        {
            wanderAngleDeg += Random.Range(-jitterDegPerSec, jitterDegPerSec) * dt;

            Vector3 forward = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.forward;
            Vector3 circleCenter = position + forward * wanderDistance;

            Vector3 displacement = Quaternion.Euler(0f, wanderAngleDeg, 0f) * (Vector3.forward * wanderRadius);
            Vector3 wanderTarget = circleCenter + displacement;
            wanderTargetOut = wanderTarget;

            Vector3 desired = wanderTarget - position;
            if (desired.sqrMagnitude > 0.0001f) desired = desired.normalized * maxSpeed;
            desiredOut = desired;

            Vector3 steer = desired - velocity;
            return Vector3.ClampMagnitude(steer, maxForce);
        }
    }
}
