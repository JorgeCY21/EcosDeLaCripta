using EcosDeLaCripta.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EcosDeLaCripta.UI
{
    /// <summary>Mira reactiva: confirma blancos hostiles y da una respuesta visual breve al disparar.</summary>
    public class FirstPersonReticle : MonoBehaviour
    {
        [SerializeField] private Color idleColor = new Color(0.98f, 0.63f, 0.22f, 0.98f);
        [SerializeField] private Color targetColor = new Color(1f, 0.2f, 0.14f, 1f);
        [SerializeField] private float detectionDistance = 80f;

        private Camera viewCamera;
        private Image[] pieces;
        private float recoilPulse;

        private void Awake()
        {
            viewCamera = Camera.main;
            pieces = GetComponentsInChildren<Image>(true);
        }

        private void Update()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            bool hostile = false;
            if (viewCamera != null)
            {
                Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, detectionDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health health = hit.collider.GetComponentInParent<Health>();
                    hostile = health != null && !hit.collider.CompareTag("Player");
                }
            }

            Color activeColor = hostile ? targetColor : idleColor;
            foreach (Image piece in pieces)
            {
                if (piece == null || piece.name.Contains("Shadow")) continue;
                piece.color = piece.name == "Center" ? Color.Lerp(Color.white, activeColor, hostile ? 0.8f : 0.2f) : activeColor;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) recoilPulse = 1f;
            recoilPulse = Mathf.MoveTowards(recoilPulse, 0f, Time.unscaledDeltaTime * 7f);
            transform.localScale = Vector3.one * (1f + recoilPulse * 0.16f);
        }
    }
}
