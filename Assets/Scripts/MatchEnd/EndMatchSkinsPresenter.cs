using System.Collections.Generic;
using UnityEngine;

public class EndMatchSkinsPresenter : MonoBehaviour
{
    private enum SlotAnimationMode
    {
        None,
        ForceState,
        SetMoveParameters
    }

    [Header("Skin Prefabs By Character Index")]
    [Tooltip("Index 0..N debe corresponder al SelectedCharacterIndex del PlayerSetup")]
    [SerializeField] private List<GameObject> survivorSkinPrefabs = new List<GameObject>();
    [SerializeField] private GameObject fallbackSurvivorSkinPrefab;

    [Header("Slots")]
    [SerializeField] private Transform[] defeatedSlots;
    [SerializeField] private Transform[] escapedSlots;

    [Header("Spawn Rules")]
    [SerializeField] private bool spawnDefeatedInBossWithKills = true;
    [SerializeField] private bool spawnEscapedInSurvivorsEscaped = true;
    [SerializeField] private bool clearChildrenOnStart = true;
    [SerializeField] private bool debugLogs = true;

    [Header("Animation Control")]
    [SerializeField] private SlotAnimationMode defeatedAnimationMode = SlotAnimationMode.ForceState;
    [SerializeField] private SlotAnimationMode escapedAnimationMode = SlotAnimationMode.SetMoveParameters;
    [Tooltip("Nombre de estado para derrotados, por ejemplo Dead o Knockdown")]
    [SerializeField] private string defeatedStateName = "Dead";
    [Tooltip("Nombre de estado para escaped, por ejemplo Locomotion o Run")]
    [SerializeField] private string escapedStateName = "Locomotion";
    [Tooltip("Parametros float de locomocion comunes en tus Animator Controllers")]
    [SerializeField] private string[] moveFloatParameters = { "Speed", "MoveSpeed", "Velocity" };
    [SerializeField] private float escapedMoveValue = 1f;
    [SerializeField] private float defeatedMoveValue = 0f;
    [Header("Common Locomotion Params")]
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private float escapedMoveXValue = 0f;
    [SerializeField] private string moveZParameter = "MoveZ";
    [SerializeField] private float escapedMoveZValue = 1f;
    [SerializeField] private string isGroundedParameter = "IsGrounded";
    [SerializeField] private bool escapedIsGroundedValue = true;

    [Header("Escaped Runtime Movement")]
    [SerializeField] private bool addRunnerControllerToLocalEscaped = false;
    [SerializeField] private float escapedForwardSpeed = 4f;
    [SerializeField] private float escapedLateralSpeed = 5f;
    [SerializeField] private Vector2 escapedXBounds = new Vector2(-6f, 6f);
    [SerializeField] private bool allowLocalForwardInput = false;

    private readonly List<GameObject> spawnedInstances = new List<GameObject>();

    private void Start()
    {
        BuildVisuals();
    }

    public void BuildVisuals()
    {
        MatchEndSnapshot snapshot = MatchEndRuntimeContext.LatestSnapshot;
        if (snapshot == null)
        {
            if (debugLogs)
                Debug.LogWarning("[EndMatchSkinsPresenter] No hay snapshot. No se generan skins.");
            return;
        }

        EndCinematicVariant variant = MatchEndSnapshotEvaluator.ResolveLocalVariant(snapshot);

        if (clearChildrenOnStart)
            ClearSpawned();

        if (variant == EndCinematicVariant.BossWithKills && spawnDefeatedInBossWithKills)
            SpawnGroup(snapshot, wantEscaped: false, defeatedSlots);

        if (variant == EndCinematicVariant.SurvivorsEscaped && spawnEscapedInSurvivorsEscaped)
            SpawnGroup(snapshot, wantEscaped: true, escapedSlots);

        if (debugLogs)
            Debug.Log($"[EndMatchSkinsPresenter] Variant={variant}, spawned={spawnedInstances.Count}");
    }

    private void SpawnGroup(MatchEndSnapshot snapshot, bool wantEscaped, Transform[] slots)
    {
        if (slots == null || slots.Length == 0)
        {
            if (debugLogs)
                Debug.LogWarning("[EndMatchSkinsPresenter] No hay slots asignados para el grupo.");
            return;
        }

        int slotIndex = 0;
        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            PlayerMatchResult player = snapshot.Players[i];
            if (player == null) continue;
            if (player.IsBoss) continue;

            bool isEscaped = player.EndState == PlayerEndState.Escaped;
            if (wantEscaped != isEscaped) continue;

            if (slotIndex >= slots.Length)
                break;

            Transform slot = slots[slotIndex];
            slotIndex++;
            if (slot == null) continue;

            GameObject prefab = ResolveSkinPrefab(player.SelectedCharacterIndex);
            if (prefab == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"[EndMatchSkinsPresenter] No hay prefab para index={player.SelectedCharacterIndex}.");
                continue;
            }

            GameObject instance = Instantiate(prefab, slot.position, slot.rotation, slot);
            spawnedInstances.Add(instance);

