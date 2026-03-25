using System;
using Fusion;
using UnityEngine;

public class KartController : KartComponent
{
	public new SphereCollider collider;
	public DriftTier[] driftTiers;
	[SerializeField] private Axis tireYawAxis = Axis.Y;

	public Transform model;
	public Transform tireFL, tireFR, tireYawFL, tireYawFR, tireBL, tireBR;

	public float maxSpeedNormal;
	public float maxSpeedBoosting;
	public float reverseSpeed;
	public float acceleration;
	public float deceleration;

	[Tooltip("X-Axis: steering\nY-Axis: velocity\nCoordinate space is normalized")]
	public AnimationCurve steeringCurve = AnimationCurve.Linear(0, 0, 1, 1);

	public float maxSteerStrength = 35;
	public float steerAcceleration;
	public float steerDeceleration;
	public Vector2 driftInputRemap = new Vector2(0.5f, 1f);
	public float hopSteerStrength;
	public float speedToDrift;
	public float driftRotationLerpFactor = 10f;

	public Rigidbody Rigidbody;
	public bool IsNetworkReady => _isSpawned && Object != null && Object.IsValid;

	public bool IsBumped => IsNetworkReady && !BumpTimer.ExpiredOrNotRunning(Runner);
	public bool IsBackfire => IsNetworkReady && !BackfireTimer.ExpiredOrNotRunning(Runner);
	public bool IsHopping => IsNetworkReady && !HopTimer.ExpiredOrNotRunning(Runner);
	public bool CanDrive => IsNetworkReady && HasStartedRace && !HasFinishedRace && !IsSpinout && !IsBumped && !IsBackfire;
	public bool HasFinishedRace => false;
	public bool HasStartedRace => true;
	public float BoostTime => !IsNetworkReady || BoostEndTick == -1 ? 0f : (BoostEndTick - Runner.Tick) * Runner.DeltaTime;
	private float RealSpeed => transform.InverseTransformDirection(Rigidbody.linearVelocity).z;
	public bool IsDrifting => IsNetworkReady && (IsDriftingLeft || IsDriftingRight);
	public bool IsBoosting => IsNetworkReady && BoostTierIndex != 0;
	public bool IsOffroad => IsNetworkReady && IsGrounded && GroundResistance >= 0.2f;
	public float DriftTime => IsNetworkReady ? (Runner.Tick - DriftStartTick) * Runner.DeltaTime : 0f;

	[Networked] public float MaxSpeed { get; set; }

	[Networked]
	public int BoostTierIndex { get; set; }

	[Networked] public TickTimer BoostpadCooldown { get; set; }

	[Networked]
	public int DriftTierIndex { get; set; } = -1;

	[Networked] public NetworkBool IsGrounded { get; set; }
	[Networked] public float GroundResistance { get; set; }
	[Networked] public int BoostEndTick { get; set; } = -1;

	[Networked]
	public NetworkBool IsSpinout { get; set; }

	[Networked] public float TireYaw { get; set; }
	[Networked] public NetworkBool RoomUserHasFinished { get; set; }
	[Networked] public NetworkBool IsDriftingLeft { get; set; }
	[Networked] public NetworkBool IsDriftingRight { get; set; }
	[Networked] public int DriftStartTick { get; set; }

	[Networked]
	public TickTimer BackfireTimer { get; set; }

	[Networked]
	public TickTimer BumpTimer { get; set; }

	[Networked]
	public TickTimer HopTimer { get; set; }

	[Networked] public float AppliedSpeed { get; set; }

	[Networked] private KartInput.NetworkInputData Inputs { get; set; }

	public event Action<int> OnDriftTierIndexChanged;
	public event Action<int> OnBoostTierIndexChanged;
	public event Action<bool> OnSpinoutChanged;
	public event Action<bool> OnBumpedChanged;
	public event Action<bool> OnHopChanged;
	public event Action<bool> OnBackfiredChanged;

	[Networked] private float SteerAmount { get; set; }
	[Networked] private int AcceleratePressedTick { get; set; }
	[Networked] private bool IsAccelerateThisFrame { get; set; }

	private ChangeDetector _changeDetector;
	private bool _isSpawned;

	private static void OnIsBackfireChangedCallback(KartController changed) =>
		changed.OnBackfiredChanged?.Invoke(changed.IsBackfire);

