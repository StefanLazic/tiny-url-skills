using CatFighter.Game.Models;

namespace CatFighter.Game.Commands;

public interface IBattleCommand
{
    string Description { get; }
    bool CanExecute(CatCharacter caster, CatCharacter target, Battle.BattleContext context);
    void Execute(CatCharacter caster, CatCharacter target, Battle.BattleContext context);
}

public class CastSpellCommand : IBattleCommand
{
    private readonly SpellDefinition _spell;

    public CastSpellCommand(SpellDefinition spell)
    {
        _spell = spell;
    }

    public string Description => $"Cast {_spell.Name} ({_spell.ManaCost} mana)";
    public SpellDefinition Spell => _spell;

    public bool CanExecute(CatCharacter caster, CatCharacter target, Battle.BattleContext context)
    {
        if (caster.IsStunned) return false;
        if (caster.Mana < _spell.ManaCost) return false;
        if (caster.Cooldowns.TryGetValue(_spell.Name, out var cd) && cd > 0) return false;
        return true;
    }

    public void Execute(CatCharacter caster, CatCharacter target, Battle.BattleContext context)
    {
        caster.SpendMana(_spell.ManaCost);
        if (_spell.Cooldown > 0)
            caster.Cooldowns[_spell.Name] = _spell.Cooldown;

        context.Log($"{caster.Name} casts {_spell.Name}!");

        foreach (var effect in _spell.Effects)
        {
            var effectTarget = _spell.Target == TargetType.Self ? caster : target;
            ApplyEffect(effect, effectTarget, context);
        }
    }

    private static void ApplyEffect(EffectDefinition effect, CatCharacter target, Battle.BattleContext context)
    {
        switch (effect.Type)
        {
            case EffectType.Damage:
                target.TakeDamage(effect.Value);
                context.Log($"  {target.Name} takes {effect.Value} damage! (HP: {target.Hp}/{target.MaxHp})");
                break;
            case EffectType.Heal:
                target.Heal(effect.Value);
                context.Log($"  {target.Name} heals for {effect.Value}! (HP: {target.Hp}/{target.MaxHp})");
                break;
            case EffectType.Shield:
                target.Shield += effect.Value;
                context.Log($"  {target.Name} gains {effect.Value} shield!");
                break;
            case EffectType.Poison:
            case EffectType.Stun:
            case EffectType.SpeedBuff:
            case EffectType.SpeedDebuff:
                target.ActiveEffects.Add(new ActiveEffect(effect.Type, effect.Value, effect.Duration));
                context.Log($"  {target.Name} is affected by {effect.Type} for {effect.Duration} turns!");
                break;
        }
    }
}
