using EcosDeLaCripta.Core;
using UnityEngine;

namespace EcosDeLaCripta.Environment
{
    public class KillGate : MonoBehaviour
    {
        [SerializeField] private int requiredKills = 2;
        [SerializeField] private float riseHeight = 2.8f;
        [SerializeField] private float riseSpeed = 3.5f;
        private Vector3 closedPosition;
        private bool opened;
        private Collider blocker;
        private TextMesh label;

        public void Configure(int kills) => requiredKills = kills;

        private void Awake()
        {
            closedPosition = transform.position;
            blocker = GetComponent<Collider>();
            GameObject labelGo = new GameObject("SealLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            label = labelGo.AddComponent<TextMesh>();
            label.fontSize = 52;
            label.characterSize = 0.045f;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(1f, 0.5f, 0.12f);
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            int current = Mathf.Min(gm.EnemiesDefeated, requiredKills);
            if (label != null) label.text = opened ? "SELLO ROTO" : $"SELLO {current}/{requiredKills}";
            if (!opened && current >= requiredKills)
            {
                opened = true;
                if (blocker != null) blocker.enabled = false;
            }
            if (opened)
            {
                transform.position = Vector3.MoveTowards(transform.position, closedPosition + Vector3.up * riseHeight, riseSpeed * Time.deltaTime);
                if (label != null) label.color = Color.Lerp(label.color, new Color(0.3f, 1f, 0.65f, 0f), Time.deltaTime * 2f);
            }
        }
    }
}
