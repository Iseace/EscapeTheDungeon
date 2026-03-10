using UnityEngine;

/// <summary>
/// Attach this to a child object on the Player prefab (e.g. a Capsule with no collider).
/// Give it a material that uses the "Custom/SilhouetteGlow" shader.
/// The renderer starts disabled; the boss's GlowPlayer script toggles it on/off.
/// </summary>
public class GlowIndicator : MonoBehaviour
{
    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        // Hidden by default – only the boss activates it
        if (_renderer != null)
            _renderer.enabled = false;
    }

    public void Show()
    {
        if (_renderer != null)
            _renderer.enabled = true;
    }

    public void Hide()
    {
        if (_renderer != null)
            _renderer.enabled = false;
    }
}
