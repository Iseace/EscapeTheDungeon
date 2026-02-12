using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    [Header("Valores por Defecto")]
    public string playerName = "Player";
    public int characterIndex = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrEmpty(newName)) playerName = "Player";
        else playerName = newName;
    }

    public void SetCharacterIndex(int index)
    {
        characterIndex = index;
    }
}