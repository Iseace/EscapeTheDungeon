using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class KartController : KartComponent
{
	[Header("Movement")]
	public float acceleration = 20f;
	public float deceleration = 8f;
	public float maxSpeedNormal = 15f;
	public float reverseSpeed = 7f;
	public float turnSpeed = 120f;
	public float minTurnSpeedThreshold = 0.1f;
	[Range(0f, 1f)] public float lowSpeedTurnMultiplier = 0.7f;
	[Range(0f, 1f)] public float highSpeedTurnMultiplier = 1f;
	[SerializeField] private bool invertSteerWhenReversing = false;

	[Header("Stability")]
	[SerializeField] private bool freezePitchAndRoll = true;

	[Header("FX Compatibility")]
	public DriftTier[] driftTiers = new DriftTier[] { new DriftTier { color = Color.white, boostDuration = 0f, startTime = 0f } };

	public Rigidbody Rigidbody;

	public bool IsNetworkReady => _isSpawned && Object != null && Object.IsValid;
	public bool CanDrive => IsNetworkReady && !IsSpinout;
	public bool IsDrifting => false;

	[Networked] public float AppliedSpeed { get; set; }
	[Networked] public NetworkBool IsSpinout { get; set; }
	[Networked] public NetworkBool RoomUserHasFinished { get; set; }

	public event Action<int> OnDriftTierIndexChanged;
	public event Action<int> OnBoostTierIndexChanged;
	public event Action<bool> OnSpinoutChanged;
	public event Action<bool> OnBumpedChanged;
	public event Action<bool> OnHopChanged;
	public event Action<bool> OnBackfiredChanged;

	private bool _isSpawned;
	private bool _lastIsSpinout;

	private void Awake()
	{
		if (Rigidbody == null)
			Rigidbody = GetComponent<Rigidbody>();

		if (Rigidbody != null)
		{
			Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			if (freezePitchAndRoll)
			{
				Rigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
				Rigidbody.constraints &= ~RigidbodyConstraints.FreezeRotationY;
			}
		}
	}

	public override void Spawned()
	{
		base.Spawned();
		_isSpawned = true;
		_lastIsSpinout = IsSpinout;
	}

	public override void Despawned(NetworkRunner runner, bool hasState)
	{
		base.Despawned(runner, hasState);
		_isSpawned = false;
	}

	public override void FixedUpdateNetwork()
	{
		if (!GetInput(out KartInput.NetworkInputData input))
			return;

		if (!CanDrive)
		{
			AppliedSpeed = Mathf.Lerp(AppliedSpeed, 0f, deceleration * Runner.DeltaTime);
			ApplyVelocity();
			return;
		}

		Move(input);
		Turn(input);
		ApplyVelocity();
	}

	public override void Render()
	{
		if (_lastIsSpinout != IsSpinout)
		{
			_lastIsSpinout = IsSpinout;
			OnSpinoutChanged?.Invoke(IsSpinout);
		}
	}

	private void Move(KartInput.NetworkInputData input)
	{
		float targetSpeed = 0f;
		if (input.IsAccelerate)
			targetSpeed = maxSpeedNormal;
		else if (input.IsReverse)
			targetSpeed = -reverseSpeed;

		float lerpFactor = input.IsAccelerate || input.IsReverse ? acceleration : deceleration;
		AppliedSpeed = Mathf.Lerp(AppliedSpeed, targetSpeed, lerpFactor * Runner.DeltaTime);
	}

	private void Turn(KartInput.NetworkInputData input)
	{
		bool hasDriveInput = input.IsAccelerate || input.IsReverse;
		if (Mathf.Abs(AppliedSpeed) < minTurnSpeedThreshold && !hasDriveInput)
			return;

		float speed01 = Mathf.Clamp01(Mathf.Abs(AppliedSpeed) / Mathf.Max(0.01f, maxSpeedNormal));
		float turnMultiplier = Mathf.Lerp(lowSpeedTurnMultiplier, highSpeedTurnMultiplier, speed01);

		float turnDirection = 1f;
		if (invertSteerWhenReversing && AppliedSpeed < -minTurnSpeedThreshold)
		{
			turnDirection = -1f;
		}

		float turn = input.Steer * turnSpeed * turnMultiplier * Runner.DeltaTime * turnDirection;
		Rigidbody.MoveRotation(Rigidbody.rotation * Quaternion.Euler(0f, turn, 0f));
	}

	private void ApplyVelocity()
	{
		Vector3 forward = Rigidbody.rotation * Vector3.forward;

		Vector3 targetVelocity = forward * AppliedSpeed;

		Rigidbody.linearVelocity = Vector3.Lerp(
			Rigidbody.linearVelocity,
			new Vector3(targetVelocity.x, Rigidbody.linearVelocity.y, targetVelocity.z),
			8f * Runner.DeltaTime
		);
	}

	public void RefreshAppliedSpeed()
	{
		if (Rigidbody == null)
			return;

		AppliedSpeed = transform.InverseTransformDirection(Rigidbody.linearVelocity).z;
	}

	public void ResetControllerState()
	{
		AppliedSpeed = 0f;
		IsSpinout = false;
		if (Rigidbody != null)
		{
			Rigidbody.linearVelocity = Vector3.zero;
			Rigidbody.angularVelocity = Vector3.zero;
		}
	}

	public void GiveBoost(bool isBoostpad, int tier = 1)
	{
		AppliedSpeed = Mathf.Max(AppliedSpeed, maxSpeedNormal);
		OnBoostTierIndexChanged?.Invoke(Mathf.Clamp(tier, 0, driftTiers.Length - 1));
	}

	[Serializable]
	public struct DriftTier
	{
		public Color color;
		public float boostDuration;
		public float startTime;
	}
}