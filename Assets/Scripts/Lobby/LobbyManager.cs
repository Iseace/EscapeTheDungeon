using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyNavigation : MonoBehaviour
{
    // Asegúrate de que este nombre coincida EXACTAMENTE con tu escena de personajes
    [SerializeField] private string characterSceneName = "CharacterSelect";

    public void OpenCharacterSelection()
    {
        // Cargamos la escena del círculo de piedra
        SceneManager.LoadScene(characterSceneName);
    }
}