	private static void OnIsBumpedChangedCallback(KartController changed) =>
		changed.OnBumpedChanged?.Invoke(changed.IsBumped);

	private static void OnIsHopChangedCallback(KartController changed) =>
		changed.OnHopChanged?.Invoke(changed.IsHopping);

	private static void OnSpinoutChangedCallback(KartController changed) =>
		changed.OnSpinoutChanged?.Invoke(changed.IsSpinout);

	private static void OnDriftTierIndexChangedCallback(KartController changed) =>
		changed.OnDriftTierIndexChanged?.Invoke(changed.DriftTierIndex);

	private static void OnBoostTierIndexChangedCallback(KartController changed) =>
		changed.OnBoostTierIndexChanged?.Invoke(changed.BoostTierIndex);

	private void Awake()
	{
		collider = GetComponent<SphereCollider>();
		if (model == null)
		{
			model = transform;
		}
	}

	public override void Spawned()
	{
		base.Spawned();
		_isSpawned = true;
		_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
		MaxSpeed = maxSpeedNormal;
	}

	public override void Despawned(NetworkRunner runner, bool hasState)
	{
		base.Despawned(runner, hasState);
		_isSpawned = false;
	}

	private void Update()
	{
		if (!IsNetworkReady)
			return;

		GroundNormalRotation();
		UpdateTireRotation();

		if (Object.HasInputAuthority && CanDrive)
		{
			if (Kart.Input.gamepad != null)
			{
				Kart.Input.gamepad.SetMotorSpeeds(IsOffroad ? AppliedSpeed / MaxSpeed : 0, 0);
			}
		}
	}

	private void OnCollisionStay(Collision collision)
	{
		//
		// OnCollisionEnter and OnCollisionExit are not reliable when trying to predict collisions, however we can
		// use OnCollisionStay reliably. This means we have to make sure not to run code every frame
		//

		// We don't want to run any of this code if we're already in the process of bumping
		if (IsBumped) return;
		if (collision.gameObject.TryGetComponent(out KartEntity otherKart))
		{
			//
			// Collision with another kart - if we are going slower than them, then we should bump!  
			//

			if (AppliedSpeed < otherKart.Controller.AppliedSpeed)
			{
				BumpTimer = TickTimer.CreateFromSeconds(Runner, 0.4f);
			}
		}
		else
		{
			//
			// Collision with a wall of some sort - We should get the angle impact and apply a force backwards, only if 
			// we are going above 'speedToDrift' speed.
			//
			if (RealSpeed > speedToDrift)
			{
				var contact = collision.GetContact(0);
				var dot = Mathf.Max(0.25f, Mathf.Abs(Vector3.Dot(contact.normal, Rigidbody.transform.forward)));
				Rigidbody.AddForceAtPosition(contact.normal * AppliedSpeed * dot, contact.point, ForceMode.VelocityChange);

				BumpTimer = TickTimer.CreateFromSeconds(Runner, 0.8f * dot);
			}
		}
	}

	public override void FixedUpdateNetwork()
	{
		base.FixedUpdateNetwork();

		if (GetInput(out KartInput.NetworkInputData input))
		{
			//
			// Copy our inputs that we have received, to a [Networked] property, so other clients can predict using our
			// tick-aligned inputs. This is the core of the Client Prediction system.
			//
			Inputs = input;
		}

		if (CanDrive)
			Move(Inputs);
		else
			RefreshAppliedSpeed();

		HandleStartRace();
		SpinOut(Inputs);
		Boost(Inputs);
		Drift(Inputs);
		Steer(Inputs);
		UpdateTireYaw(Inputs);
		UseItems(Inputs);
	}

	public override void Render()
	{
		foreach (var change in _changeDetector.DetectChanges(this))
		{
			switch (change)
			{
				case nameof(BoostTierIndex):
					OnBoostTierIndexChangedCallback(this);
					break;
				case nameof(DriftTierIndex):
					OnDriftTierIndexChangedCallback(this);
					break;
				case nameof(IsSpinout):
					OnSpinoutChangedCallback(this);
					break;
				case nameof(BackfireTimer):
					OnIsBackfireChangedCallback(this);
					break;
				case nameof(BumpTimer):
					OnIsBumpedChangedCallback(this);
					break;
				case nameof(HopTimer):
					OnIsHopChangedCallback(this);
					break;
			}
		}
	}

