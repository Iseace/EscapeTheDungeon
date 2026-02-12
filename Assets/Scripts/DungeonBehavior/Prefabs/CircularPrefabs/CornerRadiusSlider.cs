using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CornerRadiusSlider : MonoBehaviour
{
    [SerializeField] private Slider radiusSlider;
    [SerializeField] private Circularfloor circularFloor;
    [SerializeField] private TextMeshProUGUI radiusValueText;
    
    [Header("Slider Settings")]
    [SerializeField] private float minRadius = 0f;
    [SerializeField] private float maxRadius = 5f;

    private void Start()
    {
        // Setup slider
        if (radiusSlider != null)
        {
            radiusSlider.minValue = minRadius;
            radiusSlider.maxValue = maxRadius;
            radiusSlider.onValueChanged.AddListener(OnRadiusChanged);
            
            // Set initial value if circularFloor exists
            if (circularFloor != null)
            {
                radiusSlider.value = circularFloor.cornerRadius;
            }
        }
    }

    private void OnRadiusChanged(float newValue)
    {
        if (circularFloor != null)
        {
            circularFloor.SetCornerRadius(newValue);
        }

        // Update text display if available
        if (radiusValueText != null)
        {
            radiusValueText.text = newValue.ToString("F2");
        }
    }

    public void SetMaxRadius(float newMax)
    {
        maxRadius = newMax;
        if (radiusSlider != null)
        {
            radiusSlider.maxValue = maxRadius;
        }
    }
}
