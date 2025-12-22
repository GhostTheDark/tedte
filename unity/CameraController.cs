using UnityEngine;

namespace RustlikeClient.Player
{
    /// <summary>
    /// ⭐ MELHORADO: Respeita estado do inventário, não trava cursor quando inventário está aberto
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        public Transform playerBody;
        public float mouseSensitivity = 100f;
        public float minPitch = -90f;
        public float maxPitch = 90f;

        [Header("FOV")]
        public float normalFOV = 60f;
        public float runFOV = 70f;
        public float fovTransitionSpeed = 5f;

        private Camera _camera;
        private float _pitch = 0f;
        private float _yaw = 0f;
        private bool _cursorWasLocked = true;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            
            // Trava e esconde o cursor no início
            LockCursor();
        }

        private void Start()
        {
            if (playerBody == null)
            {
                playerBody = transform.parent;
            }
        }

        private void Update()
        {
            HandleCursorState();
            
            // Só processa mouse look se cursor estiver travado
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                HandleMouseLook();
            }
            
            HandleFOV();
            HandleCursorToggle();
        }

        /// <summary>
        /// ⭐ NOVO: Verifica estado do inventário e ajusta cursor automaticamente
        /// </summary>
        private void HandleCursorState()
        {
            bool inventoryOpen = UI.InventoryManager.Instance != null && 
                                 UI.InventoryManager.Instance.IsInventoryOpen();

            // Se inventário abriu, libera cursor
            if (inventoryOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                UnlockCursor();
            }
            // Se inventário fechou, trava cursor novamente
            else if (!inventoryOpen && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
        }

        private void HandleMouseLook()
        {
            // Input do mouse
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            // Atualiza Yaw (rotação horizontal) no corpo do player
            _yaw += mouseX;

            // Atualiza Pitch (rotação vertical) na câmera
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            // Aplica rotações
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            
            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
        }

        private void HandleFOV()
        {
            if (_camera == null) return;

            // Altera FOV ao correr (apenas se inventário fechado)
            bool inventoryOpen = UI.InventoryManager.Instance != null && 
                                 UI.InventoryManager.Instance.IsInventoryOpen();
            
            float targetFOV = normalFOV;
            
            if (!inventoryOpen && Input.GetKey(KeyCode.LeftShift))
            {
                targetFOV = runFOV;
            }

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
        }

        private void HandleCursorToggle()
        {
            // ⭐ MELHORADO: ESC não faz mais toggle de cursor (isso é gerenciado pelo inventário)
            // Mantido apenas para debug/emergência com ALT+ESC
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    UnlockCursor();
                    Debug.Log("[CameraController] Cursor liberado manualmente (ALT+ESC)");
                }
                else
                {
                    LockCursor();
                    Debug.Log("[CameraController] Cursor travado manualmente (ALT+ESC)");
                }
            }
        }

        /// <summary>
        /// Trava o cursor
        /// </summary>
        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Libera o cursor
        /// </summary>
        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public Vector2 GetRotation()
        {
            return new Vector2(_yaw, _pitch);
        }

        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }

        /// <summary>
        /// Para debug
        /// </summary>
        private void OnGUI()
        {
            if (Input.GetKey(KeyCode.F4))
            {
                GUI.Box(new Rect(10, 340, 200, 80), "Camera (F4)");
                GUI.Label(new Rect(20, 365, 180, 20), $"Yaw: {_yaw:F1}°");
                GUI.Label(new Rect(20, 385, 180, 20), $"Pitch: {_pitch:F1}°");
                GUI.Label(new Rect(20, 405, 180, 20), $"Cursor: {Cursor.lockState}");
            }
        }
    }
}