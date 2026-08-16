using CatFighter.Game.Battle;
using CatFighter.Game.Commands;
using CatFighter.Game.Data;
using CatFighter.Game.Effects;
using CatFighter.Game.Models;
using Xunit;

namespace CatFighter.Game.Tests;

public class BattleEngineTests
{
    private static CatCharacter MakeCat(string name, int hp = 100, int mana = 50, int speed = 10)
    {
        var spells = new List<SpellDefinition>
        {
            new("Scratch", 0, TargetType.Enemy, ElementType.Normal, 0, new List<EffectDefinition> { new(EffectType.Damage, 10) }),
            new("Heal", 12, TargetType.Self, ElementType.Normal, 2, new List<EffectDefinition> { new(EffectType.Heal, 20) }),
            new("Fireball", 15, TargetType.Enemy, ElementType.Fire, 1, new List<EffectDefinition> { new(EffectType.Damage, 25) }),
        };
        return new CatCharacter(name, hp, mana, speed, spells);
    }

    [Fact]
    public void BattleEngine_Start_SetsTurnBasedOnSpeed()
    {
        var fast = MakeCat("Fast", speed: 15);
        var slow = MakeCat("Slow", speed: 5);
        var context = new BattleContext(fast, slow);
        var engine = new BattleEngine(context);

        engine.Start();

        Assert.Equal(BattleState.PlayerTurn, context.State);
    }

    [Fact]
    public void BattleEngine_Start_OpponentGoesFirst_WhenFaster()
    {
        var slow = MakeCat("Slow", speed: 3);
        var fast = MakeCat("Fast", speed: 20);
        var context = new BattleContext(slow, fast);
        var engine = new BattleEngine(context);

        engine.Start();

        Assert.Equal(BattleState.OpponentTurn, context.State);
    }

    [Fact]
    public void CastSpell_DealsDamage()
    {
        var attacker = MakeCat("Attacker");
        var defender = MakeCat("Defender");
        var context = new BattleContext(attacker, defender);
        var spell = attacker.Spells[0]; // Scratch: 10 damage
        var cmd = new CastSpellCommand(spell);

        cmd.Execute(attacker, defender, context);

        Assert.Equal(90, defender.Hp);
    }

    [Fact]
    public void CastSpell_SpendsMana()
    {
        var cat = MakeCat("Caster");
        var target = MakeCat("Target");
        var context = new BattleContext(cat, target);
        var spell = cat.Spells[2]; // Fireball: 15 mana
        var cmd = new CastSpellCommand(spell);

        cmd.Execute(cat, target, context);

        Assert.Equal(35, cat.Mana);
    }

    [Fact]
    public void CastSpell_CannotExecute_WhenNotEnoughMana()
    {
        var spells = new List<SpellDefinition>
        {
            new("BigSpell", 999, TargetType.Enemy, ElementType.Fire, 0, new List<EffectDefinition> { new(EffectType.Damage, 50) }),
        };
        var cat = new CatCharacter("Broke", 100, 10, 10, spells);
        var target = MakeCat("Target");
        var context = new BattleContext(cat, target);
        var cmd = new CastSpellCommand(spells[0]);

        Assert.False(cmd.CanExecute(cat, target, context));
    }

    [Fact]
    public void Heal_RestoresHp()
    {
        var cat = MakeCat("Healer");
        cat.TakeDamage(30);
        var target = MakeCat("Dummy");
        var context = new BattleContext(cat, target);
        var healSpell = cat.Spells[1]; // Heal: 20 hp
        var cmd = new CastSpellCommand(healSpell);

        cmd.Execute(cat, cat, context);

        Assert.Equal(90, cat.Hp);
    }

    [Fact]
    public void Shield_AbsorbsDamage()
    {
        var cat = MakeCat("Shielded");
        cat.Shield = 10;
        cat.TakeDamage(15);

        Assert.Equal(95, cat.Hp);
        Assert.Equal(0, cat.Shield);
    }

    [Fact]
    public void Poison_DealsDamageOverTime()
    {
        var cat = MakeCat("Poisoned");
        cat.ActiveEffects.Add(new ActiveEffect(EffectType.Poison, 5, 2));
        var context = new BattleContext(cat, MakeCat("Other"));

        EndOfTurnProcessor.Process(cat, context);

        Assert.Equal(95, cat.Hp);
        Assert.Single(cat.ActiveEffects);
        Assert.Equal(1, cat.ActiveEffects[0].RemainingTurns);
    }

    [Fact]
    public void BattleEnds_WhenCatDies()
    {
        var player = MakeCat("Player", hp: 100);
        var opponent = MakeCat("Opponent", hp: 5);
        var context = new BattleContext(player, opponent);
        var engine = new BattleEngine(context);
        engine.Start();

        var cmd = new CastSpellCommand(player.Spells[0]); // Scratch 10 damage
        engine.ExecutePlayerAction(cmd);

        Assert.Equal(BattleState.BattleEnd, context.State);
        Assert.Equal(player, context.Winner);
    }

    [Fact]
    public void SpellLoader_ParsesJson()
    {
        var json = """
        [
          {
            "name": "Zap",
            "manaCost": 5,
            "target": "Enemy",
            "element": "Lightning",
            "cooldown": 0,
            "effects": [{ "type": "Damage", "value": 10 }]
          }
        ]
        """;

        var spells = SpellLoader.LoadFromJson(json);

        Assert.Single(spells);
        Assert.Equal("Zap", spells[0].Name);
        Assert.Equal(ElementType.Lightning, spells[0].Element);
    }
}
