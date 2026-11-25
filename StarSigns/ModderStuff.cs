using System;
using System.Collections.Generic;
using System.Linq;
using StarSigns.Menus;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace StarSigns;

public interface IContentPatcherApi
{
    void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);
}

static class Hooks
{
    const string Prefix = "Huskyn1nja.StarSigns_";

    public static void Register()
    {
        GameStateQuery.Register(Prefix + "PLAYER_SIGN", PlayerSign);
        GameStateQuery.Register(Prefix + "PLAYER_IN_SEASON", PlayerInSeason);
        GameStateQuery.Register(Prefix + "NPC_SIGN", NpcSign);
        GameStateQuery.Register(Prefix + "MATCH", NpcMatch);
        GameStateQuery.Register(Prefix + "CURRENT_SIGN", (q, _) => AnyOf(q, 1, Zodiac.Today()?.ToString() ?? "None"));
        TriggerActionManager.RegisterAction(Prefix + "SetSign", SetSign);
        TriggerActionManager.RegisterAction(Prefix + "OpenPicker", OpenPicker);
    }

    public static void RegisterTokens(IContentPatcherApi cp, IManifest manifest)
    {
        if (cp == null) return;
        cp.RegisterToken(manifest, "PlayerSign", () => Ready(Zodiac.Get(Game1.player)?.ToString() ?? "None"));
        cp.RegisterToken(manifest, "PlayerSignName", () => Ready(Zodiac.Get(Game1.player) is Sign s ? Zodiac.Name(s) : ""));
        cp.RegisterToken(manifest, "CurrentSign", () => Ready(Zodiac.Today()?.ToString() ?? "None"));
        cp.RegisterToken(manifest, "CurrentSignName", () => Ready(Zodiac.Today() is Sign s ? Zodiac.Name(s) : ""));
    }

    static IEnumerable<string> Ready(string value) => !Context.IsWorldReady ? null : value == "" ? Array.Empty<string>() : new[] { value };

    static bool AnyOf(string[] q, int start, string value) => q.Skip(start).Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase));

    static bool PlayerSign(string[] q, GameStateQueryContext ctx)
    {
        if (!ArgUtility.TryGet(q, 1, out var target, out var error)) return GameStateQuery.Helpers.ErrorResult(q, error);
        return GameStateQuery.Helpers.WithPlayer(ctx.Player, target, who => AnyOf(q, 2, Zodiac.Get(who)?.ToString() ?? "None"));
    }

    static bool PlayerInSeason(string[] q, GameStateQueryContext ctx)
    {
        if (!ArgUtility.TryGet(q, 1, out var target, out var error)) return GameStateQuery.Helpers.ErrorResult(q, error);
        return GameStateQuery.Helpers.WithPlayer(ctx.Player, target, who => Zodiac.Get(who) is Sign s && SignBuffs.InSeason(s));
    }

    static bool NpcSign(string[] q, GameStateQueryContext ctx)
    {
        if (!ArgUtility.TryGet(q, 1, out var npc, out var error)) return GameStateQuery.Helpers.ErrorResult(q, error);
        return AnyOf(q, 2, Zodiac.Get(npc)?.ToString() ?? "None");
    }

    static bool NpcMatch(string[] q, GameStateQueryContext ctx)
    {
        if (!ArgUtility.TryGet(q, 1, out var target, out var error) || !ArgUtility.TryGet(q, 2, out var npc, out error))
            return GameStateQuery.Helpers.ErrorResult(q, error);
        return GameStateQuery.Helpers.WithPlayer(ctx.Player, target, who => AnyOf(q, 3, (Zodiac.Get(npc) is Sign s ? Zodiac.MatchFor(who, s) : Match.Neutral).ToString()));
    }

    static bool SetSign(string[] args, TriggerActionContext ctx, out string error)
    {
        if (!ArgUtility.TryGet(args, 1, out var raw, out error)) return false;
        if (Zodiac.Parse(raw) is not Sign sign)
        {
            error = $"'{raw}' isn't a star sign";
            return false;
        }
        Zodiac.Set(Game1.player, sign);
        SignBuffs.Apply(Game1.player, sign, false);
        return true;
    }

    static bool OpenPicker(string[] args, TriggerActionContext ctx, out string error)
    {
        error = null;
        Game1.activeClickableMenu = new SignPickerMenu();
        return true;
    }
}
