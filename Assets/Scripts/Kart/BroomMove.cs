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
    [SerializeField] private float steerStrength = 90f;
    [SerializeField] private float highSpeedSteerFactor = 0.35f;
    [SerializeField] private float lateralGrip = 6f;

    [Header("Grip")]
    [SerializeField] private float downforce = 4f;

    private Rigidbody rb;
    private EventManager eventManager;
    private float weatherGripMultiplier = 1f;
    private float weatherDragMultiplier = 1f;
    private float weatherSteerMultiplier = 1f;

    private float trackSpeedMultiplier = 1f;
    private bool trackInvertSteering;
    private TickTimer trackModifierTimer;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;

        if (HasStateAuthority)
            Runner.SetIsSimulated(Object, true);
        else
            Runner.SetIsSimulated(Object, false);

        var simPhysics = Runner.GetComponent<RunnerSimulatePhysics3D>();
        if (simPhysics != null)
        {
            Runner.SetIsSimulated(Object, HasStateAuthority);
        }

        // Find the EventManager to get weather effects
        if (eventManager == null)
        {
            eventManager = FindAnyObjectByType<EventManager>();
        }

        if (eventManager != null)
        {
            eventManager.RegisterKart(this);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.IsValid) return;
        if (!HasStateAuthority) return;

        if (trackModifierTimer.IsRunning && trackModifierTimer.Expired(Runner))
        {
            ClearTrackModifierInternal();
        }

        // Block movement until countdown finishes but allow gravity so
        // brooms fall to the ground naturally during the ready phase.
        if (!RaceCountdown.IsRaceStarted)
        {
            rb.linearVelocity  = new Vector3(0f, rb.linearVelocity.y, 0f); // keep Y (gravity), zero X/Z
            rb.angularVelocity = Vector3.zero;
            rb.linearDamping   = 0f; // no drag so gravity works freely
            return;
        }

        if (GetInput(out PlayerInputData input))
        {
            float steerInput = Mathf.Clamp(input.MoveDirection.x, -1f, 1f);
            float throttleInput = Mathf.Clamp(input.MoveDirection.z, -1f, 1f);

            ApplyMovement(throttleInput);
            ApplyLateralGrip();
            ApplySteering(steerInput);
            ApplyDrag(throttleInput);
        }
        else
        {
            rb.linearDamping = idleDrag;
        }
    }

    private void ApplyMovement(float throttleInput)
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float currentMaxForwardSpeed = maxForwardSpeed * trackSpeedMultiplier;
        float currentMaxReverseSpeed = maxReverseSpeed * trackSpeedMultiplier;
        float currentAcceleration = acceleration * trackSpeedMultiplier;
        float currentReverseAcceleration = reverseAcceleration * trackSpeedMultiplier;

        if (throttleInput > 0f && forwardSpeed < currentMaxForwardSpeed)
            rb.AddForce(transform.forward * (throttleInput * currentAcceleration), ForceMode.Acceleration);
        else if (throttleInput < 0f && forwardSpeed > -currentMaxReverseSpeed)
            rb.AddForce(transform.forward * (throttleInput * currentReverseAcceleration), ForceMode.Acceleration);

        rb.AddForce(Vector3.down * downforce, ForceMode.Acceleration);
    }

    private void ApplySteering(float steerInput)
    {
        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.2f)
        {
            rb.angularVelocity = new Vector3(rb.angularVelocity.x, 0f, rb.angularVelocity.z);
            return;
        }

        if (trackInvertSteering)
        {
            steerInput = -steerInput;
        }

        float speedPercent = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxForwardSpeed));
        float currentSteer = Mathf.Lerp(steerStrength, steerStrength * highSpeedSteerFactor, speedPercent) * weatherSteerMultiplier;
        float targetYawRate = steerInput * currentSteer * Mathf.Deg2Rad;

        rb.angularVelocity = new Vector3(rb.angularVelocity.x, targetYawRate, rb.angularVelocity.z);
    }

    private void ApplyLateralGrip()
    {
        Vector3 lateralVelocity = Vector3.Project(rb.linearVelocity, transform.right);
        rb.AddForce(-lateralVelocity * (lateralGrip * weatherGripMultiplier), ForceMode.Acceleration);
    }

    private void ApplyDrag(float throttleInput)
    {
        float baseDamping = Mathf.Abs(throttleInput) < 0.01f ? idleDrag : movingDrag;
        rb.linearDamping = baseDamping * weatherDragMultiplier;
    }

    /// <summary>
    /// Called by EventManager to update weather-based effects
    /// </summary>
    public void SetWeather(WeatherType weather, EventManager manager)
    {
        eventManager = manager;
        weatherGripMultiplier = manager.GetGripMultiplier();
        weatherDragMultiplier = manager.GetDragMultiplier();
        weatherSteerMultiplier = manager.GetSteerMultiplier();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestTrackModifier(float speedMultiplier, bool invertSteering, float durationSeconds)
    {
        ApplyTrackModifierInternal(speedMultiplier, invertSteering, durationSeconds);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestClearTrackModifier()
    {
        ClearTrackModifierInternal();
    }

    private void ApplyTrackModifierInternal(float speedMultiplier, bool invertSteering, float durationSeconds)
    {
        trackSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        trackInvertSteering = invertSteering;
        float safeDuration = Mathf.Max(0f, durationSeconds);
        trackModifierTimer = safeDuration > 0f ? TickTimer.CreateFromSeconds(Runner, safeDuration) : TickTimer.None;
    }

    private void ClearTrackModifierInternal()
    {
        trackSpeedMultiplier = 1f;
        trackInvertSteering = false;
        trackModifierTimer = TickTimer.None;
    }
}