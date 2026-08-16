namespace CatFighter.Game.Models;

public enum ElementType
{
    Normal,
    Fire,
    Ice,
    Lightning,
    Shadow
}

public enum TargetType
{
    Self,
    Enemy
}

public record SpellDefinition(
    string Name,
    int ManaCost,
    TargetType Target,
    ElementType Element,
    int Cooldown,
    IReadOnlyList<EffectDefinition> Effects);

public record EffectDefinition(
    EffectType Type,
    int Value,
    int Duration = 0);

public enum EffectType
{
    Damage,
    Heal,
    Poison,
    Shield,
    Stun,
    SpeedBuff,
    SpeedDebuff
}
