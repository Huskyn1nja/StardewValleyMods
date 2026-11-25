using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Characters;

namespace StarSigns;

public enum Sign { Aries, Taurus, Gemini, Cancer, Leo, Virgo, Libra, Scorpio, Sagittarius, Capricorn, Aquarius, Pisces }
public enum Match { Neutral, Good, Clash }

public static class Zodiac
{
    public const string Key = "Huskyn1nja.StarSigns/Sign";
    public const string DataAsset = "Huskyn1nja.StarSigns/Signs";
    static readonly string[] elements = { "Fire", "Earth", "Air", "Water" };
    static Dictionary<string, SignData> data;

    public static Dictionary<string, SignData> Data => data ??= Game1.content.Load<Dictionary<string, SignData>>(DataAsset);
    public static void Invalidate() => data = null;
    public static SignData Info(Sign s) => Data.TryGetValue(s.ToString(), out var d) && d != null ? d : SignData.Defaults()[s.ToString()];

    public static Texture2D Texture(Sign s) => Game1.content.Load<Texture2D>(Info(s).Texture ?? ModEntry.IconAsset);
    public static Rectangle IconSource(Sign s) => Game1.getSourceRectForStandardTileSheet(Texture(s), Info(s).SpriteIndex, 16, 16);

    public static Sign? FromDate(Season season, int day)
    {
        foreach (var s in Enum.GetValues<Sign>())
        {
            var d = Info(s);
            if (d.Season == season && day >= d.StartDay && day <= d.EndDay) return s;
        }
        return null;
    }

    public static Sign? Today() => FromDate(Game1.season, Game1.dayOfMonth);

    public static Match Compare(Sign a, Sign b)
    {
        int gap = Math.Abs(a - b);
        gap = Math.Min(gap, 12 - gap);
        if (gap is 0 or 2 or 4) return Match.Good;
        if (gap is 3 or 6) return Match.Clash;
        return Match.Neutral;
    }

    public static Match MatchFor(Farmer who, Sign theirs)
    {
        if (Get(who) is not Sign mine) return Match.Neutral;
        var match = Compare(mine, theirs);
        return match == Match.Neutral && SignBuffs.Has(who, Stat.Charm) ? Match.Good : match;
    }

    public static Sign? Parse(string raw) => Enum.TryParse(raw, true, out Sign s) && Enum.IsDefined(s) ? s : null;

    public static Sign? Get(Farmer who) => who != null && who.modData.TryGetValue(Key, out var raw) ? Parse(raw) : null;

    public static void Set(Farmer who, Sign s) => who.modData[Key] = s.ToString();

    public static Sign? Get(NPC npc) => npc is null or Child ? null : Get(npc.Name);

    public static Sign? Get(string name)
    {
        if (name == null || !Game1.characterData.TryGetValue(name, out var npc)) return null;
        if (npc.CustomFields != null && npc.CustomFields.TryGetValue(Key, out var forced)) return Parse(forced);
        if (npc.BirthSeason is not Season season || npc.BirthDay < 1 || npc.BirthDay > 28) return null;
        return FromDate(season, npc.BirthDay);
    }

    public static string Name(Sign s) => Info(s).DisplayName ?? ModEntry.T("sign." + s);
    public static string Element(Sign s) => Info(s).Element ?? ModEntry.T("element." + elements[(int)s % 4]);
    public static string Dates(Sign s) => ModEntry.T("dates", new { season = ModEntry.T("season." + Info(s).Season), start = Info(s).StartDay, end = Info(s).EndDay });
}

public enum EffectType { Bonus, Perk, Drawback }

public class SignEffect
{
    public string Id;
    public EffectType Type;
    public Stat Stat;
    public float Amount;
}

public class SignData
{
    public string DisplayName;
    public string Element;
    public Season Season;
    public int StartDay;
    public int EndDay;
    public string Texture = ModEntry.IconAsset;
    public int SpriteIndex;
    public List<SignEffect> Effects = new();

    public static Dictionary<string, SignData> Defaults()
    {
        var data = new Dictionary<string, SignData>();
        void Add(Sign sign, int sprite, Stat bonus, float b, Stat perk, float p, Stat drawback, float d)
        {
            int i = (int)sign;
            data[sign.ToString()] = new SignData
            {
                Season = (Season)(i / 3),
                StartDay = new[] { 1, 10, 19 }[i % 3],
                EndDay = new[] { 9, 18, 28 }[i % 3],
                SpriteIndex = sprite,
                Effects = new()
                {
                    new() { Id = "Bonus", Type = EffectType.Bonus, Stat = bonus, Amount = b },
                    new() { Id = "Perk", Type = EffectType.Perk, Stat = perk, Amount = p },
                    new() { Id = "Drawback", Type = EffectType.Drawback, Stat = drawback, Amount = d }
                }
            };
        }

        Add(Sign.Aries, 4, Stat.Attack, 2, Stat.CombatXp, 0.25f, Stat.Defense, -2);
        Add(Sign.Taurus, 5, Stat.Farming, 1, Stat.CropPrice, 0.1f, Stat.Speed, -0.5f);
        Add(Sign.Gemini, 6, Stat.Speed, 0.5f, Stat.Foraging, 1, Stat.MaxEnergy, -15);
        Add(Sign.Cancer, 7, Stat.Fishing, 1, Stat.FishingXp, 0.25f, Stat.Mining, -1);
        Add(Sign.Leo, 3, Stat.Luck, 1, Stat.GemPrice, 0.1f, Stat.Fishing, -1);
        Add(Sign.Virgo, 2, Stat.Farming, 1, Stat.ArtisanPrice, 0.1f, Stat.Luck, -1);
        Add(Sign.Libra, 0, Stat.Luck, 1, Stat.Charm, 0, Stat.Attack, -2);
        Add(Sign.Scorpio, 1, Stat.Mining, 1, Stat.MiningXp, 0.25f, Stat.Foraging, -1);
        Add(Sign.Sagittarius, 8, Stat.Foraging, 1, Stat.ForagingXp, 0.25f, Stat.Farming, -1);
        Add(Sign.Capricorn, 9, Stat.Mining, 1, Stat.MaxEnergy, 30, Stat.Luck, -1);
        Add(Sign.Aquarius, 10, Stat.Magnetism, 64, Stat.FarmingXp, 0.25f, Stat.Fishing, -1);
        Add(Sign.Pisces, 11, Stat.Fishing, 1, Stat.FishPrice, 0.1f, Stat.Defense, -2);
        return data;
    }
}

