using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalDirectionHUD : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private bool showOnlyWhenEscapeWindowOpen = true;
    [SerializeField] private bool restrictToSceneName = false;
    [SerializeField] private string requiredSceneName = "Game";

    [Header("Scene UI")]
    [SerializeField] private string panelObjectName = "EscapeAlert";
    [SerializeField] private string textObjectName = "PortalDistanceText";
    [SerializeField] private bool hidePanelWhenUnavailable = true;
    [SerializeField] private string distanceFormat = "Exit {0}m";

    private MissionObjectiveManager missionManager;
    private Transform portalTransform;
    private Camera targetCamera;
    private GameObject panelObject;
    private TMP_Text distanceText;

    private void Awake()
    {
        targetCamera = Camera.main;
    }

    private void Update()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (missionManager == null)
            missionManager = MissionObjectiveManager.Instance;

        ResolveSceneUi();

        if (portalTransform == null)
        {
            MissionPortal portal = FindFirstObjectByType<MissionPortal>();
            if (portal != null)
                portalTransform = portal.transform;
        }

        RefreshUi();
    }

    private void ResolveSceneUi()
    {
        if (panelObject == null && !string.IsNullOrWhiteSpace(panelObjectName))
        {
            panelObject = FindSceneObjectByName(panelObjectName);
        }

        if (distanceText == null)
        {
            if (!string.IsNullOrWhiteSpace(textObjectName))
            {
                GameObject textObject = FindSceneObjectByName(textObjectName);
                if (textObject != null)
                {
                    distanceText = textObject.GetComponent<TMP_Text>();
                }
            }

            if (distanceText == null && panelObject != null)
            {
                distanceText = panelObject.GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private void RefreshUi()
    {
        bool shouldShow = ShouldShowDistance();

        if (panelObject != null && hidePanelWhenUnavailable)
        {
            panelObject.SetActive(shouldShow);
        }

        if (!shouldShow || distanceText == null || targetCamera == null || portalTransform == null) return;

        float distance = Vector3.Distance(targetCamera.transform.position, portalTransform.position);
        distanceText.text = string.Format(distanceFormat, Mathf.RoundToInt(distance));
    }

    private bool ShouldShowDistance()
    {
        if (!enabled) return false;

        if (restrictToSceneName && !string.IsNullOrWhiteSpace(requiredSceneName))
        {
            if (SceneManager.GetActiveScene().name != requiredSceneName)
                return false;
        }

        if (portalTransform == null) return false;
        if (targetCamera == null) return false;
        if (showOnlyWhenEscapeWindowOpen && missionManager != null && !missionManager.IsEscapeWindowOpen) return false;

        return true;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null) continue;
            if (current.gameObject.scene != gameObject.scene) continue;
            if (!string.Equals(current.name, objectName, System.StringComparison.Ordinal)) continue;

            return current.gameObject;
        }

        return null;
    }

    private void OnDisable()
    {
        if (panelObject != null && hidePanelWhenUnavailable)
        {
            panelObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (panelObject != null && hidePanelWhenUnavailable)
        {
            panelObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(distanceFormat))
        {
            distanceFormat = "PORTAL {0}m";
        }

        if (!distanceFormat.Contains("{0}"))
        {
            distanceFormat += " {0}";
        }
    }
}