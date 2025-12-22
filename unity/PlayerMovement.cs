using UnityEngine;

namespace RustlikeClient.Player
{
    /// <summary>
    /// ⭐ MELHORADO: Suporta desabilitar movimento quando UI está aberta
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpForce = 5f;
        public float gravity = -20f;

        [Header("Ground Check")]
        public Transform groundCheck;
        public float groundDistance = 0.4f;
        public LayerMask groundMask;

        [Header("State")]
        [SerializeField] private bool _movementEnabled = true;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;
        private float _currentSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            
            // Cria ground check se não existir
            if (groundCheck == null)
            {
                GameObject gc = new GameObject("GroundCheck");
                gc.transform.SetParent(transform);
                gc.transform.localPosition = new Vector3(0, 0, 0);
                groundCheck = gc.transform;
            }
        }

        private void Update()
        {
            HandleGroundCheck();
            
            // ⭐ NOVO: Só processa movimento se estiver habilitado
            if (_movementEnabled)
            {
                HandleMovement();
            }
            else
            {
                _currentSpeed = 0f;
            }
            
            // Gravidade sempre ativa
            HandleGravity();
        }

        private void HandleGroundCheck()
        {
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Pequena força para manter no chão
            }
        }

        private void HandleMovement()
        {
            // Input de movimento
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            // Calcula direção baseada na câmera
            Vector3 move = transform.right * horizontal + transform.forward * vertical;
            move.Normalize();

            // Determina velocidade (andar ou correr)
            _currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

            // Move o personagem
            _controller.Move(move * _currentSpeed * Time.deltaTime);

            // Pulo
            if (Input.GetButtonDown("Jump") && _isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }
        }

        private void HandleGravity()
        {
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        /// <summary>
        /// ⭐ NOVO: Habilita/desabilita movimento
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;
        }

        public bool IsMovementEnabled() => _movementEnabled;
        public Vector3 GetPosition() => transform.position;
        public bool IsGrounded() => _isGrounded;
        public float GetCurrentSpeed() => _currentSpeed;

        /// <summary>
        /// Para debug
        /// </summary>
        private void OnDrawGizmos()
        {
            if (groundCheck != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }
        }
    }
}