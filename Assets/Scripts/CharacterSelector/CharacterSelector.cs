using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class CharacterSelector : MonoBehaviour
{
    public float rotationSpeed = 5f;
    public float camMoveSpeed = 5f;
    public string serverSceneName = "LobbyRoom";

    public InputAction navigateAction;
    public Transform regularCameraSocket;
    public Transform dogCamSocket;
    public Transform mainCamera;
    public GameObject normalCarousel;
    public GameObject secretCharacter;

    private int currentIndex = 0;
    private Quaternion targetRotation;
    private List<Animator> characterAnimators = new List<Animator>();
    private int totalCharacters;
    private float previousNavInputX = 0f;
    private float previousNavInputY = 0f;
    private bool isSecretSelected = false;

    private void OnEnable() => navigateAction.Enable();
    private void OnDisable() => navigateAction.Disable();

    void Start()
    {
        targetRotation = normalCarousel.transform.rotation;
        foreach (Transform child in normalCarousel.transform)
        {
            Animator anim = child.GetComponent<Animator>();
            if (anim != null) characterAnimators.Add(anim);
        }
        totalCharacters = characterAnimators.Count;
        UpdateAnimations();
    }

    void Update()
    {
        Vector2 navInput = navigateAction.ReadValue<Vector2>();

        // Horizontal Navigation
        if (previousNavInputX == 0f && !isSecretSelected)
        {
            if (navInput.x > 0.5f)
                RotateCarousel(-1);
            else if (navInput.x < -0.5f)
                RotateCarousel(1);
        }

        // Vertical Navigation (Secret Character)
        if (previousNavInputY == 0f)
        {
            if (navInput.y < -0.5f && !isSecretSelected)
            {
                ToggleSecret(true);
            }
            else if (navInput.y > 0.5f && isSecretSelected)
            {
                ToggleSecret(false);
            }
        }

        previousNavInputX = navInput.x;
        previousNavInputY = navInput.y;

        // Smooth Carousel Rotation
        normalCarousel.transform.rotation = Quaternion.Slerp(normalCarousel.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Smooth Camera Movement
        Transform targetSocket = isSecretSelected ? dogCamSocket : regularCameraSocket;
        mainCamera.position = Vector3.Lerp(mainCamera.position, targetSocket.position, camMoveSpeed * Time.deltaTime);
        mainCamera.rotation = Quaternion.Slerp(mainCamera.rotation, targetSocket.rotation, camMoveSpeed * Time.deltaTime);
    }

    private void ToggleSecret(bool selected)
    {
        isSecretSelected = selected;
        UpdateAnimations();
        Debug.Log(selected ? "Secret Character Selected!" : "Returned to Carousel");
    }

    // ✅ NOW PUBLIC — called by UI buttons
    public void RotateCarousel(int direction)
    {
        if (isSecretSelected) return;

        currentIndex += direction;
        float angle = currentIndex * (360f / totalCharacters);
        targetRotation = Quaternion.Euler(0, -angle, 0);
        UpdateAnimations();
    }

    // ✅ Touch button helpers — wire these to OnClick()
    public void OnTouchLeft() => RotateCarousel(1);
    public void OnTouchRight() => RotateCarousel(-1);
    public void OnTouchDown() => ToggleSecret(true);
    public void OnTouchUp() => ToggleSecret(false);

    void UpdateAnimations()
    {
        // Update carousel characters
        for (int i = 0; i < characterAnimators.Count; i++)
        {
            int correctIndex = GetNormalizedIndex();
            bool isSelectedState = (i == correctIndex && !isSecretSelected);
            characterAnimators[i].SetBool("isSelected", isSelectedState);
        }

        // Update secret character
        Animator secretAnim = secretCharacter.GetComponent<Animator>();
        if (secretAnim != null)
        {
            secretAnim.SetBool("isSelected", isSecretSelected);
        }
    }

    // Función auxiliar para normalizar el índice (evita números negativos y fuera de rango)
    private int GetNormalizedIndex()
    {
        int r = currentIndex % totalCharacters;
        return (r < 0) ? -r : (totalCharacters - r) % totalCharacters;
    }

    // --- FUNCIÓN PARA EL BOTÓN DE CONFIRMAR ---
    public void ConfirmGoBack()
    {
        // 1. Obtener el índice real del personaje seleccionado (99 para el secreto)
        int personajeSeleccionado = isSecretSelected ? 99 : GetNormalizedIndex();

        // 2. Guardar en PlayerPrefs
        PlayerPrefs.SetInt("SelectedCharacterID", personajeSeleccionado);

        // Opcional: Si tienes un InputField para el nombre, guárdalo aquí también
        // PlayerPrefs.SetString("PlayerNick", myInputField.text);

        PlayerPrefs.Save();
        Debug.Log("Personaje " + personajeSeleccionado + " guardado. Volviendo a servidores...");

        // 3. Volver a la escena de servidores
        SceneManager.LoadScene(serverSceneName);
    }
}