using Fusion;
using System.Collections.Generic;
using UnityEngine;

public static class MatchEndSnapshotBuilder
{
    public static MatchEndSnapshot CaptureFromRunner(NetworkRunner runner, MatchEndReason reason)
    {
        MatchEndSnapshot snapshot = new MatchEndSnapshot
        {
            Reason = reason,
            LocalPlayerId = runner != null ? runner.LocalPlayer.PlayerId : -1
        };

        if (runner == null)
            return snapshot;

        foreach (PlayerRef playerRef in runner.ActivePlayers)
        {
            PlayerMatchResult result = BuildPlayerResult(runner, playerRef);
            if (result == null)
                continue;

            snapshot.Players.Add(result);

            if (result.IsBoss)
                continue;

            snapshot.SurvivorsTotal++;
            if (result.EndState == PlayerEndState.Escaped)
                snapshot.SurvivorsEscaped++;
            else
                snapshot.SurvivorsDefeated++;
        }

        snapshot.BossKilledAny = snapshot.SurvivorsDefeated > 0;
        return snapshot;
    }

    private static PlayerMatchResult BuildPlayerResult(NetworkRunner runner, PlayerRef playerRef)
    {
        PlayerMatchResult result = new PlayerMatchResult
        {
            PlayerId = playerRef.PlayerId,
            Nickname = "",
            EndState = PlayerEndState.TrappedNoEscape
        };

        if (!runner.TryGetPlayerObject(playerRef, out NetworkObject playerObject) || playerObject == null)
        {
            result.Disconnected = true;
            return result;
        }

        PlayerSetup setup = playerObject.GetComponent<PlayerSetup>();
        if (setup == null)
        {
            result.Disconnected = true;
            return result;
        }

        result.Nickname = setup.Nickname.ToString();
        result.IsBoss = setup.IsBossPlayer();
        result.SelectedCharacterIndex = setup.SelectedCharacterIndex;
        result.HasEscaped = setup.HasEscapedSafe;

        bool isDead = false;
        if (playerObject.TryGetComponent<PlayerHealth>(out PlayerHealth health))
        {
            isDead = health.IsDeadSafe;
        }

        result.IsDead = isDead;

        if (result.IsBoss)
        {
            result.EndState = PlayerEndState.TrappedNoEscape;
            return result;
        }

        if (result.HasEscaped)
        {
            result.EndState = PlayerEndState.Escaped;
        }
        else if (result.IsDead)
        {
            result.EndState = PlayerEndState.KilledByBoss;
        }
        else
        {
            result.EndState = PlayerEndState.TrappedNoEscape;
        }

        return result;
    }
}