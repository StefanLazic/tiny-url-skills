using CatFighter.Game.Battle;
using CatFighter.Game.Commands;
using CatFighter.Game.Data;
using CatFighter.Game.Models;

var spellsPath = Path.Combine(AppContext.BaseDirectory, "Data", "spells.json");
List<SpellDefinition> allSpells;

if (File.Exists(spellsPath))
{
    allSpells = SpellLoader.LoadFromFile(spellsPath);
}
else
{
    // Fallback: load from embedded resource path relative to project
    var altPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "CatFighter.Game", "Data", "spells.json");
    if (File.Exists(altPath))
        allSpells = SpellLoader.LoadFromFile(altPath);
    else
        allSpells = GetDefaultSpells();
}

var playerSpells = allSpells.Take(5).ToList();
var opponentSpells = allSpells.Take(5).ToList();

var player = new CatCharacter("Whiskers", maxHp: 100, maxMana: 50, speed: 10, playerSpells);
var opponent = new CatCharacter("Shadow Paw", maxHp: 90, maxMana: 55, speed: 12, opponentSpells);

var context = new BattleContext(player, opponent);
var engine = new BattleEngine(context);

Console.WriteLine("=== CAT SPELL FIGHTER ===");
Console.WriteLine($"{player.Name} (HP:{player.Hp} MP:{player.Mana} SPD:{player.Speed})");
Console.WriteLine($"  vs");
Console.WriteLine($"{opponent.Name} (HP:{opponent.Hp} MP:{opponent.Mana} SPD:{opponent.Speed})");
Console.WriteLine();

engine.Start();
PrintLog();

while (context.State != BattleState.BattleEnd)
{
    if (context.State == BattleState.PlayerTurn)
    {
        Console.WriteLine($"\n--- {player.Name}'s Turn (HP:{player.Hp} MP:{player.Mana}) ---");
        var actions = engine.GetAvailableActions();
        for (int i = 0; i < actions.Count; i++)
            Console.WriteLine($"  [{i + 1}] {actions[i].Description}");

        int choice = -1;
        while (choice < 0 || choice >= actions.Count)
        {
            Console.Write("Choose action: ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var parsed))
                choice = parsed - 1;
        }

        engine.ExecutePlayerAction(actions[choice]);
        PrintLog();
    }
    else if (context.State == BattleState.OpponentTurn)
    {
        Console.WriteLine($"\n--- {opponent.Name}'s Turn (HP:{opponent.Hp} MP:{opponent.Mana}) ---");
        engine.ExecuteOpponentTurn();
        PrintLog();
    }
}

Console.WriteLine($"\n=== BATTLE OVER === Winner: {context.Winner?.Name ?? "None"} ===");

void PrintLog()
{
    foreach (var entry in context.EventLog.TakeLast(5))
        Console.WriteLine(entry);
}

static List<SpellDefinition> GetDefaultSpells() => new()
{
    new("Scratch", 0, TargetType.Enemy, ElementType.Normal, 0, new List<EffectDefinition> { new(EffectType.Damage, 8) }),
    new("Fireball", 15, TargetType.Enemy, ElementType.Fire, 1, new List<EffectDefinition> { new(EffectType.Damage, 25) }),
    new("Heal", 12, TargetType.Self, ElementType.Normal, 2, new List<EffectDefinition> { new(EffectType.Heal, 20) }),
    new("Shadow Strike", 18, TargetType.Enemy, ElementType.Shadow, 2, new List<EffectDefinition> { new(EffectType.Damage, 18), new(EffectType.Poison, 5, 3) }),
    new("Shield Fur", 8, TargetType.Self, ElementType.Normal, 3, new List<EffectDefinition> { new(EffectType.Shield, 15) }),
};
