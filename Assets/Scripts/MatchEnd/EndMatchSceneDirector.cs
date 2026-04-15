using UnityEngine;

public class EndMatchSceneDirector : MonoBehaviour
{
    [Header("Variant Roots")]
    [SerializeField] private GameObject bossWithKillsRoot;
    [SerializeField] private GameObject bossWithoutKillsRoot;
    [SerializeField] private GameObject survivorsEscapedRoot;

    [Header("Fallback")]
    [SerializeField] private EndCinematicVariant fallbackVariant = EndCinematicVariant.BossWithoutKills;
    [SerializeField] private bool clearSnapshotAfterApply = false;
    [SerializeField] private bool debugLogs = true;

    private void Start()
    {
        ApplyVariantFromSnapshot();
    }

    public void ApplyVariantFromSnapshot()
    {
        MatchEndSnapshot snapshot = MatchEndRuntimeContext.LatestSnapshot;
        EndCinematicVariant variant = snapshot != null
            ? MatchEndSnapshotEvaluator.ResolveLocalVariant(snapshot)
            : fallbackVariant;

        ApplyVariant(variant);

        if (debugLogs)
        {
            string reason = snapshot != null ? snapshot.Reason.ToString() : "NoSnapshot";
            Debug.Log($"[EndMatchSceneDirector] Variant={variant} | Reason={reason}");
        }

        if (clearSnapshotAfterApply)
        {
            MatchEndRuntimeContext.Clear();
        }
    }

    public void ApplyVariant(EndCinematicVariant variant)
    {
        SetActive(bossWithKillsRoot, variant == EndCinematicVariant.BossWithKills);
        SetActive(bossWithoutKillsRoot, variant == EndCinematicVariant.BossWithoutKills);
        SetActive(survivorsEscapedRoot, variant == EndCinematicVariant.SurvivorsEscaped);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target == null) return;
        if (target.activeSelf == active) return;
        target.SetActive(active);
    }
}