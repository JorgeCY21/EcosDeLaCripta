using EcosDeLaCripta.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EcosDeLaCripta.Player
{
    /// <summary>
    /// Alterna entre una vista cenital de orientación completamente fija y una primera persona
    /// con mirada libre. La tecla T cambia de perspectiva sin modificar el estado del jugador.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Vista cenital estable")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 topDownOffset = new Vector3(0f, 10f, -7.2f);
        [SerializeField] private Vector3 topDownEuler = new Vector3(54.25f, 0f, 0f);
        [SerializeField] private float topDownSize = 4.75f;

        [Header("Primera persona")]
        [SerializeField] private float eyeHeight = 0.82f;
        [SerializeField] private float fieldOfView = 72f;
        [SerializeField] private float mouseSensitivity = 0.105f;
        [SerializeField] private float minPitch = -72f;
        [SerializeField] private float maxPitch = 78f;

        private Camera viewCamera;
        private GameObject playerModel;
        private Renderer[] playerRenderers;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private Text perspectiveToast;
        private CanvasGroup perspectiveToastGroup;
        private float toastTimer;
        private float yaw;
        private float pitch;

        public bool IsFirstPerson { get; private set; }

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            CachePlayerBody();
            if (crosshair != null) crosshair.SetActive(false);
            if (perspectiveToast != null)
            {
                perspectiveToastGroup = perspectiveToast.GetComponent<CanvasGroup>();
                perspectiveToast.gameObject.SetActive(false);
            }
            ApplyTopDownView();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            CachePlayerBody();
            ApplyTopDownView();
        }

        public void SetOffset(Vector3 newOffset)
        {
            topDownOffset = newOffset;
            ApplyTopDownView();
        }

        public void ConfigureFirstPersonUI(GameObject aimCrosshair, Text toast)
        {
            crosshair = aimCrosshair;
            perspectiveToast = toast;
            perspectiveToastGroup = toast != null ? toast.GetComponent<CanvasGroup>() : null;
            if (crosshair != null) crosshair.SetActive(IsFirstPerson);
            if (perspectiveToast != null) perspectiveToast.gameObject.SetActive(false);
        }

        /// <summary>Permite validar y activar explícitamente la perspectiva; la tecla T usa la misma ruta.</summary>
        public void SetFirstPersonMode(bool enabled) => SetFirstPerson(enabled);

        // Los impactos conservan esta llamada, pero una cámara estable no recibe sacudidas.
        public void Shake(float strength = 0.18f, float duration = 0.1f) { }

        private void Update()
        {
            if (IsFirstPerson && GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                SetFirstPerson(false);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame &&
                (GameManager.Instance == null || GameManager.Instance.State == GameState.Playing))
            {
                SetFirstPerson(!IsFirstPerson);
            }

            if (IsFirstPerson)
            {
                UpdateFirstPersonLook();
                if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            UpdateToast();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (IsFirstPerson)
            {
                transform.position = target.position + Vector3.up * eyeHeight;
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else
            {
                // Sin giro, anticipación, zoom, sacudida ni aceleración elástica.
                transform.position = target.position + topDownOffset;
                transform.rotation = Quaternion.Euler(topDownEuler);
            }
        }

        private void UpdateFirstPersonLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);
        }

        private void SetFirstPerson(bool enabled)
        {
            IsFirstPerson = enabled;
            if (enabled)
            {
                yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
                pitch = 0f;
                viewCamera.orthographic = false;
                viewCamera.fieldOfView = fieldOfView;
                viewCamera.nearClipPlane = 0.05f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                viewCamera.orthographic = true;
                viewCamera.orthographicSize = topDownSize;
                viewCamera.nearClipPlane = 0.3f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ApplyTopDownView();
            }

            SetPlayerBodyVisible(!enabled);
            if (crosshair != null) crosshair.SetActive(enabled);
            ShowPerspectiveToast(enabled ? "PRIMERA PERSONA  •  T PARA VOLVER" : "VISTA CENITAL  •  T PARA CAMBIAR");
        }

        private void ApplyTopDownView()
        {
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (IsFirstPerson || viewCamera == null) return;
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = topDownSize;
            if (target != null) transform.position = target.position + topDownOffset;
            transform.rotation = Quaternion.Euler(topDownEuler);
        }

        private void ShowPerspectiveToast(string message)
        {
            if (perspectiveToast == null) return;
            perspectiveToast.text = message;
            perspectiveToast.gameObject.SetActive(true);
            if (perspectiveToastGroup != null) perspectiveToastGroup.alpha = 1f;
            toastTimer = 1.8f;
        }

        private void UpdateToast()
        {
            if (perspectiveToast == null || toastTimer <= 0f) return;
            toastTimer -= Time.unscaledDeltaTime;
            if (perspectiveToastGroup != null)
            {
                perspectiveToastGroup.alpha = toastTimer < 0.4f ? Mathf.Clamp01(toastTimer / 0.4f) : 1f;
            }
            if (toastTimer <= 0f) perspectiveToast.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetPlayerBodyVisible(true);
        }

        private void CachePlayerBody()
        {
            playerModel = target != null ? target.Find("Model")?.gameObject : null;
            playerRenderers = playerModel != null ? playerModel.GetComponentsInChildren<Renderer>(true) : null;
        }

        private void SetPlayerBodyVisible(bool visible)
        {
            if (playerRenderers == null) CachePlayerBody();
            if (playerRenderers == null) return;
            foreach (Renderer bodyRenderer in playerRenderers)
            {
                if (bodyRenderer != null) bodyRenderer.enabled = visible;
            }
        }
    }
}
