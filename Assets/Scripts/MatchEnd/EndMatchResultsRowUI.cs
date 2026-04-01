using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndMatchResultsRowUI : MonoBehaviour
{
    [Header("Columns")]
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text skinText;

    [Header("Visual")]
    [SerializeField] private Graphic rowBackground;
    [SerializeField] private Color evenColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color oddColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color localPlayerColor = new Color(0.35f, 0.58f, 1f, 0.35f);

    private void Awake()
    {
        AutoBindColumnsIfNeeded();
    }

    private void OnValidate()
    {
        AutoBindColumnsIfNeeded();
    }

    public void SetColumns(TMP_Text player, TMP_Text role, TMP_Text result, TMP_Text detail, TMP_Text skin)
    {
        playerText = player;
        roleText = role;
        resultText = result;
        detailText = detail;
        skinText = skin;
    }

    public void Configure(string player, string role, string result, string detail, string skin, bool isLocalPlayer, int rowIndex)
    {
        AutoBindColumnsIfNeeded();

        WriteSafe(playerText, player);
        WriteSafe(roleText, role);
        WriteSafe(resultText, result);
        WriteSafe(detailText, detail);
        WriteSafe(skinText, skin);

        if (rowBackground != null)
        {
            if (isLocalPlayer)
                rowBackground.color = localPlayerColor;
            else
                rowBackground.color = (rowIndex % 2 == 0) ? evenColor : oddColor;
        }
    }

    private static void WriteSafe(TMP_Text text, string value)
    {
        if (text == null) return;
        text.text = value ?? string.Empty;
    }

    private void AutoBindColumnsIfNeeded()
    {
        if (playerText == null) playerText = FindTextByName("playersText");
        if (roleText == null) roleText = FindTextByName("rolesText");
        if (resultText == null) resultText = FindTextByName("resultText");
        if (detailText == null) detailText = FindTextByName("detailText");
        if (skinText == null) skinText = FindTextByName("skinText");
    }

    private TMP_Text FindTextByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        TMP_Text[] all = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text t = all[i];
            if (t == null) continue;
            if (t.name == objectName) return t;
        }

        return null;
    }
}
