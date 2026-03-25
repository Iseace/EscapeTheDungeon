using Fusion;
using UnityEngine;
// Note: We removed the Unity.InputSystem using here, as Fusion should handle passing the input.

// 1. Define a Network Input struct for Fusion
public struct BroomInput : INetworkInput
{
    public Vector2 move;
}

[RequireComponent(typeof(Rigidbody))]
public class BroomMove : NetworkBehaviour
{
    [Header("Speed")]
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float reverseAcceleration = 12f;
    [SerializeField] private float maxForwardSpeed = 25f;
    [SerializeField] private float maxReverseSpeed = 10f;
    [SerializeField] private float idleDrag = 2.5f;
    [SerializeField] private float movingDrag = 0.2f;

    [Header("Steering")]
    [SerializeField] private float steerStrength = 120f;
    [SerializeField] private float highSpeedSteerFactor = 0.35f;

    private Rigidbody rb;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void FixedUpdateNetwork()
    {
        // 2. Remove the HasStateAuthority check! Both Client and Server need to run this.

        // 3. Use GetInput to securely read the networked input
        if (GetInput(out BroomInput input))
        {
            float steerInput = Mathf.Clamp(input.move.x, -1f, 1f);
            float throttleInput = Mathf.Clamp(input.move.y, -1f, 1f);

            ApplyMovement(throttleInput);
            ApplySteering(steerInput);
            ApplyDrag(throttleInput);
        }
    }

    private void ApplyMovement(float throttleInput)
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (throttleInput > 0f && forwardSpeed < maxForwardSpeed)
            rb.AddForce(transform.forward * (throttleInput * acceleration), ForceMode.Acceleration);
        else if (throttleInput < 0f && forwardSpeed > -maxReverseSpeed)
            rb.AddForce(transform.forward * (throttleInput * reverseAcceleration), ForceMode.Acceleration);
    }

    private void ApplySteering(float steerInput)
    {
        float speedPercent = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.01f, maxForwardSpeed));
        float currentSteer = Mathf.Lerp(steerStrength, steerStrength * highSpeedSteerFactor, speedPercent);

        float turnAmount = steerInput * currentSteer * Runner.DeltaTime;
        Quaternion turn = Quaternion.Euler(0f, turnAmount, 0f);
        rb.MoveRotation(rb.rotation * turn);
    }

    private void ApplyDrag(float throttleInput)
    {
        rb.linearDamping = Mathf.Abs(throttleInput) < 0.01f ? idleDrag : movingDrag;
    }
}