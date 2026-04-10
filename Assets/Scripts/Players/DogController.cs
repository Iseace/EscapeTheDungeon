using UnityEngine;
using Fusion;

/// <summary>
/// This script handles specific dog logic if it differs from the base PlayerSetup.
/// For now, it provides a clean structure for the new Dog prefab.
/// </summary>
public class DogController : NetworkBehaviour
{
    // Dog-specific logic can be added here
    // e.g., Barking, faster movement modifiers, etc.

    [SerializeField] private RuntimeAnimatorController dogAnimatorController;
    [SerializeField] private Avatar dogAvatar;

    public override void Spawned()
    {
        // Dog-specific initialization
    }
}
