using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerMovement : NetworkBehaviour
{
    private CharacterController _controller;
    private Animator _animator;

    [Header("References")]
    public Transform GraphicsRoot;
    public Transform CameraPivot;

    [Header("Movement Settings")]
    public float PlayerSpeed = 5f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;
    public float GroundFriction = 6f; // High friction on ground to prevent skating
    public float AirFriction = 0.5f;   // Very low friction in air for long slides

    [Header("Boss Settings (Optional)")]
    public bool isBoss = false; // Check this for boss characters
    private BossHitbox _bossHitbox;
    private BossSpecial _bossSpecial;

    [Networked] private Vector3 _velocity { get; set; }
    [Networked] private NetworkBool _isGrounded { get; set; }

    public Camera Camera;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        RefreshAnimatorReference();

        // Get boss components (auto-detected; works even if isBoss isn't ticked)
        _bossHitbox = GetComponentInChildren<BossHitbox>();
        _bossSpecial = GetComponent<BossSpecial>();
    }

    private void Update()
    {
        if (HasInputAuthority && SceneManager.GetActiveScene().name == "LobbyRoom")
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (SceneManager.GetActiveScene().name == "LobbyRoom")
        {
            StopAnimations();
            return;
        }

        var playerSetup = GetComponent<PlayerSetup>();
        if (playerSetup != null && playerSetup.HasEscapedSafe)
        {
            _velocity = Vector3.zero;
            StopAnimations();
            return;
        }

        var health = GetComponent<PlayerHealth>();
        if (health != null && health.IsDeadSafe)
        {
            _velocity = Vector3.zero;
            StopAnimations();
            return;
        }

        var role = GetComponent<PlayerRole>();
        if (role != null && role.IsBossSafe && DungeonNetworkRunner.Instance != null && DungeonNetworkRunner.Instance.IsBossFrozen)
        {
            _isGrounded = _controller.isGrounded;

            Vector3 frozenVelocity = _velocity;
            if (_isGrounded && frozenVelocity.y < 0)
                frozenVelocity.y = -2f;

            frozenVelocity.y += Gravity * Runner.DeltaTime;
            _velocity = frozenVelocity;

            _controller.Move(_velocity * Runner.DeltaTime);
            StopAnimations();
            return;
        }

        if (GetInput(out PlayerInputData data))
        {
            _isGrounded = _controller.isGrounded;

            // 1. Rotación del Jugador (Sincronizada con la cámara)
            Vector3 camEuler = data.CameraRotation.eulerAngles;
            transform.rotation = Quaternion.Euler(0, camEuler.y, 0);

            // 2. Cálculo de Movimiento Horizontal con modificador de velocidad
            float currentSpeed = PlayerSpeed;

            // Apply speed modifiers (auto-detected, only active on boss prefab)
            if (_bossHitbox != null)
                currentSpeed *= _bossHitbox.GetMoveSpeedMultiplier();
            if (_bossSpecial != null)
                currentSpeed *= _bossSpecial.GetSpeedMultiplier();

            Vector3 move = transform.rotation * data.MoveDirection * currentSpeed;

            // 3. Cálculo de Salto y Gravedad
            Vector3 currentVelocity = _velocity;

            if (_isGrounded && currentVelocity.y < 0)
                currentVelocity.y = -2f;

            if (data.JumpPressed && _isGrounded && !isBoss)
            {
                currentVelocity.y = JumpForce;
                if (HasStateAuthority) RPC_TriggerJump();
            }

            // 3.1. Apply Gravity
            currentVelocity.y += Gravity * Runner.DeltaTime;

            // 3.2. Apply Horizontal Friction/Drag to the horizontal velocity
            // This ensures knockback doesn't last forever.
            // USES AIR FRICTION WHILE IN AIR FOR A TRUE LONG PUSH
            float currentFriction = _isGrounded ? GroundFriction : AirFriction;

            Vector2 horizontalVelocity = new Vector2(currentVelocity.x, currentVelocity.z);
            if (horizontalVelocity.magnitude > 0.01f)
            {
                horizontalVelocity = Vector2.Lerp(horizontalVelocity, Vector2.zero, currentFriction * Runner.DeltaTime);
                currentVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.y);
            }
            else
            {
                currentVelocity = new Vector3(0, currentVelocity.y, 0);
            }

            _velocity = currentVelocity;

            // 4. APLICAR MOVIMIENTO (Combinado en un solo vector)
            // Separate the velocity (knockback/gravity) from the input-based horizontal move
            // because _controller.Move takes a DELTA displacement.
            Vector3 moveDisplacement = move * Runner.DeltaTime;
            Vector3 velocityDisplacement = _velocity * Runner.DeltaTime;

            _controller.Move(moveDisplacement + velocityDisplacement);

            UpdateAnimations(move);
        }
    }

    private void UpdateAnimations(Vector3 move)
    {
        // Always ensure we are talking to the CURRENT active animator
        RefreshAnimatorReference();

        if (_animator == null || !_animator.isActiveAndEnabled) return;

        // Use the raw move vector so the Blend Tree gets the actual speed
        Vector3 localMove = transform.InverseTransformDirection(move);

        // Normalize these for the 2D Blend Tree to ensure they stay in -1 to 1 range
        float normX = move.magnitude > 0.01f ? localMove.x / PlayerSpeed : 0;
        float normZ = move.magnitude > 0.01f ? localMove.z / PlayerSpeed : 0;

        _animator.SetFloat("MoveX", normX);
        _animator.SetFloat("MoveZ", normZ);

        // Check if "Speed" parameter exists to avoid warnings/errors
        foreach (var param in _animator.parameters)
        {
            if (param.name == "Speed")
            {
                _animator.SetFloat("Speed", move.magnitude);
                break;
            }
        }

        _animator.SetBool("IsGrounded", _isGrounded);
    }

    private void StopAnimations()
    {
        if (_animator != null)
        {
            _animator.SetFloat("MoveX", 0);
            _animator.SetFloat("MoveZ", 0);

            // Check if "Speed" parameter exists to avoid warnings/errors
            foreach (var param in _animator.parameters)
            {
                if (param.name == "Speed")
                {
                    _animator.SetFloat("Speed", 0);
                    break;
                }
            }
        }
    }

    public void RefreshAnimatorReference()
    {
        if (GraphicsRoot != null)
        {
            _animator = GraphicsRoot.GetComponentInChildren<Animator>(false); // false = only active
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>(false);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerJump()
    {
        if (_animator != null) _animator.SetTrigger("Jump");
    }

    public override void Spawned()
    {
        // Re-resolve boss references after Fusion spawns the object (safety net)
        if (_bossSpecial == null)
            _bossSpecial = GetComponent<BossSpecial>();
        if (_bossHitbox == null)
            _bossHitbox = GetComponentInChildren<BossHitbox>();

        if (HasInputAuthority)
        {
            // 1. Configuramos la cámara
            Camera = Camera.main;
            var fpCam = Camera.GetComponent<FirstPersonCamera>();
            if (fpCam != null)
            {
                fpCam.SetTarget(CameraPivot != null ? CameraPivot : transform, GraphicsRoot.gameObject);
            }

            // 2. HACERTE INVISIBLE PARA TI MISMO
            if (GraphicsRoot != null)
            {
                SetLayerRecursively(GraphicsRoot.gameObject, LayerMask.NameToLayer("LocalPlayerHidden"));
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    /// <summary>
    /// Apply a knockback force to the player, pushing them away from the boss.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force, float upwardBonus = 0f)
    {
        // Ensure direction is normalized
        direction = direction.normalized;

        // Apply knockback - horizontal component carries the main force
        Vector3 knockback = direction * force;

        // Add upward component if specified (for more dramatic effect)
        if (upwardBonus > 0)
        {
            knockback.y = upwardBonus;
        }

        // Add to current velocity (doesn't replace it, adds to existing momentum)
        _velocity += knockback;
    }
}