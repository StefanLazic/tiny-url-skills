using CatFighter.Game.Battle;
using CatFighter.Game.Commands;
using CatFighter.Game.Models;

namespace CatFighter.Game.AI;

public class SimpleAiPlayer
{
    private readonly Random _rng = new();

    public IBattleCommand ChooseAction(CatCharacter self, CatCharacter enemy, BattleContext context)
    {
        var available = self.Spells
            .Select(s => new CastSpellCommand(s))
            .Where(cmd => cmd.CanExecute(self, enemy, context))
            .ToList();

        if (available.Count == 0)
        {
            // Fallback: basic scratch (always available, 0 mana)
            var scratch = new SpellDefinition("Scratch", 0, TargetType.Enemy, ElementType.Normal, 0,
                new List<EffectDefinition> { new(EffectType.Damage, 5) });
            return new CastSpellCommand(scratch);
        }

        // Simple heuristic: prefer heals when low HP, prefer damage when enemy is low
        var scored = available.Select(cmd =>
        {
            var spell = self.Spells.First(s => cmd.Description.Contains(s.Name));
            double score = _rng.NextDouble() * 2;

            if (self.Hp < self.MaxHp * 0.3 && spell.Effects.Any(e => e.Type == EffectType.Heal))
                score += 5;
            if (enemy.Hp < enemy.MaxHp * 0.3 && spell.Effects.Any(e => e.Type == EffectType.Damage))
                score += 4;
            if (spell.Effects.Any(e => e.Type == EffectType.Damage))
                score += 1;

            return (cmd, score);
        }).OrderByDescending(x => x.score).ToList();

        return scored.First().cmd;
    }
}