            bool isLocalPlayer = player.PlayerId == snapshot.LocalPlayerId;
            if (wantEscaped && isLocalPlayer && addRunnerControllerToLocalEscaped)
            {
                EndMatchEscapeRunnerController mover = instance.GetComponent<EndMatchEscapeRunnerController>();
                if (mover == null)
                    mover = instance.AddComponent<EndMatchEscapeRunnerController>();

                ConfigureEscapedRunnerController(mover);
            }

            // Best effort: if prefab has controller, keep deterministic visuals for cinematic start.
            Animator anim = instance.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                if (wantEscaped)
                {
                    anim.SetBool("IsDead", false);
                    ApplyAnimationMode(anim, escapedAnimationMode, escapedStateName, escapedMoveValue, "escaped");
                    ApplyCommonEscapedLocomotion(anim);
                }
                else
                {
                    anim.SetBool("IsDead", true);
                    ApplyAnimationMode(anim, defeatedAnimationMode, defeatedStateName, defeatedMoveValue, "defeated");
                }
            }
        }
    }

    private void ApplyCommonEscapedLocomotion(Animator anim)
    {
        if (anim == null)
            return;

        if (HasFloatParameter(anim, moveXParameter))
            anim.SetFloat(moveXParameter, escapedMoveXValue);

        if (HasFloatParameter(anim, moveZParameter))
            anim.SetFloat(moveZParameter, escapedMoveZValue);

        if (HasBoolParameter(anim, isGroundedParameter))
            anim.SetBool(isGroundedParameter, escapedIsGroundedValue);
    }

    private void ConfigureEscapedRunnerController(EndMatchEscapeRunnerController mover)
    {
        if (mover == null)
            return;

        mover.Configure(escapedForwardSpeed, escapedLateralSpeed, escapedXBounds, allowLocalForwardInput);
    }

    private void ApplyAnimationMode(Animator anim, SlotAnimationMode mode, string stateName, float moveValue, string groupLabel)
    {
        if (anim == null) return;

        switch (mode)
        {
            case SlotAnimationMode.ForceState:
                if (!string.IsNullOrWhiteSpace(stateName) && HasStateInAnyLayer(anim, stateName))
                {
                    anim.CrossFadeInFixedTime(stateName, 0.1f);
                }
                else if (debugLogs)
                {
                    Debug.LogWarning($"[EndMatchSkinsPresenter] Estado '{stateName}' no encontrado para {groupLabel} en {anim.name}.");
                }
                break;

            case SlotAnimationMode.SetMoveParameters:
                bool appliedAny = false;
                if (moveFloatParameters != null)
                {
                    for (int i = 0; i < moveFloatParameters.Length; i++)
                    {
                        string param = moveFloatParameters[i];
                        if (string.IsNullOrWhiteSpace(param)) continue;
                        if (!HasFloatParameter(anim, param)) continue;

                        anim.SetFloat(param, moveValue);
                        appliedAny = true;
                    }
                }

                if (!appliedAny && debugLogs)
                {
                    Debug.LogWarning($"[EndMatchSkinsPresenter] No se aplico ningun parametro float para {groupLabel} en {anim.name}. Revisa moveFloatParameters.");
                }
                break;
        }
    }

    private GameObject ResolveSkinPrefab(int selectedCharacterIndex)
    {
        if (selectedCharacterIndex >= 0 && selectedCharacterIndex < survivorSkinPrefabs.Count)
            return survivorSkinPrefabs[selectedCharacterIndex];

        return fallbackSurvivorSkinPrefab;
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawnedInstances.Count; i++)
        {
            GameObject item = spawnedInstances[i];
            if (item == null) continue;
            Destroy(item);
        }

        spawnedInstances.Clear();

        ClearChildrenInSlots(defeatedSlots);
        ClearChildrenInSlots(escapedSlots);
    }

    private static void ClearChildrenInSlots(Transform[] slots)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];
            if (slot == null) continue;

            for (int c = slot.childCount - 1; c >= 0; c--)
            {
                Transform child = slot.GetChild(c);
                if (child == null) continue;
                Destroy(child.gameObject);
            }
        }
    }

    private static bool HasStateInAnyLayer(Animator anim, string stateName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(stateName)) return false;

        int stateHash = Animator.StringToHash(stateName);
        for (int layer = 0; layer < anim.layerCount; layer++)
        {
            if (anim.HasState(layer, stateHash))
                return true;
        }

        return false;
    }

    private static bool HasFloatParameter(Animator anim, string parameterName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(parameterName)) return false;

        AnimatorControllerParameter[] parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type != AnimatorControllerParameterType.Float) continue;
            if (parameters[i].name != parameterName) continue;
            return true;
        }

        return false;
    }

    private static bool HasBoolParameter(Animator anim, string parameterName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(parameterName)) return false;

        AnimatorControllerParameter[] parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type != AnimatorControllerParameterType.Bool) continue;
            if (parameters[i].name != parameterName) continue;
            return true;
        }

        return false;
    }
}