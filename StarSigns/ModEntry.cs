using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StarSigns.Menus;
using StarSigns.Patches;

namespace StarSigns;

public class ModEntry : Mod
{
    public const string IconAsset = "Mods/Huskyn1nja.StarSigns/Icons";
    public static ModEntry Instance;
    public static ModConfig Config;
    readonly CreationPanel panel = new();
    bool needsPick;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.Content.AssetRequested += (_, e) =>
        {
            if (e.NameWithoutLocale.IsEquivalentTo(IconAsset))
                e.LoadFromModFile<Texture2D>("assets/zodiac.png", AssetLoadPriority.Exclusive);
            else if (e.NameWithoutLocale.IsEquivalentTo(Zodiac.DataAsset))
                e.LoadFrom(SignData.Defaults, AssetLoadPriority.Exclusive);
        };
        helper.Events.Content.AssetsInvalidated += (_, e) =>
        {
            if (e.NamesWithoutLocale.Any(n => n.IsEquivalentTo(Zodiac.DataAsset)))
                Zodiac.Invalidate();
        };
        helper.Events.GameLoop.GameLaunched += (_, _) =>
        {
            Hooks.Register();
            Hooks.RegisterTokens(helper.ModRegistry.GetApi<IContentPatcherApi>("Pathoschild.ContentPatcher"), ModManifest);
        };
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => { needsPick = false; panel.Reset(); };
        helper.Events.Display.RenderedActiveMenu += (_, e) => panel.Draw(e.SpriteBatch);
        helper.Events.Input.ButtonPressed += (_, e) =>
        {
            var pos = e.Cursor.GetScaledScreenPixels();
            if (e.Button == SButton.MouseLeft && panel.Click((int)pos.X, (int)pos.Y))
                helper.Input.Suppress(e.Button);
        };

        var harmony = new Harmony(ModManifest.UniqueID);
        FriendshipPatch.Apply(harmony);
        SocialPagePatch.Apply(harmony);
        PerkPatches.Apply(harmony);
    }

    public static string T(string key, object tokens = null) => (tokens == null ? Instance.Helper.Translation.Get(key) : Instance.Helper.Translation.Get(key, tokens)).ToString();

    void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        var sign = Zodiac.Get(Game1.player);
        if (sign != null)
            SignBuffs.Apply(Game1.player, sign.Value, true);
        else
        {
            SignBuffs.Clear(Game1.player);
            needsPick = true;
        }
    }

    void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        panel.Tick();
        if (!needsPick || !Context.IsPlayerFree) return;
        needsPick = false;
        Game1.activeClickableMenu = new SignPickerMenu();
    }
}
