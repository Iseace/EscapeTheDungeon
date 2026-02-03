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

    [Networked] private Vector3 _velocity { get; set; }
    [Networked] private NetworkBool _isGrounded { get; set; }

    public Camera Camera;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        RefreshAnimatorReference();
    }

    // Forced cursor unlock for the LobbyRoom UI
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

    public void RefreshAnimatorReference()
    {
        if (GraphicsRoot != null)
        {
            _animator = GraphicsRoot.GetComponent<Animator>();
            if (_animator == null)
                _animator = GraphicsRoot.GetComponentInChildren<Animator>(false);
        }
        else
        {
            _animator = GetComponentInChildren<Animator>(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Freeze movement in LobbyRoom
        if (SceneManager.GetActiveScene().name == "LobbyRoom")
        {
            if (_animator != null)
            {
                _animator.SetFloat("MoveX", 0);
                _animator.SetFloat("MoveZ", 0);
            }
            return; 
        }

        if (GetInput(out PlayerInputData data))
        {
            _isGrounded = _controller.isGrounded;
            Vector3 currentVelocity = _velocity;
            
            if (_isGrounded && currentVelocity.y < 0)
                currentVelocity.y = -2f;

            Vector3 camEuler = data.CameraRotation.eulerAngles;
            transform.rotation = Quaternion.Euler(0, camEuler.y, 0);
            
            Vector3 move = transform.rotation * new Vector3(data.MoveDirection.x, 0, data.MoveDirection.z) * PlayerSpeed;
            _controller.Move(move * Runner.DeltaTime);
            
            if (data.JumpPressed && _isGrounded)
            {
                currentVelocity.y = JumpForce;
                if (HasStateAuthority) RPC_TriggerJump();
            }

            currentVelocity.y += Gravity * Runner.DeltaTime;
            _velocity = currentVelocity;
            _controller.Move(_velocity * Runner.DeltaTime);
            
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerJump()
    {
        if (_animator != null) _animator.SetTrigger("Jump");
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            // Initial cursor setup for LobbyRoom
            if (SceneManager.GetActiveScene().name == "LobbyRoom")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Camera = Camera.main;
            Transform targetTransform = CameraPivot != null ? CameraPivot : transform;

            var fpCam = Camera.GetComponent<FirstPersonCamera>();
            if (fpCam != null)
            {
                fpCam.SetTarget(targetTransform, GraphicsRoot != null ? GraphicsRoot.gameObject : gameObject);

            }
        }
    }
}