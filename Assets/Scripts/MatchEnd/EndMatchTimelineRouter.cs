using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class EndMatchTimelineRouter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private EndMatchSceneDirector sceneDirector;

    [Header("Playable Directors")]
    [SerializeField] private PlayableDirector globalDirector;
    [SerializeField] private PlayableDirector bossWithKillsDirector;
    [SerializeField] private PlayableDirector bossWithoutKillsDirector;
    [SerializeField] private PlayableDirector survivorsEscapedDirector;

    [Header("Playback")]
    [SerializeField] private bool playGlobalDirector = true;
    [SerializeField] private bool playVariantDirector = true;
    [SerializeField] private float variantStartDelaySeconds = 0f;
    [Tooltip("Si es >= 0, fuerza este tiempo antes de mostrar resultados. Si es < 0, intenta usar la duracion de la timeline de variante.")]
    [SerializeField] private float showResultsAfterSeconds = -1f;
    [SerializeField] private EndCinematicVariant fallbackVariant = EndCinematicVariant.BossWithoutKills;

    [Header("Results UI")]
    [SerializeField] private bool showResultsPanelAfterCinematic = true;
    [SerializeField] private GameObject resultsPanelRoot;
    [SerializeField] private EndMatchResultsHUD resultsHud;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private void Start()
    {
        StartCoroutine(RunFlow());
    }

    private IEnumerator RunFlow()
    {
        if (sceneDirector == null)
        {
            sceneDirector = FindAnyObjectByType<EndMatchSceneDirector>();
        }

        if (sceneDirector != null)
        {
            sceneDirector.ApplyVariantFromSnapshot();
        }

        EndCinematicVariant variant = ResolveVariant();

        if (resultsPanelRoot != null)
        {
            resultsPanelRoot.SetActive(false);
        }

        if (playGlobalDirector)
        {
            PlayDirector(globalDirector, "global");
        }

        if (variantStartDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(variantStartDelaySeconds);
        }

        PlayableDirector variantDirector = GetVariantDirector(variant);
        if (playVariantDirector)
        {
            PlayDirector(variantDirector, variant.ToString());
        }

        float waitTime = ResolveWaitTime(variantDirector);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        if (!showResultsPanelAfterCinematic)
        {
            yield break;
        }

        if (resultsPanelRoot != null)
        {
            resultsPanelRoot.SetActive(true);
        }

        if (resultsHud != null)
        {
            resultsHud.RenderFromSnapshot();
        }
    }

    private EndCinematicVariant ResolveVariant()
    {
        MatchEndSnapshot snapshot = MatchEndRuntimeContext.LatestSnapshot;
        EndCinematicVariant variant = snapshot != null
            ? MatchEndSnapshotEvaluator.ResolveLocalVariant(snapshot)
            : fallbackVariant;

        if (debugLogs)
        {
            Debug.Log($"[EndMatchTimelineRouter] Variant resuelta: {variant}");
        }

        return variant;
    }

    private PlayableDirector GetVariantDirector(EndCinematicVariant variant)
    {
        switch (variant)
        {
            case EndCinematicVariant.BossWithKills:
                return bossWithKillsDirector;
            case EndCinematicVariant.BossWithoutKills:
                return bossWithoutKillsDirector;
            case EndCinematicVariant.SurvivorsEscaped:
                return survivorsEscapedDirector;
            default:
                return null;
        }
    }

    private float ResolveWaitTime(PlayableDirector variantDirector)
    {
        if (showResultsAfterSeconds >= 0f)
            return showResultsAfterSeconds;

        double duration = 0d;
        if (variantDirector != null && variantDirector.playableAsset != null)
        {
            duration = variantDirector.playableAsset.duration;
        }
        else if (globalDirector != null && globalDirector.playableAsset != null)
        {
            duration = globalDirector.playableAsset.duration;
        }

        return Mathf.Max(0f, (float)duration);
    }

    private void PlayDirector(PlayableDirector director, string label)
    {
        if (director == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[EndMatchTimelineRouter] Director '{label}' no asignado.");
            }
            return;
        }

        director.Stop();
        director.time = 0d;
        director.Evaluate();
        director.Play();

        if (debugLogs)
        {
            Debug.Log($"[EndMatchTimelineRouter] Reproduciendo director '{label}'.");
        }
    }
}