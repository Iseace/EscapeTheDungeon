using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class CharacterSelector : MonoBehaviour
{
    public float rotationSpeed = 5f;
    public string serverSceneName = "LobbyRoom";

    public InputAction navigateAction;

    private int currentIndex = 0;
    private Quaternion targetRotation;
    private List<Animator> characterAnimators = new List<Animator>();
    private int totalCharacters;
    private float previousNavInput = 0f;

    private void OnEnable() => navigateAction.Enable();
    private void OnDisable() => navigateAction.Disable();

    void Start()
    {
        targetRotation = transform.rotation;
        foreach (Transform child in transform)
        {
            Animator anim = child.GetComponent<Animator>();
            if (anim != null) characterAnimators.Add(anim);
        }
        totalCharacters = characterAnimators.Count;
        UpdateAnimations();
    }

    void Update()
    {
        float navInput = navigateAction.ReadValue<Vector2>().x;

        if (previousNavInput == 0f)
        {
            if (navInput > 0.5f)
                RotateCarousel(-1);
            else if (navInput < -0.5f)
                RotateCarousel(1);
        }

        previousNavInput = navInput;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // ✅ NOW PUBLIC — called by UI buttons
    public void RotateCarousel(int direction)
    {
        currentIndex += direction;
        // No necesitamos el if de bucle aquí si usamos el operador % en el cálculo del ángulo

        float angle = currentIndex * (360f / totalCharacters);
        targetRotation = Quaternion.Euler(0, -angle, 0);
        UpdateAnimations();
    }

    // ✅ Touch button helpers — wire these to OnClick()
    public void OnTouchLeft() => RotateCarousel(1);
    public void OnTouchRight() => RotateCarousel(-1);

    void UpdateAnimations()
    {
        for (int i = 0; i < characterAnimators.Count; i++)
        {
            // Calculamos quién está al frente basándonos en el currentIndex
            int correctIndex = GetNormalizedIndex();
            bool isSelectedState = (i == correctIndex);
            characterAnimators[i].SetBool("isSelected", isSelectedState);
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
        // 1. Obtener el índice real del personaje seleccionado
        int personajeSeleccionado = GetNormalizedIndex();

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