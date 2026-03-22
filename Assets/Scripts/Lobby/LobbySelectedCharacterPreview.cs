using TMPro;
using UnityEngine;

public class LobbySelectedCharacterPreview : MonoBehaviour
{
    [Header("Model Preview")]
    [SerializeField] private GameObject[] previewModels;
    [SerializeField] private Transform previewParent;
    [SerializeField] private Transform rotationTarget;
    [SerializeField] private float rotationSpeed = 25f;

    [Header("Animation")]
    [SerializeField] private bool forceAnimatorAlwaysAnimate = true;
    [SerializeField] private string idleStateName = "";
    [SerializeField] private float animatorSpeed = 1f;
    [SerializeField] private bool randomizeIdleStartTime = true;
    [SerializeField] private RuntimeAnimatorController sharedPreviewController;
    [SerializeField] private RuntimeAnimatorController[] perModelControllers;
    [SerializeField] private bool autoAddAnimatorIfMissing = true;
    [SerializeField] private bool overrideExistingController = false;

    [Header("UI (Optional)")]
    [SerializeField] private TMP_Text nicknameText;

    private const string CharacterIdKey = "SelectedCharacterID";
    private const string NicknameKey = "Nickname";

    private int _currentIndex = -1;
    private GameObject[] _runtimeModels;

    private void Awake()
    {
        if (previewParent == null)
            previewParent = transform;

        if ((previewModels == null || previewModels.Length == 0) && transform.childCount > 0)
        {
            previewModels = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                previewModels[i] = transform.GetChild(i).gameObject;
            }
        }

        BuildRuntimeModelList();

        if (rotationTarget == null)
            rotationTarget = previewParent;
    }

    private void OnEnable()
    {
        RefreshPreview();
    }

    private void Update()
    {
        if (rotationTarget != null)
        {
            rotationTarget.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }

    public void RefreshPreview()
    {
        if (_runtimeModels == null || _runtimeModels.Length == 0)
        {
            Debug.LogWarning("[LobbyPreview] No preview models assigned.");
            return;
        }

        int savedIndex = PlayerPrefs.GetInt(CharacterIdKey, 0);
        int normalizedIndex = Mathf.Clamp(savedIndex, 0, _runtimeModels.Length - 1);

        for (int i = 0; i < _runtimeModels.Length; i++)
        {
            if (_runtimeModels[i] != null)
            {
                bool active = (i == normalizedIndex);
                _runtimeModels[i].SetActive(active);

                if (active)
                {
                    SetupAnimatorForPreview(_runtimeModels[i], i);
                }
            }
        }

        _currentIndex = normalizedIndex;

        if (nicknameText != null)
        {
            string nick = PlayerPrefs.GetString(NicknameKey, string.Empty).Trim();
            nicknameText.text = string.IsNullOrEmpty(nick) ? "Player" : nick;
        }
    }

    public int GetCurrentIndex()
    {
        return _currentIndex;
    }

    private void BuildRuntimeModelList()
    {
        if (previewModels == null || previewModels.Length == 0)
        {
            _runtimeModels = System.Array.Empty<GameObject>();
            return;
        }

        _runtimeModels = new GameObject[previewModels.Length];

        for (int i = 0; i < previewModels.Length; i++)
        {
            GameObject source = previewModels[i];
            if (source == null) continue;

            if (source.scene.IsValid())
            {
                _runtimeModels[i] = source;
                continue;
            }

            GameObject instance = Instantiate(source, previewParent);
            instance.name = source.name + "_Preview";
            _runtimeModels[i] = instance;
        }
    }

    private void SetupAnimatorForPreview(GameObject modelRoot, int modelIndex)
    {
        if (modelRoot == null) return;

        Animator animator = modelRoot.GetComponentInChildren<Animator>(true);
        if (animator == null && autoAddAnimatorIfMissing)
        {
            animator = modelRoot.AddComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"[LobbyPreview] '{modelRoot.name}' has no Animator. Assign one or enable autoAddAnimatorIfMissing.");
            return;
        }

        RuntimeAnimatorController externalController = ResolveControllerForIndex(modelIndex);
        if (externalController != null && (overrideExistingController || animator.runtimeAnimatorController == null))
        {
            animator.runtimeAnimatorController = externalController;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[LobbyPreview] '{modelRoot.name}' has Animator but no controller assigned.");
            return;
        }

        animator.enabled = true;
        animator.speed = Mathf.Max(0f, animatorSpeed);

        if (forceAnimatorAlwaysAnimate)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (!string.IsNullOrWhiteSpace(idleStateName))
        {
            float normalizedTime = randomizeIdleStartTime ? Random.value : 0f;
            animator.Play(idleStateName, 0, normalizedTime);
            animator.Update(0f);
        }
    }

    private RuntimeAnimatorController ResolveControllerForIndex(int index)
    {
        if (perModelControllers != null && index >= 0 && index < perModelControllers.Length && perModelControllers[index] != null)
        {
            return perModelControllers[index];
        }

        return sharedPreviewController;
    }
}