public enum Stat
{
    Farming, Fishing, Foraging, Mining, Luck, Attack, Defense, Speed, MaxEnergy, Magnetism,
    FarmingXp, FishingXp, ForagingXp, MiningXp, CombatXp, CropPrice, ArtisanPrice, GemPrice, FishPrice, Charm
}

public static class SignBuffs
{
    const string MainId = "Huskyn1nja.StarSigns_Sign";
    const string SurgeId = "Huskyn1nja.StarSigns_Surge";

    static bool IsBuff(SignEffect e) => e.Stat <= Stat.Magnetism;

    public static bool InSeason(Sign sign) => Zodiac.Today() == sign;

    public static bool Has(Farmer who, Stat stat) => Zodiac.Get(who) is Sign s && Zodiac.Info(s).Effects.Any(e => e.Stat == stat);

    public static float Amount(Farmer who, Stat stat)
    {
        if (Zodiac.Get(who) is not Sign s) return 0;
        bool doubled = InSeason(s);
        return Zodiac.Info(s).Effects.Where(e => e.Stat == stat).Sum(e => e.Amount * (doubled && e.Type != EffectType.Drawback ? 2 : 1));
    }

    public static void Apply(Farmer who, Sign sign, bool announce)
    {
        var effects = Zodiac.Info(sign).Effects;
        var boosts = effects.Where(e => e.Type != EffectType.Drawback).ToList();
        string name = Zodiac.Name(sign);

        who.applyBuff(Make(MainId, sign, name, Join(ModEntry.T("buff.desc"), effects, 1), effects));
        who.buffs.Remove(SurgeId);
        if (!InSeason(sign)) return;

        who.applyBuff(Make(SurgeId, sign, ModEntry.T("surge.name", new { sign = name }), Join(ModEntry.T("surge.desc", new { sign = name }), boosts.Where(e => e.Stat != Stat.Charm), 2), boosts));
        if (announce && Game1.dayOfMonth == Zodiac.Info(sign).StartDay)
            Game1.addHUDMessage(new HUDMessage(ModEntry.T("surge.hud", new { sign = name }), HUDMessage.newQuest_type));
    }

    public static void Clear(Farmer who)
    {
        who.buffs.Remove(MainId);
        who.buffs.Remove(SurgeId);
    }

    public static List<(string Text, Color Color)> Lines(Sign sign)
    {
        var lines = Zodiac.Info(sign).Effects
            .Select(e => (Describe(e, 1), e.Type == EffectType.Drawback ? new Color(150, 40, 40) : new Color(40, 110, 40)))
            .ToList();
        lines.Add((ModEntry.T("surge.line"), new Color(70, 60, 140)));
        return lines;
    }

    static string Join(string desc, IEnumerable<SignEffect> effects, float mult) =>
        string.Join("\n", effects.Where(e => !IsBuff(e)).Select(e => Describe(e, mult)).Prepend(desc));

    static string Describe(SignEffect e, float mult)
    {
        if (e.Stat == Stat.Charm) return ModEntry.T("stat.Charm");
        float value = e.Amount * mult;
        string amount = IsBuff(e) ? value.ToString() : (value * 100).ToString("0") + "%";
        return ModEntry.T("stat.line", new { amount = (value > 0 ? "+" : "") + amount, stat = ModEntry.T("stat." + e.Stat) });
    }

    static Buff Make(string id, Sign sign, string name, string desc, IEnumerable<SignEffect> effects)
    {
        var fx = new BuffEffects();
        foreach (var e in effects.Where(IsBuff))
        {
            var field = e.Stat switch
            {
                Stat.Farming => fx.FarmingLevel,
                Stat.Fishing => fx.FishingLevel,
                Stat.Foraging => fx.ForagingLevel,
                Stat.Mining => fx.MiningLevel,
                Stat.Luck => fx.LuckLevel,
                Stat.Attack => fx.Attack,
                Stat.Defense => fx.Defense,
                Stat.Speed => fx.Speed,
                Stat.MaxEnergy => fx.MaxStamina,
                _ => fx.MagneticRadius
            };
            field.Value += e.Amount;
        }
        return new Buff(id, "Huskyn1nja.StarSigns", ModEntry.T("panel.title"), Buff.ENDLESS, Zodiac.Texture(sign), Zodiac.Info(sign).SpriteIndex, fx, false, name, desc);
    }
}
