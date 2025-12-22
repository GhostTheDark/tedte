using UnityEngine;

namespace RustlikeClient.Network
{
    /// <summary>
    /// Sincronização suave de movimento de outros jogadores (interpolação + extrapolação)
    /// </summary>
    public class NetworkPlayerSync : MonoBehaviour
    {
        [Header("Interpolation Settings")]
        [Tooltip("Velocidade de interpolação de posição")]
        public float positionLerpSpeed = 15f;
        
        [Tooltip("Velocidade de interpolação de rotação")]
        public float rotationLerpSpeed = 20f;
        
        [Tooltip("Distância mínima para teleportar ao invés de interpolar")]
        public float teleportDistance = 10f;

        [Header("Extrapolation (Dead Reckoning)")]
        [Tooltip("Ativa extrapolação para prever movimento")]
        public bool useExtrapolation = true;
        
        [Tooltip("Tempo máximo de extrapolação sem receber pacotes")]
        public float maxExtrapolationTime = 0.5f;

        // Targets (recebidos da rede)
        private Vector3 _targetPosition;
        private float _targetYaw;

        // Extrapolação
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private float _lastUpdateTime;

        // Estado
        private bool _hasReceivedFirstUpdate = false;

        private void Awake()
        {
            _targetPosition = transform.position;
            _lastPosition = transform.position;
            _targetYaw = transform.eulerAngles.y;
            _lastUpdateTime = Time.time;
        }

        private void Update()
        {
            if (!_hasReceivedFirstUpdate) return;

            float timeSinceLastUpdate = Time.time - _lastUpdateTime;

            // Se passou muito tempo sem update, usa extrapolação
            if (useExtrapolation && timeSinceLastUpdate < maxExtrapolationTime)
            {
                // Extrapola baseado na velocidade
                Vector3 extrapolatedPos = _targetPosition + (_velocity * timeSinceLastUpdate);
                SmoothMoveTo(extrapolatedPos);
            }
            else
            {
                // Interpolação normal
                SmoothMoveTo(_targetPosition);
            }

            // Rotação sempre interpola suavemente
            SmoothRotateTo(_targetYaw);
        }

        /// <summary>
        /// Atualiza posição e rotação alvo (chamado quando recebe pacote da rede)
        /// </summary>
        public void UpdateTargetTransform(Vector3 position, float yaw)
        {
            // Calcula velocidade para extrapolação
            if (_hasReceivedFirstUpdate)
            {
                float deltaTime = Time.time - _lastUpdateTime;
                if (deltaTime > 0.001f) // Evita divisão por zero
                {
                    _velocity = (position - _targetPosition) / deltaTime;
                }
            }

            // Atualiza targets
            _lastPosition = _targetPosition;
            _targetPosition = position;
            _targetYaw = yaw;
            _lastUpdateTime = Time.time;
            _hasReceivedFirstUpdate = true;

            // Se a distância for muito grande, teleporta ao invés de interpolar
            float distance = Vector3.Distance(transform.position, _targetPosition);
            if (distance > teleportDistance)
            {
                transform.position = _targetPosition;
                _velocity = Vector3.zero;
            }
        }

        private void SmoothMoveTo(Vector3 target)
        {
            // Interpolação suave usando Lerp
            transform.position = Vector3.Lerp(
                transform.position,
                target,
                Time.deltaTime * positionLerpSpeed
            );
        }

        private void SmoothRotateTo(float targetYaw)
        {
            // Interpolação suave de rotação
            float currentYaw = transform.eulerAngles.y;
            float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotationLerpSpeed);
            transform.rotation = Quaternion.Euler(0, newYaw, 0);
        }

        /// <summary>
        /// Para debug
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_hasReceivedFirstUpdate) return;

            // Desenha posição alvo
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_targetPosition, 0.2f);

            // Desenha linha de velocidade
            if (useExtrapolation)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + _velocity);
            }
        }
    }
}