using System;
using System.Collections.Generic;

[Serializable]
public enum MatchEndReason
{
    TimeLimitExpired,
    AllSurvivorsEscaped,
    AllSurvivorsDefeated,
    Manual,
    NoActiveSurvivors
}

[Serializable]
public enum PlayerEndState
{
    Escaped,
    KilledByBoss,
    TrappedNoEscape
}

[Serializable]
public enum EndCinematicVariant
{
    BossWithKills,
    BossWithoutKills,
    SurvivorsEscaped
}

[Serializable]
public class PlayerMatchResult
{
    public int PlayerId;
    public string Nickname;
    public bool IsBoss;
    public int SelectedCharacterIndex;
    public bool HasEscaped;
    public bool IsDead;
    public bool Disconnected;
    public PlayerEndState EndState;
}

[Serializable]
public class MatchEndSnapshot
{
    public MatchEndReason Reason;
    public int LocalPlayerId;
    public List<PlayerMatchResult> Players = new List<PlayerMatchResult>();
    public int SurvivorsTotal;
    public int SurvivorsEscaped;
    public int SurvivorsDefeated;
    public bool BossKilledAny;
}

public static class MatchEndRuntimeContext
{
    public static MatchEndSnapshot LatestSnapshot { get; private set; }

    public static void SetSnapshot(MatchEndSnapshot snapshot)
    {
        LatestSnapshot = snapshot;
    }

    public static void Clear()
    {
        LatestSnapshot = null;
    }
}

public static class MatchEndSnapshotEvaluator
{
    public static EndCinematicVariant ResolveLocalVariant(MatchEndSnapshot snapshot)
    {
        if (snapshot == null)
            return EndCinematicVariant.BossWithoutKills;

        PlayerMatchResult local = null;
        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            if (snapshot.Players[i].PlayerId == snapshot.LocalPlayerId)
            {
                local = snapshot.Players[i];
                break;
            }
        }

        if (local != null)
        {
            if (local.IsBoss)
                return snapshot.BossKilledAny ? EndCinematicVariant.BossWithKills : EndCinematicVariant.BossWithoutKills;

            return local.EndState == PlayerEndState.Escaped
                ? EndCinematicVariant.SurvivorsEscaped
                : EndCinematicVariant.BossWithKills;
        }

        if (snapshot.BossKilledAny)
            return EndCinematicVariant.BossWithKills;

        return EndCinematicVariant.SurvivorsEscaped;
    }
}