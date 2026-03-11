using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fusion;

public class GlowPlayer : NetworkBehaviour
{
    [Header("Glow Settings")]
    public float glowDuration = 5f;
    public float cooldown = 15f;

    private float _cooldownEndTime;
    private bool _isGlowing;
    private bool _isLocalBoss;

    // Tracked indicators so we can turn them off later
    private List<GlowIndicator> _activeIndicators = new List<GlowIndicator>();

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
        {
            enabled = false;
            return;
        }
        _isLocalBoss = true;
        Debug.Log("[GlowPlayer] Spawned — I am the local boss");
    }

    public override void FixedUpdateNetwork()
    {
        if (!_isLocalBoss) return;

        if (GetInput(out PlayerInputData data) && data.InteractPressed)
        {
            Debug.Log($"[GlowPlayer] Interact pressed! isGlowing={_isGlowing}, cooldownOK={Time.time >= _cooldownEndTime}");
            if (!_isGlowing && Time.time >= _cooldownEndTime)
            {
                StartCoroutine(ActivateGlow());
            }
        }
    }

    private IEnumerator ActivateGlow()
    {
        Debug.Log("Glow Activado");
        _isGlowing = true;
        ShowIndicators();

        yield return new WaitForSeconds(glowDuration);

        HideIndicators();
        _isGlowing = false;
        _cooldownEndTime = Time.time + cooldown;
        Debug.Log("Glow Finalizado");
    }

    private void ShowIndicators()
    {
        _activeIndicators.Clear();

        // Debug: list ALL networked objects in scene
        var allNetObjs = FindObjectsByType<Fusion.NetworkObject>(FindObjectsSortMode.None);
        Debug.Log($"[GlowPlayer] Total NetworkObjects in scene: {allNetObjs.Length}");
        foreach (var no in allNetObjs)
            Debug.Log($"[GlowPlayer]   NetworkObj: {no.gameObject.name}");

        PlayerRole[] allPlayers = FindObjectsByType<PlayerRole>(FindObjectsSortMode.None);
        Debug.Log($"[GlowPlayer] Found {allPlayers.Length} PlayerRole objects in scene");

        foreach (var player in allPlayers)
        {
            Debug.Log($"[GlowPlayer]   -> {player.gameObject.name} | IsBoss={player.IsBoss}");
            if (player.IsBoss) continue;

            GlowIndicator indicator = player.GetComponentInChildren<GlowIndicator>(true);
            if (indicator != null)
            {
                Debug.Log($"[GlowPlayer]   -> Found GlowIndicator on {player.gameObject.name}, showing it");
                indicator.Show();
                _activeIndicators.Add(indicator);
            }
            else
            {
                Debug.LogWarning($"[GlowPlayer]   -> NO GlowIndicator found on {player.gameObject.name}!");
            }
        }

        Debug.Log($"[GlowPlayer] Activated {_activeIndicators.Count} indicators");
    }

    private void HideIndicators()
    {
        foreach (var indicator in _activeIndicators)
        {
            if (indicator != null)
                indicator.Hide();
        }
        _activeIndicators.Clear();
    }
}