using UnityEngine;

public class KartActiveModelAnimatorBridge : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool driveAnimatorParameters = false;

    [Header("Animator Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string rideBoolParameter = "IsRidingBroom";

    [Header("Speed Tuning")]
    [SerializeField] private float speedNormalization = 8f;

    private KartEntity _entity;
    private KartController _controller;
    private Animator _activeAnimator;
    private GameObject _activeModel;
    private bool _warnedMissingController;
    private bool _warnedMissingSpeedParameter;
    private bool _warnedMissingRideParameter;

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

        if (!HasPlayableAnimator(_activeAnimator))
        {
            if (!_warnedMissingController)
            {
                string modelName = _activeModel != null ? _activeModel.name : "UnknownModel";
                Debug.LogWarning($"[KART ANIM] Animator on '{modelName}' has no Animator Controller. Assign one in the model/prefab Animator component.", _activeAnimator);
                _warnedMissingController = true;
            }

            return;
        }

        if (!driveAnimatorParameters)
        {
            return;
        }

        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(_controller.AppliedSpeed) / Mathf.Max(0.01f, speedNormalization));

        if (HasParameter(_activeAnimator, speedParameter, AnimatorControllerParameterType.Float))
        {
            _activeAnimator.SetFloat(speedParameter, normalizedSpeed);
        }
        else if (!_warnedMissingSpeedParameter)
        {
            Debug.LogWarning($"[KART ANIM] Missing float parameter '{speedParameter}' on animator '{_activeAnimator.name}'.", _activeAnimator);
            _warnedMissingSpeedParameter = true;
        }

        if (HasParameter(_activeAnimator, rideBoolParameter, AnimatorControllerParameterType.Bool))
        {
            _activeAnimator.SetBool(rideBoolParameter, true);
        }
        else if (!_warnedMissingRideParameter)
        {
            Debug.LogWarning($"[KART ANIM] Missing bool parameter '{rideBoolParameter}' on animator '{_activeAnimator.name}'.", _activeAnimator);
            _warnedMissingRideParameter = true;
        }
    }

    private void BindAnimator(GameObject model)
    {
        if (driveAnimatorParameters && _activeAnimator != null && HasPlayableAnimator(_activeAnimator))
        {
            if (HasParameter(_activeAnimator, rideBoolParameter, AnimatorControllerParameterType.Bool))
            {
                _activeAnimator.SetBool(rideBoolParameter, false);
            }
        }

        _activeModel = model;
        _activeAnimator = null;
        _warnedMissingController = false;
        _warnedMissingSpeedParameter = false;
        _warnedMissingRideParameter = false;

        if (_activeModel == null)
            return;

        _activeAnimator = _activeModel.GetComponentInChildren<Animator>(true);
        if (_activeAnimator != null)
        {
            // Physics drives the kart transform; root motion can cause flips/drift.
            _activeAnimator.applyRootMotion = false;

            if (driveAnimatorParameters && HasPlayableAnimator(_activeAnimator))
            {
                if (HasParameter(_activeAnimator, rideBoolParameter, AnimatorControllerParameterType.Bool))
                {
                    _activeAnimator.SetBool(rideBoolParameter, true);
                }
            }
        }
    }

    private static bool HasPlayableAnimator(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        return animator.runtimeAnimatorController != null || animator.hasBoundPlayables;
    }

    private static bool HasParameter(Animator animator, string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == expectedType)
            {
                return true;
            }
        }

        return false;
    }
}
