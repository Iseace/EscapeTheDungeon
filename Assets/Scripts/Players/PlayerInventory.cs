using Fusion;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [Networked] public int CurrentWeaponID { get; set; }

    [Header("Prefabs para soltar")]
    // Arrastra aquí los prefabs de los báculos
    [SerializeField] public NetworkPrefabRef[] staffPrefabs;

    [Header("Visuales en la mano")]
    // Arrastra aquí los modelos que están dentro del WandSocket del personaje
    [SerializeField] private GameObject[] visualWands;

    // Dentro de PlayerInventory.cs
    private int _lastWeaponID = -1; // Rastrea el ID anterior

    public override void Render()
    {
        // Solo ejecutamos el código si el ID cambió desde el último frame
        if (CurrentWeaponID != _lastWeaponID)
        {
            for (int i = 0; i < visualWands.Length; i++)
            {
                if (visualWands[i] == null) continue;

                bool shouldBeActive = (i + 1) == CurrentWeaponID;
                visualWands[i].SetActive(shouldBeActive);

                // El log ahora solo saldrá una vez por cada báculo que recojas
                if (shouldBeActive)
                {
                    Debug.Log($"Inventario: Staff {i + 1} equipado.");
                }
            }

            // Actualizamos el centinela para que no entre aquí en el siguiente frame
            _lastWeaponID = CurrentWeaponID;
        }
    }

    // Solo el servidor llama a esto
    public void SwapWeapon(int newID, Vector3 dropPosition)
    {
        // 1. Si ya tiene algo, lo soltamos al suelo
        if (CurrentWeaponID > 0)
        {
            // Restamos 1 porque el ID 1 usa el índice 0 del arreglo
            NetworkPrefabRef oldStaff = staffPrefabs[CurrentWeaponID - 1];
            Runner.Spawn(oldStaff, dropPosition, Quaternion.identity);
        }

        // 2. Asignamos el nuevo ID
        CurrentWeaponID = newID;
    }
}
