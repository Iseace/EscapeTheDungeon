using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;

public class PlayerSetup : NetworkBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private GameObject graphicsContainer;
    [SerializeField] private float cameraHeight = 1.6f;

    [Header("Visual Models")]
    [SerializeField] private GameObject[] characterModels;
    [SerializeField] private Avatar[] characterAvatars;

    [Networked]
    public int SelectedCharacterIndex { get; set; }

    [Networked]
    public NetworkBool HasEscaped { get; set; }
    public ParentConstraint wandConstraint;
    private bool escapeHandledLocally;

    // The resolved eye-height pivot for this player, available on ALL instances
    // (not just the local one) so SpectatorSystem can read it on remote players.
    private Transform _activeCameraPivot;
    public Transform GetCameraPivot() => _activeCameraPivot;

    public override void Spawned()
    {
        escapeHandledLocally = false;

        // Pivot creation runs on EVERY instance so remote players have it too
        EnsureCameraPivot();

        if (HasInputAuthority)
        {
            // Attach the main camera only on the machine that owns this player
            AttachCamera();
            HandleCursorState();
            EnsurePortalDirectionHud();

            int idGuardado = PlayerPrefs.GetInt("SelectedCharacterID", 0);
            Rpc_RequestCharacterSelection(idGuardado);
        }
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestEscapePortal()
    {
        if (HasEscaped) return;
        HasEscaped = true;
    }

    public override void Render()
    {
        TryHandleEscapedState();

        if (graphicsContainer == null) return;

        var role = GetComponent<PlayerRole>();
        if (role != null && role.IsBoss)
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

        for (int i = 0; i < characterModels.Length; i++)
        {
            bool isSelected = (i == currentID);

            if (characterModels[i].activeSelf != isSelected)
            {
                characterModels[i].SetActive(isSelected);
            }

            if (isSelected && anim != null && characterAvatars.Length > i)
            {
                if (anim.avatar != characterAvatars[i])
                {
                    anim.avatar = characterAvatars[i];
                    anim.Rebind();
                    anim.Update(0);
                    Debug.Log($"Avatar sincronizado: {characterModels[i].name}");
                }
            }
        }

        if (wandConstraint != null)
        {
            for (int s = 0; s < wandConstraint.sourceCount; s++)
            {
                ConstraintSource source = wandConstraint.GetSource(s);
                float targetWeight = (s == currentID) ? 1f : 0f;

                if (source.weight != targetWeight)
                {
                    source.weight = targetWeight;
                    wandConstraint.SetSource(s, source);
                }
            }
        }
    }

    private void TryHandleEscapedState()
    {
        if (!HasInputAuthority) return;
        if (!HasEscaped) return;
        if (escapeHandledLocally) return;

        escapeHandledLocally = true;

        var health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.EnterSpectatorModeFromEscape();
            return;
        }

        if (GetComponent<SpectatorSystem>() == null)
        {
            gameObject.AddComponent<SpectatorSystem>();
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

    private void EnsurePortalDirectionHud()
    {
        if (GetComponent<PortalDirectionHUD>() != null) return;
        gameObject.AddComponent<PortalDirectionHUD>();
    }
}