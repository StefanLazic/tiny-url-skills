using CatFighter.Game.Models;

namespace CatFighter.Game.Effects;

public static class EndOfTurnProcessor
{
    public static void Process(CatCharacter cat, Battle.BattleContext context)
    {
        for (int i = cat.ActiveEffects.Count - 1; i >= 0; i--)
        {
            var effect = cat.ActiveEffects[i];
            switch (effect.Type)
            {
                case EffectType.Poison:
                    cat.TakeDamage(effect.Value);
                    context.Log($"  {cat.Name} takes {effect.Value} poison damage! (HP: {cat.Hp}/{cat.MaxHp})");
                    break;
            }

            effect.RemainingTurns--;
            if (effect.RemainingTurns <= 0)
            {
                context.Log($"  {effect.Type} wears off on {cat.Name}.");
                cat.ActiveEffects.RemoveAt(i);
            }
        }

        // Reduce cooldowns
        var keys = cat.Cooldowns.Keys.ToList();
        foreach (var key in keys)
        {
            cat.Cooldowns[key]--;
            if (cat.Cooldowns[key] <= 0)
                cat.Cooldowns.Remove(key);
        }

        // Mana regen
        cat.RestoreMana(2);
    }
}
