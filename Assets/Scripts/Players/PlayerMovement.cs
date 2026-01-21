using UnityEngine;
using Fusion;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 6f;
    public float gravity = -20f;

    [Header("References")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Animator animator;
    private SkinnedMeshRenderer[] bodyMeshes;
    private float moveX;
    private float moveZ;
    private float velocityY;

    public override void Spawned()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        bodyMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();

        // Avoid skipping small downward moves while idle
        controller.minMoveDistance = 0f;

        if (!HasInputAuthority)
        {
            if (cameraTransform != null)
                cameraTransform.gameObject.SetActive(false);
        }
        else
        {
            // Hide local body (FPS)
            foreach (var mesh in bodyMeshes)
            {
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }
    }

    void Update()
    {
        if (!HasInputAuthority) return;

        // INPUT
        moveX = Input.GetAxis("Horizontal");
        moveZ = Input.GetAxis("Vertical");

        // CAMERA-RELATIVE MOVE
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * moveZ + right * moveX) * speed;

        // Apply gravity first; stabilize when grounded
        if (controller.isGrounded && velocityY < 0)
            velocityY = -2f;

        velocityY += gravity * Time.deltaTime;

        // Move and use current frame collision flags for grounding
        Vector3 velocity = move + Vector3.up * velocityY;
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);
        bool grounded = (flags & CollisionFlags.Below) != 0;

        // Jump after Move so grounded reflects this frame
        if (grounded && Input.GetButtonDown("Jump"))
        {
            velocityY = jumpForce;
            if (animator != null)
                animator.SetTrigger("Jump");
        }

        // ANIMATIONS
        if (animator != null)
        {
            animator.SetFloat("MoveX", moveX);
            animator.SetFloat("MoveZ", moveZ);
            animator.SetBool("IsGrounded", grounded);
        }
    }
}