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
    private bool _jumpPressed; // New: captures jump input from Update()

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Find the Animator in the active skin
        RefreshAnimatorReference();
    }

    /// <summary>
    /// Finds the Animator component in the currently active skin.
    /// Call this method after changing skins.
    /// </summary>
    public void RefreshAnimatorReference()
    {
        // If GraphicsRoot is assigned, search only within it
        if (GraphicsRoot != null)
        {
            _animator = GraphicsRoot.GetComponentInChildren<Animator>(false); // false = only active objects
        }
        else
        {
            // Fallback to searching all children
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
        // Only capture input for local player
        if (HasStateAuthority == false)
            return;

        // Capture jump input every frame so it's never missed
        if (Input.GetButtonDown("Jump"))
        {
            _jumpPressed = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only move own player and not every other player
        if (HasStateAuthority == false)
        {
            return;
        }

        // Check if grounded
        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Small downward force to keep grounded
        }

        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calculate movement relative to camera
        var cameraRotationY = Quaternion.Euler(0, Camera.transform.rotation.eulerAngles.y, 0);
        Vector3 move = cameraRotationY * new Vector3(horizontal, 0, vertical) * Runner.DeltaTime * PlayerSpeed;

        // Move the character
        _controller.Move(move);

        // Rotate character to face movement direction
        if (move != Vector3.zero)
        {
            gameObject.transform.forward = move.normalized;
        }

        // Jump input - now captured from Update() so it's never missed
        if (_jumpPressed && _isGrounded)
        {
            _velocity.y = JumpForce;
            if (_animator != null)
            {
                _animator.SetTrigger("Jump");
            }
        }

        // Apply gravity
        _velocity.y += Gravity * Runner.DeltaTime;
        _controller.Move(_velocity * Runner.DeltaTime);

        // Update animator parameters
        if (_animator != null)
        {
            // Send movement values relative to player's local space
            Vector3 localMove = transform.InverseTransformDirection(move.normalized);

            _animator.SetFloat("MoveX", localMove.x * (move.magnitude > 0 ? 1 : 0));
            _animator.SetFloat("MoveZ", localMove.z * (move.magnitude > 0 ? 1 : 0));
            _animator.SetBool("IsGrounded", _isGrounded);
        }

        // Reset jump flag after consuming it
        _jumpPressed = false;
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Camera = Camera.main;

            // Use CameraPivot if assigned, otherwise fall back to transform
            Transform targetTransform = CameraPivot != null ? CameraPivot : transform;
            Camera.GetComponent<FirstPersonCamera>().Target = targetTransform;

            if (CameraPivot == null)
            {
                Debug.LogWarning("CameraPivot not assigned! Camera will follow root transform at ground level.");
            }
        }
    }
}