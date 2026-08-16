using System.Text.Json;
using CatFighter.Game.Models;

namespace CatFighter.Game.Data;

public static class SpellLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<SpellDefinition> LoadFromJson(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<SpellDto>>(json, Options)
            ?? throw new InvalidOperationException("Failed to parse spells JSON.");

        return dtos.Select(d => new SpellDefinition(
            d.Name,
            d.ManaCost,
            Enum.Parse<TargetType>(d.Target, true),
            Enum.Parse<ElementType>(d.Element, true),
            d.Cooldown,
            d.Effects.Select(e => new EffectDefinition(
                Enum.Parse<EffectType>(e.Type, true),
                e.Value,
                e.Duration)).ToList()
        )).ToList();
    }

    public static List<SpellDefinition> LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    private record SpellDto(string Name, int ManaCost, string Target, string Element, int Cooldown, List<EffectDto> Effects);
    private record EffectDto(string Type, int Value, int Duration = 0);
}
