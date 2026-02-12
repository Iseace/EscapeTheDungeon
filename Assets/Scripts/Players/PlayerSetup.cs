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
    public ParentConstraint wandConstraint;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            SetupCamera();
            // Initial cursor state
            HandleCursorState();

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

    public override void Render()
    {
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

    private void SetupCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        FirstPersonCamera fpCamera = mainCam.GetComponent<FirstPersonCamera>();
        if (fpCamera == null) return;

        Transform cameraTarget = cameraPivot;
        if (cameraTarget == null)
        {
            GameObject pivot = new GameObject("CameraPivot_Runtime");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(0, cameraHeight, 0);
            cameraTarget = pivot.transform;
        }

        fpCamera.SetTarget(cameraTarget, graphicsContainer);
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