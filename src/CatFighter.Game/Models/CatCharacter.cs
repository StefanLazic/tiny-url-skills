namespace CatFighter.Game.Models;

public class CatCharacter
{
    public string Name { get; }
    public int MaxHp { get; }
    public int Hp { get; private set; }
    public int MaxMana { get; }
    public int Mana { get; private set; }
    public int Speed { get; set; }
    public List<SpellDefinition> Spells { get; }
    public List<ActiveEffect> ActiveEffects { get; } = new();
    public Dictionary<string, int> Cooldowns { get; } = new();
    public int Shield { get; set; }
    public bool IsAlive => Hp > 0;
    public bool IsStunned => ActiveEffects.Any(e => e.Type == EffectType.Stun);

    public CatCharacter(string name, int maxHp, int maxMana, int speed, List<SpellDefinition> spells)
    {
        Name = name;
        MaxHp = maxHp;
        Hp = maxHp;
        MaxMana = maxMana;
        Mana = maxMana;
        Speed = speed;
        Spells = spells;
    }

    public void TakeDamage(int amount)
    {
        var absorbed = Math.Min(Shield, amount);
        Shield -= absorbed;
        var remaining = amount - absorbed;
        Hp = Math.Max(0, Hp - remaining);
    }

    public void Heal(int amount)
    {
        Hp = Math.Min(MaxHp, Hp + amount);
    }

    public bool SpendMana(int cost)
    {
        if (Mana < cost) return false;
        Mana -= cost;
        return true;
    }

    public void RestoreMana(int amount)
    {
        Mana = Math.Min(MaxMana, Mana + amount);
    }
}

public class ActiveEffect
{
    public EffectType Type { get; }
    public int Value { get; }
    public int RemainingTurns { get; set; }

    public ActiveEffect(EffectType type, int value, int duration)
    {
        Type = type;
        Value = value;
        RemainingTurns = duration;
    }
}
