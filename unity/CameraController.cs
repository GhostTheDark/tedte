using UnityEngine;

namespace RustlikeClient.Player
{
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

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            
            // Trava e esconde o cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            HandleMouseLook();
            HandleFOV();
            HandleCursorToggle();
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

            // Altera FOV ao correr
            float targetFOV = normalFOV;
            
            if (Input.GetKey(KeyCode.LeftShift))
            {
                targetFOV = runFOV;
            }

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
        }

        private void HandleCursorToggle()
        {
            // ESC para liberar/travar cursor
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public Vector2 GetRotation()
        {
            return new Vector2(_yaw, _pitch);
        }

        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }
    }
}