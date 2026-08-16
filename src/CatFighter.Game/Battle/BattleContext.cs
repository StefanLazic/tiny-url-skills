using CatFighter.Game.Models;

namespace CatFighter.Game.Battle;

public class BattleContext
{
    public CatCharacter Player { get; }
    public CatCharacter Opponent { get; }
    public int TurnCounter { get; set; }
    public BattleState State { get; set; } = BattleState.Lobby;
    public List<string> EventLog { get; } = new();
    public CatCharacter? Winner { get; set; }

    public BattleContext(CatCharacter player, CatCharacter opponent)
    {
        Player = player;
        Opponent = opponent;
    }

    public void Log(string message)
    {
        EventLog.Add($"[Turn {TurnCounter}] {message}");
    }
}