	private void UseItems(KartInput.NetworkInputData inputs)
	{
		if (inputs.IsDownThisFrame(KartInput.NetworkInputData.UseItem) && Kart != null && Kart.Items != null)
		{
			Kart.Items.UseItem();
		}
	}

	private void HandleStartRace()
	{
		// Movement-only mode: race systems are disabled in this project.
	}

	/// <summary>
	/// Handling spinout at the start of the race. We record the tick that we last pressed the Accelerate button down,
	/// and then calculate how long we have been pressing that button elsewhere.
	/// </summary>
	/// <param name="input"></param>
	private void SpinOut(KartInput.NetworkInputData input)
	{
		var isAccelerate = input.IsDown(KartInput.NetworkInputData.ButtonAccelerate);

		if (isAccelerate && !IsAccelerateThisFrame)
		{
			AcceleratePressedTick = Runner.Tick;
		}

		if (AcceleratePressedTick != -1 && !isAccelerate)
		{
			AcceleratePressedTick = -1;
		}

		IsAccelerateThisFrame = isAccelerate;
	}

	public override void OnRaceStart()
	{
		base.OnRaceStart();

		//
		// If the acceleration button is held down OnRaceStart, then we can apply either a boost (if they were quick
		// enough), or stall them (if they were too slow!)
		//
		if (AcceleratePressedTick != -1)
		{
			var tickDiff = Runner.Tick - AcceleratePressedTick;
			var time = tickDiff * Runner.DeltaTime;

			if (time < 0.15f)
				GiveBoost(false);
			else if (time < 0.3f)
			{
				BackfireTimer = TickTimer.CreateFromSeconds(Runner, 0.8f);
			}
		}
	}

	private void Move(KartInput.NetworkInputData input)
	{
		if (input.IsAccelerate)
		{
			AppliedSpeed = Mathf.Lerp(AppliedSpeed, MaxSpeed, acceleration * Runner.DeltaTime);
		}
		else if (input.IsReverse)
		{
			AppliedSpeed = Mathf.Lerp(AppliedSpeed, -reverseSpeed, acceleration * Runner.DeltaTime);
		}
		else
		{
			AppliedSpeed = Mathf.Lerp(AppliedSpeed, 0, deceleration * Runner.DeltaTime);
		}

		var resistance = 1 - (IsGrounded ? GroundResistance : 0);
		if (resistance < 1)
		{
			AppliedSpeed = Mathf.Lerp(AppliedSpeed, AppliedSpeed * resistance, Runner.DeltaTime * (IsDrifting ? 8 : 2));
		}

		// transform.forward is not reliable when using NetworkedRigidbody - instead use: NetworkRigidbody.Rigidbody.rotation * Vector3.forward
		var vel = (Rigidbody.rotation * Vector3.forward) * AppliedSpeed;
		vel.y = Rigidbody.linearVelocity.y;
		Rigidbody.linearVelocity = vel;
	}


	private void Steer(KartInput.NetworkInputData input)
	{
		var steerTarget = GetSteerTarget(input);

		if (SteerAmount != steerTarget)
		{
			var steerLerp = Mathf.Abs(SteerAmount) < Mathf.Abs(steerTarget) ? steerAcceleration : steerDeceleration;
			SteerAmount = Mathf.Lerp(SteerAmount, steerTarget, Runner.DeltaTime * steerLerp);
		}

		if (IsDrifting)
		{
			model.localEulerAngles = LerpAxis(Axis.Y, model.localEulerAngles, SteerAmount * 2,
				driftRotationLerpFactor * Runner.DeltaTime);
		}
		else
		{
			model.localEulerAngles = LerpAxis(Axis.Y, model.localEulerAngles, 0, 6 * Runner.DeltaTime);
		}

		if (CanDrive)
		{
			var rot = Quaternion.Euler(
				Vector3.Lerp(
					Rigidbody.rotation.eulerAngles,
					Rigidbody.rotation.eulerAngles + Vector3.up * SteerAmount,
					3 * Runner.DeltaTime)
			);

			Rigidbody.MoveRotation(rot);
		}
	}

