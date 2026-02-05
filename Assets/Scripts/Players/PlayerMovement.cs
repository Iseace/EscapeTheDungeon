using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private CharacterController _controller;
    private Animator _animator;

    [Header("References")]
    public Transform GraphicsRoot;
    public Transform CameraPivot;

    [Header("Movement Settings")]
    public float PlayerSpeed = 2f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;

    // Networked properties host to clients
    [Networked] private Vector3 _velocity { get; set; }
    [Networked] private NetworkBool _isGrounded { get; set; }

    public Camera Camera;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        RefreshAnimatorReference();
    }

    // Mantenemos tu lógica de desbloqueo de cursor para el Lobby
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

        if (GetInput(out PlayerInputData data))
        {
            _isGrounded = _controller.isGrounded;

            // 1. Rotación del Jugador (Sincronizada con la cámara)
            Vector3 camEuler = data.CameraRotation.eulerAngles;
            transform.rotation = Quaternion.Euler(0, camEuler.y, 0);

            // 2. Cálculo de Movimiento Horizontal
            // data.MoveDirection ya viene como (x, 0, y) desde tu Handler
            Vector3 move = transform.rotation * data.MoveDirection * PlayerSpeed;

            // 3. Cálculo de Salto y Gravedad
            Vector3 currentVelocity = _velocity;

            if (_isGrounded && currentVelocity.y < 0)
            {
                currentVelocity.y = -2f;

            if (data.JumpPressed && _isGrounded)
            {
                currentVelocity.y = JumpForce;
                if (HasStateAuthority) RPC_TriggerJump();
            }
            //Apply Gravity to the local variable
            currentVelocity.y += Gravity * Runner.DeltaTime;
            //RE-ASSIGN the modified velocity back to the Networked property
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

        // Convertimos el movimiento mundial a local para las animaciones (MoveX, MoveZ)
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
        // HasInputAuthority is true for the player who controls this specific prefab
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
                // Cambiamos el modelo y todos sus hijos (incluida la espada) a la capa invisible
                SetLayerRecursively(GraphicsRoot.gameObject, LayerMask.NameToLayer("LocalPlayerHidden"));
            }
        }
    }

    // Función auxiliar para cambiar la capa a todo el modelo
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}