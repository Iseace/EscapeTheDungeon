using UnityEngine;
using Fusion;

public enum TrackModifierType
{
    Normal,
    VelocityBoost,
    InvertedSteering
}

[RequireComponent(typeof(Collider))]
public class ModifyManager : NetworkBehaviour
{
    [Header("Modifier")]
    [SerializeField] private TrackModifierType modifierType = TrackModifierType.Normal;
    [SerializeField] private float durationSeconds = 4f;

    [Header("Velocity")]
    [SerializeField] private float velocityMultiplier = 1.5f;

    [Header("Single Use")]
    [SerializeField] private bool singleUse = false;

    [Networked]
    private NetworkBool hasBeenUsed { get; set; }

    public override void Spawned()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (singleUse && hasBeenUsed)
        {
            return;
        }

        BroomMove broom = other.GetComponentInParent<BroomMove>();
        if (broom == null)
        {
            return;
        }

        ApplyModifierToBroom(broom);

        if (singleUse)
        {
            hasBeenUsed = true;
        }
    }

    private void ApplyModifierToBroom(BroomMove broom)
    {
        switch (modifierType)
        {
            case TrackModifierType.Normal:
                broom.RPC_RequestClearTrackModifier();
                break;

            case TrackModifierType.VelocityBoost:
                broom.RPC_RequestTrackModifier(Mathf.Max(0.1f, velocityMultiplier), false, Mathf.Max(0f, durationSeconds));
                break;

            case TrackModifierType.InvertedSteering:
                broom.RPC_RequestTrackModifier(1f, true, Mathf.Max(0f, durationSeconds));
                break;
        }
    }
}
