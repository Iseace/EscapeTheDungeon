using System.Text;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class EndMatchResultsHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private TMP_Text survivorsEscapedCountText;
    [SerializeField] private TMP_Text survivorsDefeatedCountText;
    [SerializeField] private TMP_Text totalPlayersCountText;
    [SerializeField] private TMP_Text bossOutcomeText;

    [Header("Table")]
    [SerializeField] private Transform tableRowsContainer;
    [SerializeField] private EndMatchResultsRowUI tableRowPrefab;
    [SerializeField] private bool clearRowsOnRender = true;

    [Header("Skin Names")]
    [Tooltip("Opcional: nombre legible por indice de skin. Si no existe, se muestra Skin #index.")]
    [SerializeField] private string[] skinNamesByIndex;

    [Header("Labels")]
    [SerializeField] private string escapedLabel = "Escaped";
    [SerializeField] private string killedLabel = "Killed";
    [SerializeField] private string trappedLabel = "Didn't Escape";
    [SerializeField] private string bossLabel = "Boss";

    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    private void Start()
    {
        RenderFromSnapshot();
    }

    public void RenderFromSnapshot()
    {
        MatchEndSnapshot snapshot = MatchEndRuntimeContext.LatestSnapshot;
        if (snapshot == null)
        {
            WriteSafe(titleText, "Match Ended");
            WriteSafe(summaryText, "No snapshot available");
            WriteSafe(playersText, "");
            return;
        }

        EndCinematicVariant variant = MatchEndSnapshotEvaluator.ResolveLocalVariant(snapshot);

        WriteSafe(titleText, BuildTitle(variant));
        WriteSafe(summaryText, BuildSummary(snapshot, variant));
        WriteSafe(survivorsEscapedCountText, snapshot.SurvivorsEscaped.ToString());
        WriteSafe(survivorsDefeatedCountText, snapshot.SurvivorsDefeated.ToString());
        WriteSafe(totalPlayersCountText, snapshot.Players.Count.ToString());
        WriteSafe(bossOutcomeText, snapshot.BossKilledAny ? "Boss won (partial)" : "Boss got no kills");

        bool renderedTable = RenderTable(snapshot);
        if (playersText != null)
        {
            playersText.gameObject.SetActive(!renderedTable);
        }

        WriteSafe(playersText, BuildPlayersList(snapshot));
    }

    private string BuildTitle(EndCinematicVariant variant)
    {
        switch (variant)
        {
            case EndCinematicVariant.SurvivorsEscaped:
                return "Victory";
            case EndCinematicVariant.BossWithKills:
                return "Partial Victory";
            case EndCinematicVariant.BossWithoutKills:
                return "Boss Defeated";
            default:
                return "Defeat";
        }
    }

    private static string BuildSummary(MatchEndSnapshot snapshot, EndCinematicVariant variant)
    {
        StringBuilder sb = new StringBuilder(128);
                sb.Append("Reason: ").Append(snapshot.Reason).Append('\n');
                sb.Append("Variant: ").Append(variant).Append('\n');
        sb.Append("Survivors: ")
          .Append(snapshot.SurvivorsEscaped)
          .Append("/")
          .Append(snapshot.SurvivorsTotal)
                    .Append(" escaped");
        return sb.ToString();
    }

    private string BuildPlayersList(MatchEndSnapshot snapshot)
    {
        StringBuilder sb = new StringBuilder(256);

        List<PlayerMatchResult> ordered = BuildOrderedPlayers(snapshot);
        for (int i = 0; i < ordered.Count; i++)
        {
            PlayerMatchResult player = ordered[i];
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

    private bool RenderTable(MatchEndSnapshot snapshot)
    {
        if (tableRowsContainer == null || tableRowPrefab == null)
            return false;

        if (clearRowsOnRender)
            ClearRows();

        List<PlayerMatchResult> ordered = BuildOrderedPlayers(snapshot);
        for (int i = 0; i < ordered.Count; i++)
        {
            PlayerMatchResult player = ordered[i];
            if (player == null) continue;

            EndMatchResultsRowUI row = Instantiate(tableRowPrefab, tableRowsContainer);
            bool isLocal = player.PlayerId == snapshot.LocalPlayerId;

            string playerName = string.IsNullOrWhiteSpace(player.Nickname)
                ? $"Player {player.PlayerId}"
                : player.Nickname;

            string role = player.IsBoss ? bossLabel : "Survivor";
            string result = player.IsBoss ? (snapshot.BossKilledAny ? "Won (partial)" : "Failed") : GetStateLabel(player.EndState);
            string detail = BuildDetailText(player, snapshot);
            string skin = BuildSkinText(player);

            row.Configure(playerName, role, result, detail, skin, isLocal, i);
            spawnedRows.Add(row.gameObject);
        }

        return true;
    }

    private List<PlayerMatchResult> BuildOrderedPlayers(MatchEndSnapshot snapshot)
    {
        List<PlayerMatchResult> ordered = new List<PlayerMatchResult>(snapshot.Players);
        ordered.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int roleCmp = SortRoleOrder(a).CompareTo(SortRoleOrder(b));
            if (roleCmp != 0) return roleCmp;

            int stateCmp = SortStateOrder(a).CompareTo(SortStateOrder(b));
            if (stateCmp != 0) return stateCmp;

            return a.PlayerId.CompareTo(b.PlayerId);
        });

        return ordered;
    }

    private int SortRoleOrder(PlayerMatchResult player)
    {
        return player.IsBoss ? 0 : 1;
    }

    private int SortStateOrder(PlayerMatchResult player)
    {
        if (player.IsBoss) return 0;

        switch (player.EndState)
        {
            case PlayerEndState.Escaped:
                return 1;
            case PlayerEndState.KilledByBoss:
                return 2;
            default:
                return 3;
        }
    }

    private string BuildDetailText(PlayerMatchResult player, MatchEndSnapshot snapshot)
    {
        if (player.IsBoss)
            return snapshot.BossKilledAny ? "Defeated at least one survivor" : "No survivor defeats";

        switch (player.EndState)
        {
            case PlayerEndState.Escaped:
                return "Reached the portal";
            case PlayerEndState.KilledByBoss:
                return "Killed by the boss";
            default:
                return "Trapped when the escape closed";
        }
    }

    private string BuildSkinText(PlayerMatchResult player)
    {
        if (player.IsBoss)
            return bossLabel;

        int index = player.SelectedCharacterIndex;
        if (skinNamesByIndex != null && index >= 0 && index < skinNamesByIndex.Length)
        {
            string skinName = skinNamesByIndex[index];
            if (!string.IsNullOrWhiteSpace(skinName))
                return skinName;
        }

        return $"Skin #{index}";
    }

    private void ClearRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            GameObject row = spawnedRows[i];
            if (row == null) continue;
            Destroy(row);
        }

        spawnedRows.Clear();

        if (tableRowsContainer == null) return;
        for (int i = tableRowsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = tableRowsContainer.GetChild(i);
            if (child == null) continue;
            Destroy(child.gameObject);
        }
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