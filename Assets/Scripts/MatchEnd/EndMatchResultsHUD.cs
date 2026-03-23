using System.Text;
using TMPro;
using UnityEngine;

public class EndMatchResultsHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text playersText;

    [Header("Labels")]
    [SerializeField] private string escapedLabel = "Escapo";
    [SerializeField] private string killedLabel = "Asesinado";
    [SerializeField] private string trappedLabel = "No escapo";
    [SerializeField] private string bossLabel = "Boss";

    private void Start()
    {
        RenderFromSnapshot();
    }

    public void RenderFromSnapshot()
    {
        MatchEndSnapshot snapshot = MatchEndRuntimeContext.LatestSnapshot;
        if (snapshot == null)
        {
            WriteSafe(titleText, "Fin de partida");
            WriteSafe(summaryText, "Sin snapshot disponible");
            WriteSafe(playersText, "");
            return;
        }

        EndCinematicVariant variant = MatchEndSnapshotEvaluator.ResolveLocalVariant(snapshot);

        WriteSafe(titleText, BuildTitle(variant));
        WriteSafe(summaryText, BuildSummary(snapshot, variant));
        WriteSafe(playersText, BuildPlayersList(snapshot));
    }

    private string BuildTitle(EndCinematicVariant variant)
    {
        switch (variant)
        {
            case EndCinematicVariant.SurvivorsEscaped:
                return "Victoria";
            case EndCinematicVariant.BossWithoutKills:
                return "Boss Derrotado";
            default:
                return "Derrota";
        }
    }

    private static string BuildSummary(MatchEndSnapshot snapshot, EndCinematicVariant variant)
    {
        StringBuilder sb = new StringBuilder(128);
        sb.Append("Motivo: ").Append(snapshot.Reason).Append('\n');
        sb.Append("Variante: ").Append(variant).Append('\n');
        sb.Append("Survivors: ")
          .Append(snapshot.SurvivorsEscaped)
          .Append("/")
          .Append(snapshot.SurvivorsTotal)
          .Append(" escaparon");
        return sb.ToString();
    }

    private string BuildPlayersList(MatchEndSnapshot snapshot)
    {
        StringBuilder sb = new StringBuilder(256);

        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            PlayerMatchResult player = snapshot.Players[i];
            if (player == null) continue;

            if (sb.Length > 0)
                sb.Append('\n');

            string name = string.IsNullOrWhiteSpace(player.Nickname)
                ? $"Player {player.PlayerId}"
                : player.Nickname;

            sb.Append(name).Append(" - ");

            if (player.IsBoss)
            {
                sb.Append(bossLabel);
                continue;
            }

            sb.Append(GetStateLabel(player.EndState));
        }

        return sb.ToString();
    }

    private string GetStateLabel(PlayerEndState state)
    {
        switch (state)
        {
            case PlayerEndState.Escaped:
                return escapedLabel;
            case PlayerEndState.KilledByBoss:
                return killedLabel;
            default:
                return trappedLabel;
        }
    }

    private static void WriteSafe(TMP_Text text, string value)
    {
        if (text == null) return;
        text.text = value;
    }
}