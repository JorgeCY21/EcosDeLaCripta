using EcosDeLaCripta.Core;
using UnityEngine;

namespace EcosDeLaCripta.Enemy
{
    [RequireComponent(typeof(Health))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private float normalWidth = 1.05f;
        [SerializeField] private float bossWidth = 1.55f;
        private static Material sharedMaterial;
        private Health health;
        private EnemyAI enemy;
        private LineRenderer background;
        private LineRenderer fill;
        private float currentRatio = 1f;
        private float targetRatio = 1f;

        private void Awake()
        {
            health = GetComponent<Health>();
            enemy = GetComponent<EnemyAI>();
            if (sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                sharedMaterial = new Material(shader) { name = "EnemyHealthBarMaterial" };
            }
            background = CreateLine("HealthBarBackground", new Color(0.025f, 0.018f, 0.025f, 0.92f), 0.15f, 20);
            fill = CreateLine("HealthBarFill", new Color(0.25f, 0.95f, 0.35f), 0.095f, 21);
        }

        private void OnEnable()
        {
            health.OnChanged += HandleHealthChanged;
            health.OnDied += HandleDeath;
        }

        private void Start() => HandleHealthChanged(health.Current, health.MaxHealth);

        private LineRenderer CreateLine(string lineName, Color color, float width, int order)
        {
            GameObject child = new GameObject(lineName);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = sharedMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 2;
            line.sortingOrder = order;
            return line;
        }

        private void HandleHealthChanged(int current, int max)
        {
            targetRatio = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
            bool visible = current < max || (enemy != null && enemy.isBoss);
            background.enabled = visible;
            fill.enabled = visible;
        }

        private void LateUpdate()
        {
            if (!background.enabled) return;
            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, Time.deltaTime * 2.8f);
            Camera cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right.normalized : Vector3.right;
            float width = enemy != null && enemy.isBoss ? bossWidth : normalWidth;
            Vector3 center = transform.position + Vector3.up * (1.28f * transform.lossyScale.y + 0.18f);
            Vector3 left = center - right * width * 0.5f;
            background.SetPosition(0, left - right * 0.035f);
            background.SetPosition(1, left + right * (width + 0.07f));
            fill.SetPosition(0, left);
            fill.SetPosition(1, left + right * width * currentRatio);
            Color color = Color.Lerp(new Color(0.95f, 0.12f, 0.08f), new Color(0.25f, 0.95f, 0.35f), currentRatio);
            fill.startColor = fill.endColor = color;
        }

        private void HandleDeath()
        {
            background.enabled = false;
            fill.enabled = false;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.OnChanged -= HandleHealthChanged;
            health.OnDied -= HandleDeath;
        }
    }
}
