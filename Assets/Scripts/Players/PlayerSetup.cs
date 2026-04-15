using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using TMPro;

public class PlayerSetup : NetworkBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private GameObject graphicsContainer;
    [SerializeField] private float cameraHeight = 1.6f;

    [Header("Visual Models")]
    [SerializeField] private GameObject[] characterModels;
    [SerializeField] private Avatar[] characterAvatars;

    [Header("Nameplate")]
    [SerializeField] private TMP_Text nameplateText;

    [Networked]
    public int SelectedCharacterIndex { get; set; }

    [Networked]
    public NetworkBool HasEscaped { get; set; }
    public ParentConstraint wandConstraint;
    private bool escapeHandledLocally;
    private bool escapeCollisionDisabled;

    [Networked, OnChangedRender(nameof(OnNicknameChanged))]
    public NetworkString<_32> Nickname { get; set; }

    // Tracks whether the nickname has been successfully applied after scene sync
    private bool _nickApplied = false;

    // The resolved eye-height pivot for this player, available on ALL instances
    // (not just the local one) so SpectatorSystem can read it on remote players.
    private Transform _activeCameraPivot;
    public Transform GetCameraPivot() => _activeCameraPivot;

    public override void Spawned()
    {
        escapeHandledLocally = false;
        escapeCollisionDisabled = false;

        // Reset so Render() retries nickname sync on each spawn
        _nickApplied = false;

        // Pivot creation runs on EVERY instance so remote players have it too
        EnsureCameraPivot();

        if (HasInputAuthority)
        {
            // Attach the main camera only on the machine that owns this player
            AttachCamera();
            HandleCursorState();

            // Set the nickname for the player locally as a backup, 
            // the server will also set it from the connection token
            string nick = PlayerPrefs.GetString("Nickname", "").Trim();
            if (string.IsNullOrEmpty(nick))
                nick = "Player" + Runner.LocalPlayer.PlayerId;
            Rpc_SetNickname(nick);

            int idGuardado = PlayerPrefs.GetInt("SelectedCharacterID", 0);
            Rpc_RequestCharacterSelection(idGuardado);

            // Show own nickname in LobbyRoom, hide in Game scene
            if (nameplateText != null)
            {
                bool isLobby = SceneManager.GetActiveScene().name == "LobbyRoom";
                nameplateText.transform.parent.gameObject.SetActive(isLobby);
            }
        }
    }

    public void SetNameplateVisible(bool visible)
    {
        if (nameplateText != null)
            nameplateText.transform.parent.gameObject.SetActive(visible);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetNickname(string nick)
    {
        Nickname = nick;
    }

    // Called when the nickname changes
    private void OnNicknameChanged()
    {
        ApplyNickname();
        _nickApplied = true;
    }

    // Apply the nickname to the nameplate
    private void ApplyNickname()
    {
        if (nameplateText != null)
            nameplateText.text = Nickname.ToString();
    }

    // Added Update to force cursor visibility if a camera script tries to lock it in the Lobby
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestCharacterSelection(int index)
    {
        SelectedCharacterIndex = index;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestEscapePortal()
    {
        if (Object == null || !Object.HasStateAuthority) return;
        if (IsBossPlayer()) return;
        if (HasEscaped) return;
        HasEscaped = true;
    }

    public bool IsBossPlayer()
    {
        var role = GetComponent<PlayerRole>();
        if (role != null && role.IsBoss) return true;

        // Fallback for prefabs where role sync is late or missing.
        if (GetComponent<BossSpecial>() != null) return true;
        if (GetComponentInChildren<BossHitbox>(true) != null) return true;

        return false;
    }

    public override void Render()
    {
        // Keep trying to apply nickname every frame until Nickname is synced and not empty
        if (!_nickApplied && !string.IsNullOrEmpty(Nickname.ToString()))
        {
            ApplyNickname();
            _nickApplied = true;

            // After nickname syncs, re-apply own nameplate visibility rule
            if (HasInputAuthority)
            {
                bool isLobby = SceneManager.GetActiveScene().name == "LobbyRoom";
                nameplateText.transform.parent.gameObject.SetActive(isLobby);
            }
        }

        TryHandleEscapedState();

        if (HasEscaped)
        {
            SetAllCharacterModelsActive(false);
            return;
        }

        if (graphicsContainer == null) return;

        if (IsBossPlayer())
        {
            // Si es el Boss, nos aseguramos de que todos sus modelos en el array estén ACTIVOS
            foreach (var model in characterModels)
            {
                if (model != null && !model.activeSelf) model.SetActive(true);
            }
            return; // Salimos de la función para que no ejecute la lógica de supervivientes
        }

        Animator anim = graphicsContainer.GetComponent<Animator>();
        int currentID = SelectedCharacterIndex;

        // Special case: If this is the DogPrefab object, it might have one model (the dog)
        // or it might just be the base player with index 99.
        // If it's the DogPrefab, we need to make sure the model is active.
        bool isDogPrefabObject = gameObject.name.Contains("Dog");
        bool isDogSelection = (currentID == 99) || isDogPrefabObject;

        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] == null) continue;

            // If it's a DogPrefab, and it only has 1 model, that model MUST be the dog.
            bool isSelected = (i == currentID) || (isDogPrefabObject && characterModels.Length == 1);

            if (characterModels[i].activeSelf != isSelected)
            {
                characterModels[i].SetActive(isSelected);
            }

            int avatarIndex = (isDogPrefabObject && characterModels.Length == 1) ? 0 : i;

            if (isSelected && anim != null && characterAvatars != null && characterAvatars.Length > avatarIndex)
            {
                if (anim.avatar != characterAvatars[avatarIndex])
                {
                    anim.avatar = characterAvatars[avatarIndex];
                    Debug.Log($"Avatar synchronized: {characterModels[i].name} (Using index {avatarIndex})");
                }
            }
        }

        // Shared Wand Logic
        if (wandConstraint != null)
        {
            // Don't enable wand for Dog Selection
            if (isDogSelection)
            {
                if (wandConstraint.enabled) wandConstraint.enabled = false;
                return;
            }

            if (!wandConstraint.enabled) wandConstraint.enabled = true;

            for (int s = 0; s < wandConstraint.sourceCount; s++)
            {
                ConstraintSource source = wandConstraint.GetSource(s);

                bool isSelectedSource = (s == currentID);
                float targetWeight = (isSelectedSource) ? 1f : 0f;

                if (wandConstraint.GetSource(s).weight != targetWeight)
                {
                    ConstraintSource updatedSource = wandConstraint.GetSource(s);
                    updatedSource.weight = targetWeight;
                    wandConstraint.SetSource(s, updatedSource);
                }
            }
        }
    }

    private void TryHandleEscapedState()
    {
        if (!HasEscaped) return;

        if (!escapeCollisionDisabled)
        {
            DisableCollisionForEscape();
            escapeCollisionDisabled = true;
        }

        if (escapeHandledLocally) return;

        escapeHandledLocally = true;

        var health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.EnterSpectatorModeFromEscape();
            return;
        }

        if (HasInputAuthority && GetComponent<SpectatorSystem>() == null)
        {
            gameObject.AddComponent<SpectatorSystem>();
        }
    }

    private void DisableCollisionForEscape()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
        {
            cc.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null) continue;
            if (!col.enabled) continue;
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null) continue;
            rb.detectCollisions = false;
            rb.isKinematic = true;
        }
    }

    private void SetAllCharacterModelsActive(bool active)
    {
        if (characterModels == null || characterModels.Length == 0) return;

        for (int i = 0; i < characterModels.Length; i++)
        {
            var model = characterModels[i];
            if (model != null && model.activeSelf != active)
            {
                model.SetActive(active);
            }
        }
    }

    // Runs on ALL instances: ensures _activeCameraPivot is set at eye height.
    private void EnsureCameraPivot()
    {
        if (_activeCameraPivot != null) return; // already done

        if (cameraPivot != null)
        {
            // Use the pivot assigned in the Inspector
            _activeCameraPivot = cameraPivot;
        }
        else
        {
            // Create one at the configured eye height (same as before, but for everyone)
            const string runtimeName = "CameraPivot_Runtime";
            Transform existing = transform.Find(runtimeName);
            if (existing != null)
            {
                _activeCameraPivot = existing;
            }
            else
            {
                GameObject pivot = new GameObject(runtimeName);
                pivot.transform.SetParent(transform);
                pivot.transform.localPosition = new Vector3(0, cameraHeight, 0);
                _activeCameraPivot = pivot.transform;
            }
        }
    }

    // Runs only on the local player: attaches the main camera to the pivot.
    private void AttachCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        FirstPersonCamera fpCamera = mainCam.GetComponent<FirstPersonCamera>();
        if (fpCamera == null) return;

        fpCamera.SetTarget(_activeCameraPivot, graphicsContainer);
    }

    private void HandleCursorState()
    {
        // CHANGED: Using "LobbyRoom" to match your actual scene name
        if (SceneManager.GetActiveScene().name == "LobbyRoom")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}