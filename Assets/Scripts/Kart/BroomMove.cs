using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkRigidbody3D))]
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
        rb.constraints  = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;

        if (GetInput(out PlayerInputData input))
        {
            float steerInput    = Mathf.Clamp(input.MoveDirection.x, -1f, 1f);
            float throttleInput = Mathf.Clamp(input.MoveDirection.z, -1f, 1f);

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
        float turnAmount   = steerInput * currentSteer * Runner.DeltaTime;

        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
    }

    private void ApplyDrag(float throttleInput)
    {
        rb.linearDamping = Mathf.Abs(throttleInput) < 0.01f ? idleDrag : movingDrag;
    }
}