using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Color = Microsoft.Xna.Framework.Color;
using Constants = StardewModdingAPI.Constants;

namespace NoNewGame;

internal sealed class ModEntry : Mod
{
    private static IMonitor SMonitor;
    private static bool? _hasSavedGamesCache;

    public override void Entry(IModHelper helper)
    {
        SMonitor = this.Monitor;
        var harmony = new Harmony(this.ModManifest.UniqueID);

        harmony.Patch(
            original: AccessTools.Method(typeof(TitleMenu), nameof(TitleMenu.performButtonAction)),
            prefix: new HarmonyMethod(typeof(ModEntry), nameof(TitleMenu_performButtonAction_Prefix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(TitleMenu), nameof(TitleMenu.draw), new Type[] { typeof(SpriteBatch) }),
            postfix: new HarmonyMethod(typeof(ModEntry), nameof(TitleMenu_draw_Postfix))
        );
    }

    public static bool TitleMenu_performButtonAction_Prefix(TitleMenu __instance, string which)
    {
        try
        {
            if (which == "New" && HasSavedGamesCached())
            {
                Game1.playSound("cancel");

                string message = "Nah uh! No new game for you.\nFinish your current farm first! Shame on you!";

                var popup = new ConfirmationDialog(
                    message,
                    onConfirm: (who) => { TitleMenu.subMenu = null; },
                    onCancel: null
                );

                popup.cancelButton.visible = false;
                popup.okButton.bounds.X = popup.xPositionOnScreen + (popup.width / 2) - (popup.okButton.bounds.Width / 2);

                TitleMenu.subMenu = popup;

                return false;
            }
        }
        catch (Exception ex)
        {
            if (SMonitor != null)
            {
                SMonitor.Log($"Button broke:\n{ex}", LogLevel.Error);
            }
        }

        return true;
    }

    public static void TitleMenu_draw_Postfix(TitleMenu __instance, SpriteBatch b)
    {
        try
        {
            if (!HasSavedGamesCached()) return;

            bool shouldDrawMenu = TitleMenu.subMenu == null || TitleMenu.subMenu is AboutMenu || TitleMenu.subMenu is LanguageSelectionMenu;

            if (shouldDrawMenu && __instance.buttonsToShow > 0 && __instance.buttons != null)
            {
                for (int i = 0; i < __instance.buttonsToShow; i++)
                {
                    if (i < __instance.buttons.Count && __instance.buttons[i]?.name == "New")
                    {
                        __instance.buttons[i].draw(b, Color.Black * 0.6f, 1f);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (SMonitor != null)
            {
                SMonitor.LogOnce($"Drawing the overlay broke:\n{ex}", LogLevel.Error);
            }
        }
    }

    private static bool HasSavedGamesCached()
    {
        if (_hasSavedGamesCache == null)
        {
            try
            {
                string savesPath = Constants.SavesPath;
                if (Directory.Exists(savesPath))
                {
                    _hasSavedGamesCache = Directory.EnumerateDirectories(savesPath).Any();
                }
                else
                {
                    _hasSavedGamesCache = false;
                }
            }
            catch (Exception ex)
            {
                if (SMonitor != null)
                {
                    SMonitor.Log($"Save file checking broke:\n{ex}", LogLevel.Error);
                }
                _hasSavedGamesCache = false;
            }
        }

        return _hasSavedGamesCache.Value;
    }
}