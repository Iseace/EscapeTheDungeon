using Fusion;
using UnityEngine;

public class BossSpecial : NetworkBehaviour
{
    [Header("Invisibility Settings")]
    [Tooltip("Speed multiplier while invisible (e.g. 1.5 = 50% faster)")]
    public float invisibleSpeedMultiplier = 1.5f;

    [Tooltip("Cooldown in seconds before toggling invisibility again")]
    public float toggleCooldown = 1.5f;

    [Tooltip("Alpha value for the local boss player while invisible (others see nothing)")]
    [Range(0f, 1f)]
    public float localGhostAlpha = 0.3f;

    [Networked] public NetworkBool IsInvisible { get; set; }
    [Networked] private TickTimer CooldownTimer { get; set; }

    private Renderer[] _renderers;
    private Material[][] _originalMaterials;
    private Material[][] _ghostMaterials;
    private bool _lastInvisibleState;

    // Reference to the sound component on this prefab
    private BossSounds _bossSounds;

    public override void Spawned()
    {
        // Cache all renderers under this object for toggling visibility
        _renderers = GetComponentsInChildren<Renderer>(true);
        CacheAndPrepareGhostMaterials();
        _lastInvisibleState = IsInvisible;
        ApplyVisuals(IsInvisible);

        // Grab the sound component
        _bossSounds = GetComponent<BossSounds>();
    }

    /// <summary>
    /// Reads the Special input and toggles invisibility.
    /// Runs on the input authority (the boss player).
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;

        if (DungeonNetworkRunner.Instance != null && DungeonNetworkRunner.Instance.IsBossFrozen) return;

        if (GetInput(out PlayerInputData data) && data.SpecialPressed)
        {
            if (CooldownTimer.ExpiredOrNotRunning(Runner))
            {
                Rpc_ToggleInvisibility(!IsInvisible);
            }
        }
    }

    public override void Render()
    {
        // Detect networked state change and update visuals on every client
        if (_lastInvisibleState != IsInvisible)
        {
            _lastInvisibleState = IsInvisible;
            ApplyVisuals(IsInvisible);
        }
    }

    // ----- RPCs -----

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_ToggleInvisibility(NetworkBool wantInvisible)
    {
        if (!CooldownTimer.ExpiredOrNotRunning(Runner)) return;

        IsInvisible = wantInvisible;
        CooldownTimer = TickTimer.CreateFromSeconds(Runner, toggleCooldown);

        // Play sound here — skill has actually activated on StateAuthority
        if (_bossSounds != null)
            _bossSounds.OnInvisibilityToggle();
    }

    // ----- Visuals -----

    private void CacheAndPrepareGhostMaterials()
    {
        _originalMaterials = new Material[_renderers.Length][];
        _ghostMaterials = new Material[_renderers.Length][];

        for (int i = 0; i < _renderers.Length; i++)
        {
            _originalMaterials[i] = _renderers[i].materials; // cloned array

            // Build ghost (semi-transparent) copies for the local player
            _ghostMaterials[i] = new Material[_originalMaterials[i].Length];
            for (int j = 0; j < _originalMaterials[i].Length; j++)
            {
                Material ghost = new Material(_originalMaterials[i][j]);
                SetMaterialTransparent(ghost, localGhostAlpha);
                _ghostMaterials[i][j] = ghost;
            }
        }
    }

    private void ApplyVisuals(bool invisible)
    {
        if (_renderers == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            if (!invisible)
            {
                // Fully visible for everyone
                _renderers[i].enabled = true;
                _renderers[i].materials = _originalMaterials[i];
            }
            else if (HasInputAuthority)
            {
                // Local boss: semi-transparent ghost look
                _renderers[i].enabled = true;
                _renderers[i].materials = _ghostMaterials[i];
            }
            else
            {
                // Other clients: completely hidden
                _renderers[i].enabled = false;
            }
        }
    }

    private static void SetMaterialTransparent(Material mat, float alpha)
    {
        // Works with Standard, URP Lit (Shader Graph) and most built-in shaders
        Color c = mat.color;
        c.a = alpha;
        mat.color = c;

        // Standard shader transparency keywords
        mat.SetFloat("_Mode", 3); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        // URP Lit support
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // Alpha blend
        }
    }

    // ----- Public API for other scripts -----

    /// <summary>Returns the speed multiplier based on invisibility state.</summary>
    public float GetSpeedMultiplier() => IsInvisible ? invisibleSpeedMultiplier : 1f;

    /// <summary>Returns true when the boss cannot attack (while invisible).</summary>
    public bool IsAttackBlocked() => IsInvisible;
}