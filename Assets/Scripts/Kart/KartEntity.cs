using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class KartEntity : KartComponent
{
	private const string CharacterIdKey = "SelectedCharacterID";

	public static event Action<KartEntity> OnKartSpawned;
	public static event Action<KartEntity> OnKartDespawned;

	public event Action<int> OnHeldItemChanged;
	public event Action<int> OnCoinCountChanged;

	public KartAnimator Animator { get; private set; }
	public KartCamera Camera { get; private set; }
	public KartController Controller { get; private set; }
	public KartInput Input { get; private set; }
	public KartLapController LapController { get; private set; }
	public KartAudio Audio { get; private set; }
	public GameUI Hud { get; private set; }
	public KartItemController Items { get; private set; }
	public NetworkRigidbody3D Rigidbody { get; private set; }

	public Powerup HeldItem =>
		HeldItemIndex == -1
			? null
			: ResourceManager.Instance.powerups[HeldItemIndex];

	[Networked]
	public int HeldItemIndex { get; set; } = -1;

	[Networked]
	public int CoinCount { get; set; }

	public Transform itemDropNode;
	[SerializeField] private GameObject[] characterModels;

	[Networked] public int SelectedCharacterIndex { get; set; }
	public bool IsNetworkReady => _isSpawned && Object != null && Object.IsValid;

	public GameObject[] CharacterModels => characterModels;
	public int ActiveCharacterIndex
	{
		get
		{
			if (characterModels == null || characterModels.Length == 0)
				return 0;

			int selected = IsNetworkReady ? SelectedCharacterIndex : 0;
			return Mathf.Clamp(selected, 0, characterModels.Length - 1);
		}
	}

	public GameObject GetActiveCharacterModel()
	{
		if (characterModels == null || characterModels.Length == 0)
			return null;

		int selected = IsNetworkReady ? SelectedCharacterIndex : 0;
		selected = Mathf.Clamp(selected, 0, characterModels.Length - 1);
		return characterModels[selected];
	}

	private bool _despawned;
	private bool _isSpawned;

	private ChangeDetector _changeDetector;

	private static void OnHeldItemIndexChangedCallback(KartEntity changed)
	{
		changed.OnHeldItemChanged?.Invoke(changed.HeldItemIndex);

		if (changed.HeldItemIndex != -1)
		{
			foreach (var behaviour in changed.GetComponentsInChildren<KartComponent>())
				behaviour.OnEquipItem(changed.HeldItem, 3f);
		}
	}

	private static void OnCoinCountChangedCallback(KartEntity changed)
	{
		changed.OnCoinCountChanged?.Invoke(changed.CoinCount);
	}

	private void Awake()
	{
		// Set references before initializing all components
		Animator = GetComponentInChildren<KartAnimator>();
		Camera = GetComponent<KartCamera>();
		Controller = GetComponent<KartController>();
		Input = GetComponent<KartInput>();
		LapController = GetComponent<KartLapController>();
		Audio = GetComponentInChildren<KartAudio>();
		Items = GetComponent<KartItemController>();
		Rigidbody = GetComponent<NetworkRigidbody3D>();

		// Initializes all KartComponents on or under the Kart prefab
		var components = GetComponentsInChildren<KartComponent>();
		foreach (var component in components) component.Init(this);
	}

	public static readonly List<KartEntity> Karts = new List<KartEntity>();

	public override void Spawned()
	{
		base.Spawned();
		_isSpawned = true;

		_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

		if (Object.HasInputAuthority)
		{
			int selected = PlayerPrefs.GetInt(CharacterIdKey, 0);
			Rpc_SetSelectedCharacter(selected);

			// Create HUD
			Hud = Instantiate(ResourceManager.Instance.hudPrefab);
			Hud.Init(this);

			Instantiate(ResourceManager.Instance.nicknameCanvasPrefab);
		}

		Karts.Add(this);
		OnKartSpawned?.Invoke(this);
	}

	[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
	private void Rpc_SetSelectedCharacter(int index)
	{
		if (characterModels == null || characterModels.Length == 0)
		{
			SelectedCharacterIndex = 0;
			return;
		}

		SelectedCharacterIndex = Mathf.Clamp(index, 0, characterModels.Length - 1);
	}

	public override void Render()
	{
		ApplySelectedCharacterModel();

		foreach (var change in _changeDetector.DetectChanges(this))
		{
			switch (change)
			{
				case nameof(HeldItemIndex):
					OnHeldItemIndexChangedCallback(this);
					break;
				case nameof(CoinCount):
					OnCoinCountChangedCallback(this);
					break;
			}
		}
	}

	private void ApplySelectedCharacterModel()
	{
		if (characterModels == null || characterModels.Length == 0)
			return;

		int selected = Mathf.Clamp(SelectedCharacterIndex, 0, characterModels.Length - 1);
		for (int i = 0; i < characterModels.Length; i++)
		{
			if (characterModels[i] == null)
				continue;

			bool active = i == selected;
			if (characterModels[i].activeSelf != active)
			{
				characterModels[i].SetActive(active);
			}
		}
	}

	public override void Despawned(NetworkRunner runner, bool hasState)
	{
		base.Despawned(runner, hasState);
		_isSpawned = false;
		Karts.Remove(this);
		_despawned = true;
		OnKartDespawned?.Invoke(this);
	}

	private void OnDestroy()
	{
		Karts.Remove(this);
		if (!_despawned)
		{
			OnKartDespawned?.Invoke(this);
		}
	}

	private void OnTriggerStay(Collider other)
	{

		if (other.TryGetComponent(out ICollidable collidable))
		{
			collidable.Collide(this);
		}
	}

	public bool SetHeldItem(int index)
	{
		if (HeldItem != null) return false;

		HeldItemIndex = index;
		return true;
	}

	public void SpinOut()
	{
		Controller.IsSpinout = true;
	}

	private IEnumerable OnSpinOut()
	{
		yield return new WaitForSeconds(2f);

		Controller.IsSpinout = false;
	}
}