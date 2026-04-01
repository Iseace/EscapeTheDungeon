using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class KartInput
{
    public static float Throttle;
    public static float Steer;
}

public class KartMobileInput : MonoBehaviour
{
    [Header("Throttle Buttons")]
    [SerializeField] private GameObject btnForward;
    [SerializeField] private GameObject btnReverse;

    [Header("Steering Buttons")]
    [SerializeField] private GameObject btnLeft;
    [SerializeField] private GameObject btnRight;

    [Header("Settings")]
    [SerializeField] private bool activeInEditor = true;

    public static bool IsActive { get; private set; }

    private void Awake()
    {
        bool isMobile = Application.platform == RuntimePlatform.Android
                     || Application.platform == RuntimePlatform.IPhonePlayer;
                     //|| (Application.isEditor && activeInEditor);

        if (!isMobile) { IsActive = false; gameObject.SetActive(false); return; }

        IsActive = true;

        // Auto-find by name if not assigned in Inspector
        if (btnForward == null) btnForward = GameObject.Find("BtnForward");
        if (btnReverse == null) btnReverse = GameObject.Find("BtnReverse");
        if (btnLeft    == null) btnLeft    = GameObject.Find("BtnLeft");
        if (btnRight   == null) btnRight   = GameObject.Find("BtnRight");

        SetupButton(btnForward, "BtnForward",  throttle:  1f, steer:  0f);
        SetupButton(btnReverse, "BtnReverse",  throttle: -1f, steer:  0f);
        SetupButton(btnLeft,    "BtnLeft",     throttle:  0f, steer: -1f);
        SetupButton(btnRight,   "BtnRight",    throttle:  0f, steer:  1f);
    }

    private void SetupButton(GameObject btn, string name, float throttle, float steer)
    {
        if (btn == null)
        {
            Debug.LogWarning($"[KartMobileInput] '{name}' not found!");
            return;
        }

        var rawImg = btn.GetComponent<RawImage>();
        if (rawImg != null)
        {
            rawImg.raycastTarget = true;
        }
        else
        {
            var img = btn.GetComponent<Image>() ?? btn.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.01f);
            img.raycastTarget = true;
        }

        var handler = btn.GetComponent<KartButton>() ?? btn.AddComponent<KartButton>();
        handler.Init(throttle, steer);

        Debug.Log($"[KartMobileInput] {name} ready (throttle:{throttle} steer:{steer})");
    }

    private void OnDestroy()
    {
        IsActive = false;
        KartInput.Throttle = 0f;
        KartInput.Steer    = 0f;
    }
}

public class KartButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private float _throttle;
    private float _steer;

    public void Init(float throttle, float steer)
    {
        _throttle = throttle;
        _steer    = steer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_throttle != 0f) KartInput.Throttle = _throttle;
        if (_steer    != 0f) KartInput.Steer    = _steer;
        Debug.Log($"[KartButton] Down → Throttle:{KartInput.Throttle} Steer:{KartInput.Steer}");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_throttle != 0f) KartInput.Throttle = 0f;
        if (_steer    != 0f) KartInput.Steer    = 0f;
        Debug.Log($"[KartButton] Up → Throttle:{KartInput.Throttle} Steer:{KartInput.Steer}");
    }
}