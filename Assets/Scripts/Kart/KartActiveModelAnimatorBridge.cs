using UnityEngine;

public class KartActiveModelAnimatorBridge : MonoBehaviour
{
    [Header("Animator Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string rideBoolParameter = "IsRidingBroom";

    [Header("Speed Tuning")]
    [SerializeField] private float speedNormalization = 8f;

    private KartEntity _entity;
    private KartController _controller;
    private Animator _activeAnimator;
    private GameObject _activeModel;

    private void Awake()
    {
        _entity = GetComponent<KartEntity>();
        _controller = GetComponent<KartController>();
    }

    private void Update()
    {
        if (_entity == null || _controller == null)
            return;

        if (!_entity.IsNetworkReady || !_controller.IsNetworkReady)
            return;

        var model = _entity.GetActiveCharacterModel();
        if (model != _activeModel)
        {
            BindAnimator(model);
        }

        if (_activeAnimator == null)
            return;

        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(_controller.AppliedSpeed) / Mathf.Max(0.01f, speedNormalization));
        _activeAnimator.SetFloat(speedParameter, normalizedSpeed);
        _activeAnimator.SetBool(rideBoolParameter, true);
    }

    private void BindAnimator(GameObject model)
    {
        if (_activeAnimator != null)
        {
            _activeAnimator.SetBool(rideBoolParameter, false);
        }

        _activeModel = model;
        _activeAnimator = null;

        if (_activeModel == null)
            return;

        _activeAnimator = _activeModel.GetComponentInChildren<Animator>(true);
        if (_activeAnimator != null)
        {
            _activeAnimator.SetBool(rideBoolParameter, true);
        }
    }
}
