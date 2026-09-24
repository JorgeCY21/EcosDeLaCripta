using UnityEngine;

namespace EcosDeLaCripta.Environment
{
    /// <summary>Parpadeo sutil de intensidad para luces de antorcha, sin costo de animación.</summary>
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 3.2f;
        [SerializeField] private float amplitude = 0.5f;
        [SerializeField] private float speed = 6f;

        private Light lightSource;
        private float seed;

        private void Awake()
        {
            lightSource = GetComponent<Light>();
            seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            lightSource.intensity = baseIntensity + (n - 0.5f) * 2f * amplitude;
        }
    }
}
