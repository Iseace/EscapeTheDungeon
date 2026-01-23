using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private CharacterController _controller;
    private Animator _animator;

    [Header("References")]
    [Tooltip("Assign the Graphics parent object that contains all skin options")]
    public Transform GraphicsRoot;
    [Tooltip("Assign the CameraPivot transform (should be at y=1.8)")]
    public Transform CameraPivot;

    [Header("Movement Settings")]
    public float PlayerSpeed = 2f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;

    public Camera Camera;

    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _jumpPressed;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        RefreshAnimatorReference();
    }

    public void RefreshAnimatorReference()
    {
        if (GraphicsRoot != null)
        {
            _animator = GraphicsRoot.GetComponentInChildren<Animator>(false);
        }
        else
        {
            _animator = GetComponentInChildren<Animator>(false);
        }

        if (_animator == null)
        {
            Debug.LogWarning("Animator not found! Make sure one skin is active in the Graphics folder.");
        }
        else
        {
            Debug.Log($"Animator found on: {_animator.gameObject.name}");
        }
    }

    void Update()
    {
        if (HasStateAuthority == false)
            return;

        if (Input.GetButtonDown("Jump"))
        {
            _jumpPressed = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only move own player
        if (HasStateAuthority == false)
        {
            return;
        }

        if (Camera == null)
        {
            return;
        }

        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        var cameraRotationY = Quaternion.Euler(0, Camera.transform.rotation.eulerAngles.y, 0);
        Vector3 move = cameraRotationY * new Vector3(horizontal, 0, vertical) * Runner.DeltaTime * PlayerSpeed;

        _controller.Move(move);

        if (move != Vector3.zero)
        {
            gameObject.transform.forward = move.normalized;
        }

        if (_jumpPressed && _isGrounded)
        {
            _velocity.y = JumpForce;

            // Trigger jump animation via RPC so all clients see it
            RPC_TriggerJump();
        }

        _velocity.y += Gravity * Runner.DeltaTime;
        _controller.Move(_velocity * Runner.DeltaTime);

        // Update animator - NetworkMecanimAnimator handles syncing these
        if (_animator != null)
        {
            Vector3 localMove = transform.InverseTransformDirection(move.normalized);
            _animator.SetFloat("MoveX", localMove.x * (move.magnitude > 0 ? 1 : 0));
            _animator.SetFloat("MoveZ", localMove.z * (move.magnitude > 0 ? 1 : 0));
            _animator.SetBool("IsGrounded", _isGrounded);
        }

        _jumpPressed = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerJump()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Jump");
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Camera = Camera.main;
            Transform targetTransform = CameraPivot != null ? CameraPivot : transform;
            Camera.GetComponent<FirstPersonCamera>().Target = targetTransform;

            if (CameraPivot == null)
            {
                Debug.LogWarning("CameraPivot not assigned! Camera will follow root transform at ground level.");
            }
        }
    }
}