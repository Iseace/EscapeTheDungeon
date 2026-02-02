using UnityEngine;
using Fusion;
using UnityEngine.Animations;

public class PlayerSetup : NetworkBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private GameObject graphicsContainer; // El objeto "Graphics" que contiene a los personajes
    [SerializeField] private float cameraHeight = 1.6f;

    [Header("Visual Models")]
    [SerializeField] private GameObject[] characterModels; // Arrastra aquí a Mago, Guerrero, etc.
    [SerializeField] private Avatar[] characterAvatars; // Arrastra aquí los Avatares correspondientes

    // Esta variable sincroniza el personaje para TODOS en la red
    [Networked]
    public int SelectedCharacterIndex { get; set; }
    public ParentConstraint wandConstraint;

    public override void Spawned()
    {
        // 1. Configuración de Cámara (Solo local)
        if (HasInputAuthority)
        {
            SetupCamera();
            LockCursor();

            // 2. Leer la selección guardada y avisar al servidor
            int idGuardado = PlayerPrefs.GetInt("SelectedCharacterID", 0);

            // Enviamos un RPC para que el Host asigne nuestro personaje
            Rpc_RequestCharacterSelection(idGuardado);
        }
    }

    // El cliente le pide al servidor: "Ponme el modelo X"
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestCharacterSelection(int index)
    {
        // Solo el StateAuthority (Host) puede cambiar valores [Networked]
        SelectedCharacterIndex = index;
    }

    // Render se encarga de la visual en tiempo real sin afectar la física
    public override void Render()
    {
        if (graphicsContainer == null) return;

        Animator anim = graphicsContainer.GetComponent<Animator>();
        int currentID = SelectedCharacterIndex;

        // 1. CICLO PARA MODELOS Y AVATARS
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

        // 2. NUEVO: SINCRONIZAR EL PESO DEL CONSTRAINT (MANO IZQ/DER)
        if (wandConstraint != null)
        {
            for (int s = 0; s < wandConstraint.sourceCount; s++)
            {
                ConstraintSource source = wandConstraint.GetSource(s);

                // Si el índice del 'Source' coincide con el ID del personaje, peso 1 (activo)
                // Si no, peso 0 (inactivo)
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