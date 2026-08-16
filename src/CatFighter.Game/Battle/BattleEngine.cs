using CatFighter.Game.AI;
using CatFighter.Game.Commands;
using CatFighter.Game.Effects;
using CatFighter.Game.Models;

namespace CatFighter.Game.Battle;

public class BattleEngine
{
    private readonly BattleContext _context;
    private readonly SimpleAiPlayer _ai = new();

    public BattleEngine(BattleContext context)
    {
        _context = context;
    }

    public BattleContext Context => _context;

    public void Start()
    {
        _context.State = BattleState.BattleStart;
        _context.TurnCounter = 0;
        _context.Log($"Battle begins: {_context.Player.Name} vs {_context.Opponent.Name}!");
        _context.State = DetermineFirstTurn();
    }

    public IReadOnlyList<IBattleCommand> GetAvailableActions()
    {
        var cat = _context.State == BattleState.PlayerTurn ? _context.Player : _context.Opponent;
        var enemy = _context.State == BattleState.PlayerTurn ? _context.Opponent : _context.Player;

        return cat.Spells
            .Select(s => (IBattleCommand)new CastSpellCommand(s))
            .Where(cmd => cmd.CanExecute(cat, enemy, _context))
            .ToList();
    }

    public void ExecutePlayerAction(IBattleCommand command)
    {
        if (_context.State != BattleState.PlayerTurn) return;
        command.Execute(_context.Player, _context.Opponent, _context);
        EndTurn(_context.Player);
    }

    public void ExecuteOpponentTurn()
    {
        if (_context.State != BattleState.OpponentTurn) return;
        var command = _ai.ChooseAction(_context.Opponent, _context.Player, _context);
        command.Execute(_context.Opponent, _context.Player, _context);
        EndTurn(_context.Opponent);
    }

    private void EndTurn(CatCharacter activeCat)
    {
        _context.State = BattleState.Resolution;
        EndOfTurnProcessor.Process(activeCat, _context);

        if (!_context.Player.IsAlive)
        {
            _context.Winner = _context.Opponent;
            _context.State = BattleState.BattleEnd;
            _context.Log($"{_context.Opponent.Name} wins!");
            return;
        }

        if (!_context.Opponent.IsAlive)
        {
            _context.Winner = _context.Player;
            _context.State = BattleState.BattleEnd;
            _context.Log($"{_context.Player.Name} wins!");
            return;
        }

        _context.TurnCounter++;
        _context.State = activeCat == _context.Player ? BattleState.OpponentTurn : BattleState.PlayerTurn;
    }

    private BattleState DetermineFirstTurn()
    {
        return _context.Player.Speed >= _context.Opponent.Speed
            ? BattleState.PlayerTurn
            : BattleState.OpponentTurn;
    }
}
