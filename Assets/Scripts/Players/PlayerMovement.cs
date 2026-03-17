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
        if (playerSetup != null && playerSetup.HasEscaped)
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
        if (role != null && role.IsBoss && DungeonNetworkRunner.Instance != null && DungeonNetworkRunner.Instance.IsBossFrozen)
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

            currentVelocity.y += Gravity * Runner.DeltaTime;
            _velocity = currentVelocity;

            // 4. APLICAR MOVIMIENTO (Combinado en un solo vector)
            Vector3 finalMotion = (move + _velocity) * Runner.DeltaTime;
            _controller.Move(finalMotion);

            UpdateAnimations(move);
        }
    }

    private void UpdateAnimations(Vector3 move)
    {
        if (_animator == null)
        {
            RefreshAnimatorReference();
            if (_animator == null) return;
        }

        Vector3 localMove = transform.InverseTransformDirection(move.normalized);
        _animator.SetFloat("MoveX", localMove.x);
        _animator.SetFloat("MoveZ", localMove.z);
        _animator.SetBool("IsGrounded", _isGrounded);
    }

    private void StopAnimations()
    {
        if (_animator != null)
        {
            _animator.SetFloat("MoveX", 0);
            _animator.SetFloat("MoveZ", 0);
        }
    }

    public void RefreshAnimatorReference()
    {
        _animator = (GraphicsRoot != null)
            ? GraphicsRoot.GetComponentInChildren<Animator>(false)
            : GetComponentInChildren<Animator>(false);
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
}