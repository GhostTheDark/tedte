using UnityEngine;

namespace RustlikeClient.Player
{
    /// <summary>
    /// ⭐ MELHORADO: Desabilita movimento quando inventário está aberto
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        private PlayerMovement _movement;
        private CameraController _camera;

        [Header("Network Sync")]
        [Tooltip("Taxa de envio de pacotes de movimento (em segundos)")]
        public float networkSyncRate = 0.1f;
        private float _lastNetworkSync;

        [Header("Optimization")]
        [Tooltip("Distância mínima de movimento para enviar pacote")]
        public float minMovementThreshold = 0.01f;
        
        [Tooltip("Ângulo mínimo de rotação para enviar pacote")]
        public float minRotationThreshold = 1f;

        private Vector3 _lastSentPosition;
        private Vector2 _lastSentRotation;

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

            _lastSentPosition = transform.position;
            _lastSentRotation = Vector2.zero;
        }

        private void Update()
        {
            // ⭐ NOVO: Desabilita movimento quando inventário está aberto
            bool inventoryOpen = UI.InventoryManager.Instance != null && 
                                 UI.InventoryManager.Instance.IsInventoryOpen();

            if (_movement != null)
            {
                _movement.SetMovementEnabled(!inventoryOpen);
            }

            // Sempre sincroniza com servidor (mesmo parado)
            SyncWithServer();
        }

        private void SyncWithServer()
        {
            if (Time.time - _lastNetworkSync < networkSyncRate) return;
            if (Network.NetworkManager.Instance == null) return;
            if (!Network.NetworkManager.Instance.IsConnected()) return;

            Vector3 position = _movement.GetPosition();
            Vector2 rotation = _camera.GetRotation();

            // ⭐ OTIMIZAÇÃO: Só envia se houve mudança significativa
            if (HasMovedSignificantly(position, rotation))
            {
                _lastNetworkSync = Time.time;
                _lastSentPosition = position;
                _lastSentRotation = rotation;

                Network.NetworkManager.Instance.SendPlayerMovement(position, rotation);
            }
        }

        private bool HasMovedSignificantly(Vector3 currentPos, Vector2 currentRot)
        {
            // Verifica se moveu mais que o threshold
            float positionDelta = Vector3.Distance(currentPos, _lastSentPosition);
            if (positionDelta > minMovementThreshold)
            {
                return true;
            }

            // Verifica se girou mais que o threshold
            float yawDelta = Mathf.Abs(Mathf.DeltaAngle(currentRot.x, _lastSentRotation.x));
            float pitchDelta = Mathf.Abs(Mathf.DeltaAngle(currentRot.y, _lastSentRotation.y));
            
            if (yawDelta > minRotationThreshold || pitchDelta > minRotationThreshold)
            {
                return true;
            }

            return false;
        }

        // Métodos públicos para debug/info
        public Vector3 GetPosition() => _movement.GetPosition();
        public bool IsGrounded() => _movement.IsGrounded();
        public float GetSpeed() => _movement.GetCurrentSpeed();
        public bool IsMovementEnabled() => _movement.IsMovementEnabled();

        /// <summary>
        /// Para debug - mostra info de rede
        /// </summary>
        private void OnGUI()
        {
            if (Input.GetKey(KeyCode.F3))
            {
                bool inventoryOpen = UI.InventoryManager.Instance != null && 
                                     UI.InventoryManager.Instance.IsInventoryOpen();

                GUI.Box(new Rect(10, 230, 250, 120), "Network Stats (F3)");
                
                float timeSinceLastSync = Time.time - _lastNetworkSync;
                float posDistance = Vector3.Distance(transform.position, _lastSentPosition);
                
                GUI.Label(new Rect(20, 255, 230, 20), $"Last sync: {timeSinceLastSync:F2}s ago");
                GUI.Label(new Rect(20, 275, 230, 20), $"Pos delta: {posDistance:F3}m");
                GUI.Label(new Rect(20, 295, 230, 20), $"Send rate: {1f/networkSyncRate:F0} pkt/s");
                GUI.Label(new Rect(20, 315, 230, 20), $"Movement: {(IsMovementEnabled() ? "ENABLED" : "DISABLED")}");
                GUI.Label(new Rect(20, 335, 230, 20), $"Inventory: {(inventoryOpen ? "OPEN" : "CLOSED")}");
            }
        }
    }
}