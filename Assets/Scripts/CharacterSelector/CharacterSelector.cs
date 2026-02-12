using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // 1. NECESARIO para las acciones

public class CharacterSelector : MonoBehaviour
{
    public float rotationSpeed = 5f;
    public string serverSceneName = "LobbyRoom";

    // 2. Variable para leer el movimiento (Flechas, WASD, Stick)
    public InputAction navigateAction;

    private int currentIndex = 0;
    private Quaternion targetRotation;
    private List<Animator> characterAnimators = new List<Animator>();
    private int totalCharacters;
    private float previousNavInput = 0f; // Para detectar cambios (como GetKeyDown)

    // 3. ACTIVAR Y DESACTIVAR las acciones (Obligatorio)
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
        // 4. LÓGICA DE INPUT - Detecta cambios (funciona como GetKeyDown)
        float navInput = navigateAction.ReadValue<Vector2>().x;

        // Detecta cuando se presiona (cambio de 0 a valor positivo/negativo)
        if (previousNavInput == 0f)
        {
            if (navInput > 0.5f) // Derecha
            {
                RotateCarousel(-1);
            }
            else if (navInput < -0.5f) // Izquierda
            {
                RotateCarousel(1);
            }
        }

        previousNavInput = navInput;

        // Tu lógica de suavizado original
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void RotateCarousel(int direction)
    {
        currentIndex += direction;
        float angle = currentIndex * (360f / totalCharacters);
        targetRotation = Quaternion.Euler(0, -angle, 0);

        UpdateAnimations();
    }

    void UpdateAnimations()
    {
        for (int i = 0; i < characterAnimators.Count; i++)
        {
            int correctIndex = GetNormalizedIndex();
            bool isSelectedState = (i == correctIndex);
            characterAnimators[i].SetBool("isSelected", isSelectedState);
        }
    }

    private int GetNormalizedIndex()
    {
        int r = currentIndex % totalCharacters;
        return (r < 0) ? -r : (totalCharacters - r) % totalCharacters;
    }

    public void ConfirmGoBack()
    {
        int personajeSeleccionado = GetNormalizedIndex();
        PlayerPrefs.SetInt("SelectedCharacterID", personajeSeleccionado);
        PlayerPrefs.Save();

        Debug.Log("Personaje " + personajeSeleccionado + " guardado. Volviendo a servidores...");
        SceneManager.LoadScene(serverSceneName);
    }
}