	private float GetSteerTarget(KartInput.NetworkInputData input)
	{
		var steerFactor = steeringCurve.Evaluate(Mathf.Abs(RealSpeed) / maxSpeedNormal) * maxSteerStrength *
						  Mathf.Sign(RealSpeed);

		if (IsHopping && RealSpeed < speedToDrift)
			return input.Steer * hopSteerStrength;

		if (IsDriftingLeft)
			return Remap(input.Steer, -1, 1, -driftInputRemap.y, -driftInputRemap.x) * maxSteerStrength;
		if (IsDriftingRight)
			return Remap(input.Steer, -1, 1, driftInputRemap.x, driftInputRemap.y) * maxSteerStrength;

		return input.Steer * steerFactor;
	}

	private void Drift(KartInput.NetworkInputData input)
	{
		if (driftTiers == null || driftTiers.Length == 0)
		{
			if (IsDrifting)
			{
				StopDrifting();
			}
			return;
		}

		var startDrift = input.IsDriftPressedThisFrame && CanDrive && !IsDrifting;
		if (startDrift && IsGrounded)
		{
			StartDrifting(input);
			DriftStartTick = Runner.Tick;
			HopTimer = TickTimer.CreateFromSeconds(Runner, 0.367f);
		}

		if (IsDrifting)
		{
			if (!input.IsDriftPressed || RealSpeed < speedToDrift)
			{
				StopDrifting();
			}
			else if (IsGrounded)
			{
				EvaluateDrift(DriftTime, out var index);
				if (DriftTierIndex != index) DriftTierIndex = index;
			}
		}
	}

	/// <summary>
	/// Handles when a boost is applied.
	/// </summary>
	/// <param name="input"></param>
	private void Boost(KartInput.NetworkInputData input)
	{
		if (BoostTime > 0)
		{
			MaxSpeed = maxSpeedBoosting;
			AppliedSpeed = Mathf.Lerp(AppliedSpeed, MaxSpeed, Runner.DeltaTime);
		}
		else if (BoostEndTick != -1)
		{
			StopBoosting();
		}
	}

	/// <summary>
	/// This corrects the kart visuals to the ground normal so the edges of the kart dont clip into the floor
	/// </summary>
	private void GroundNormalRotation()
	{
		var wasOffroad = IsOffroad;

		IsGrounded = Physics.SphereCast(collider.transform.TransformPoint(collider.center), collider.radius - 0.1f,
			Vector3.down, out var hit, 0.3f, ~LayerMask.GetMask("Kart"));

		if (IsGrounded)
		{
			Debug.DrawRay(hit.point, hit.normal, Color.magenta);
			GroundResistance = hit.collider.material.dynamicFriction;

			model.transform.rotation = Quaternion.Lerp(
				model.transform.rotation,
				Quaternion.FromToRotation(model.transform.up * 2, hit.normal) * model.transform.rotation,
				7.5f * Time.deltaTime);
		}

		if (wasOffroad != IsOffroad)
		{
			if (Kart != null && Kart.Animator != null)
			{
				if (IsOffroad)
					Kart.Animator.PlayOffroad();
				else
					Kart.Animator.StopOffroad();
			}
		}
	}

	private void UpdateTireYaw(KartInput.NetworkInputData input)
	{
		TireYaw = input.Steer * maxSteerStrength;
	}

	private void UpdateTireRotation()
	{
		if (tireYawFL != null)
			tireYawFL.localEulerAngles = LerpAxis(tireYawAxis, tireYawFL.localEulerAngles, TireYaw, 5 * Time.deltaTime);
		if (tireYawFR != null)
			tireYawFR.localEulerAngles = LerpAxis(tireYawAxis, tireYawFR.localEulerAngles, TireYaw, 5 * Time.deltaTime);

		if (CanDrive)
		{
			if (tireFL != null) tireFL.Rotate(90 * Time.deltaTime * AppliedSpeed * 0.5f, 0, 0);
			if (tireFR != null) tireFR.Rotate(90 * Time.deltaTime * AppliedSpeed * 0.5f, 0, 0);
			if (tireBL != null) tireBL.Rotate(90 * Time.deltaTime * AppliedSpeed * 0.5f, 0, 0);
			if (tireBR != null) tireBR.Rotate(90 * Time.deltaTime * AppliedSpeed * 0.5f, 0, 0);
		}
	}

