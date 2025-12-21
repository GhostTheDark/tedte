using UnityEngine;

namespace RustlikeClient.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        private PlayerMovement _movement;
        private CameraController _camera;

        [Header("Network Sync")]
        public float networkSyncRate = 0.1f; // Sincroniza a cada 100ms
        private float _lastNetworkSync;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _camera = GetComponentInChildren<CameraController>();

            if (_movement == null)
            {
                Debug.LogError("[PlayerController] PlayerMovement não encontrado!");
            }

            if (_camera == null)
            {
                Debug.LogError("[PlayerController] CameraController não encontrado!");
            }
        }

        private void Update()
        {
            SyncWithServer();
        }

        private void SyncWithServer()
        {
            if (Time.time - _lastNetworkSync < networkSyncRate) return;
            if (Network.NetworkManager.Instance == null) return;
            if (!Network.NetworkManager.Instance.IsConnected()) return;

            _lastNetworkSync = Time.time;

            // Envia posição e rotação para o servidor
            Vector3 position = _movement.GetPosition();
            Vector2 rotation = _camera.GetRotation();

            Network.NetworkManager.Instance.SendPlayerMovement(position, rotation);
        }

        // Métodos públicos para debug/info
        public Vector3 GetPosition() => _movement.GetPosition();
        public bool IsGrounded() => _movement.IsGrounded();
        public float GetSpeed() => _movement.GetCurrentSpeed();
    }
}