	// One-Shot Functions

	private void StartDrifting(KartInput.NetworkInputData input)
	{
		if (AppliedSpeed < speedToDrift || input.Steer == 0)
		{
			StopDrifting();
			return;
		}

		IsDriftingRight = input.Steer > 0f;
		IsDriftingLeft = input.Steer < 0f;
	}

	private void StopDrifting()
	{
		if (driftTiers == null || driftTiers.Length == 0)
		{
			StopBoosting();
			DriftStartTick = -1;
			DriftTierIndex = -1;
			IsDriftingLeft = false;
			IsDriftingRight = false;
			return;
		}

		BoostTierIndex = DriftTierIndex == -1 ? 0 : DriftTierIndex;
		BoostEndTick = BoostTierIndex == 0
			? -1
			: Runner.Tick +
			  (int)(driftTiers[BoostTierIndex].boostDuration / Runner.DeltaTime);

		if (BoostTime <= 0) StopBoosting();

		DriftStartTick = -1;
		DriftTierIndex = -1;
		IsDriftingLeft = false;
		IsDriftingRight = false;
	}

	private void StopBoosting()
	{
		BoostTierIndex = 0;
		BoostEndTick = -1;
		MaxSpeed = maxSpeedNormal;
	}

	public void GiveBoost(bool isBoostpad, int tier = 1)
	{
		if (driftTiers == null || driftTiers.Length == 0)
			return;

		tier = Mathf.Clamp(tier, 0, driftTiers.Length - 1);

		if (isBoostpad)
		{
			//
			// If we are given a boost from a boostpad, we need to add a cooldown to ensure that we dont get a boost
			// every frame we are in contact with the boost pad.
			// 
			if (!BoostpadCooldown.ExpiredOrNotRunning(Runner))
				return;

			BoostpadCooldown = TickTimer.CreateFromSeconds(Runner, 4f);
		}

		// set the boost tier to 'tier' only if it's a higher tier than current
		BoostTierIndex = BoostTierIndex > tier ? BoostTierIndex : tier;

		if (BoostEndTick == -1) BoostEndTick = Runner.Tick;
		BoostEndTick += (int)(driftTiers[tier].boostDuration / Runner.DeltaTime);
	}

	public void RefreshAppliedSpeed()
	{
		AppliedSpeed = transform.InverseTransformDirection(Rigidbody.linearVelocity).z;
	}

	// Utility functions

	private static Vector3 LerpAxis(Axis axis, Vector3 euler, float tgtVal, float t)
	{
		if (axis == Axis.X) return new Vector3(Mathf.LerpAngle(euler.x, tgtVal, t), euler.y, euler.z);
		if (axis == Axis.Y) return new Vector3(euler.x, Mathf.LerpAngle(euler.y, tgtVal, t), euler.z);
		return new Vector3(euler.x, euler.y, Mathf.LerpAngle(euler.z, tgtVal, t));
	}

	private static float Remap(float value, float srcMin, float srcMax, float destMin, float destMax, bool clamp = false)
	{
		if (clamp) value = Mathf.Clamp(value, srcMin, srcMax);
		return (value - srcMin) / (srcMax - srcMin) * (destMax - destMin) + destMin;
	}

	public DriftTier EvaluateDrift(float driftDuration, out int index)
	{
		if (driftTiers == null || driftTiers.Length == 0)
		{
			index = 0;
			return default;
		}

		var i = 0;
		var tier = driftTiers[0];
		while (i < driftTiers.Length)
		{
			if (driftDuration < tier.startTime)
			{
				tier = driftTiers[--i];
				break;
			}

			if (i < driftTiers.Length - 1)
				tier = driftTiers[++i];
			else
				break;
		}

		index = i;
		return tier;
	}

	public void ResetControllerState()
	{
		Rigidbody.linearVelocity = Vector3.zero;
		AppliedSpeed = 0;
		BoostEndTick = -1;
		BoostTierIndex = 0;
		transform.up = Vector3.up;
		model.transform.up = Vector3.up;
	}

	// type definitions

	public enum Axis
	{
		X,
		Y,
		Z
	}

	[Serializable]
	public struct DriftTier
	{
		public Color color;
		public float boostDuration;
		public float startTime;
	}
}