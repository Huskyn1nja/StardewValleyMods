using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.GameData.Tools;

namespace PokingStick
{
    public class ModEntry : Mod
    {
        private ModConfig config;
        private string stickId;
        private string stickTexture;

        private int pokeTicks;
        private int pokeFacing;
        private NPC pokeTarget;
        const int PokeTime = 18;

        private Dictionary<string, Hurry> hurrying = new();
        private Dictionary<string, int> lastPoked = new();

        class Hurry
        {
            public NPC Npc;
            public float Leftover;
            public int StoppedFor;
        }

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>();
            stickId = ModManifest.UniqueID + "_PokingStick";
            stickTexture = $"Mods/{ModManifest.UniqueID}/PokingStick";

            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.GameLoop.DayStarted += (s, e) => Reset();
            helper.Events.GameLoop.DayEnding += (s, e) => Reset();
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(stickTexture))
            {
                e.LoadFromModFile<Texture2D>("assets/stick.png", AssetLoadPriority.Medium);
                return;
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Tools"))
            {
                e.Edit(asset =>
                {
                    asset.AsDictionary<string, ToolData>().Data[stickId] = new ToolData
                    {
                        ClassName = "GenericTool",
                        Name = "PokingStick",
                        DisplayName = Helper.Translation.Get("item.name"),
                        Description = Helper.Translation.Get("item.description"),
                        Texture = stickTexture,
                        SpriteIndex = 0,
                        MenuSpriteIndex = 0,
                        SalePrice = 10,
                        CanBeLostOnDeath = false
                    };
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                e.Edit(asset =>
                {
                    var shops = asset.AsDictionary<string, ShopData>().Data;
                    if (!shops.TryGetValue("Carpenter", out var robin) || robin.Items.Any(i => i.Id == stickId))
                        return;

                    robin.Items.Add(new ShopItemData
                    {
                        Id = stickId,
                        ItemId = "(T)" + stickId,
                        Price = config.ShopPrice
                    });
                });
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree || !e.Button.IsUseToolButton())
                return;

            var who = Game1.player;
            if (who.CurrentTool?.ItemId != stickId)
                return;

            Helper.Input.Suppress(e.Button);
            if (pokeTicks > 0)
                return;

            pokeTarget = FindNpc(e.Cursor.GrabTile);
            who.faceGeneralDirection(pokeTarget?.getStandingPosition() ?? e.Cursor.AbsolutePixels);
            who.Halt();
            who.freezePause = PokeTime * 16;

            pokeFacing = who.FacingDirection;
            pokeTicks = PokeTime;
            Game1.playSound("daggerswipe");
        }

        private NPC FindNpc(Vector2 cursorTile)
        {
            var who = Game1.player;
            var tiles = new[] { cursorTile, who.GetGrabTile() };

            NPC best = null;
            float bestDist = 160f;

            foreach (var npc in who.currentLocation.characters)
            {
                if (!npc.IsVillager || npc.IsInvisible)
                    continue;

                var box = npc.GetBoundingBox();
                box.Inflate(16, 16);
                if (!tiles.Any(t => box.Intersects(new Rectangle((int)t.X * 64, (int)t.Y * 64, 64, 64))))
                    continue;

                float dist = Vector2.Distance(npc.getStandingPosition(), who.getStandingPosition());
                if (dist < bestDist)
                {
                    best = npc;
                    bestDist = dist;
                }
            }

            return best;
        }

        private void Poke(NPC npc)
        {
            if (npc.currentLocation != Game1.player.currentLocation)
                return;

            if (lastPoked.TryGetValue(npc.Name, out int when) && Game1.ticks - when < config.CooldownSeconds * 60)
                return;
            lastPoked[npc.Name] = Game1.ticks;

            Game1.playSound("woodyHit");
            npc.shake(300);

            if (hurrying.ContainsKey(npc.Name))
                npc.doEmote(12);
            else if (npc.controller == null)
                npc.doEmote(8);
            else
            {
                hurrying[npc.Name] = new Hurry { Npc = npc };
                npc.doEmote(16);
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (pokeTicks > 0)
            {
                pokeTicks--;
                if (pokeTicks == 10 && pokeTarget != null)
                {
                    Poke(pokeTarget);
                    pokeTarget = null;
                }
            }

            foreach (var name in hurrying.Keys.ToList())
            {
                var hurry = hurrying[name];
                var npc = hurry.Npc;

                if (npc.controller == null)
                {
                    if (++hurry.StoppedFor > 60)
                        hurrying.Remove(name);
                    continue;
                }

                hurry.StoppedFor = 0;
                hurry.Leftover += Math.Max(0f, config.HurryMultiplier - 1f);

                while (hurry.Leftover >= 1f && npc.controller != null)
                {
                    hurry.Leftover--;
                    if (npc.controller.update(Game1.currentGameTime))
                        npc.controller = null;
                }
            }
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (pokeTicks <= 0)
                return;

            var dir = pokeFacing switch
            {
                0 => new Vector2(0, -1),
                1 => new Vector2(1, 0),
                2 => new Vector2(0, 1),
                _ => new Vector2(-1, 0)
            };

            float t = 1f - pokeTicks / (float)PokeTime;
            float reach = (float)Math.Sin(t * Math.PI) * 28f;
            var pos = Game1.player.getStandingPosition() + new Vector2(0, -36) + dir * (12 + reach);
            float rotation = (float)Math.Atan2(dir.Y, dir.X) + MathHelper.PiOver4;

            var tex = Helper.GameContent.Load<Texture2D>(stickTexture);
            e.SpriteBatch.Draw(tex, Game1.GlobalToLocal(Game1.viewport, pos), new Rectangle(0, 0, 16, 16),
                Color.White, rotation, new Vector2(2, 13), 4f, SpriteEffects.None, 1f);
        }

        private void Reset()
        {
            hurrying.Clear();
            lastPoked.Clear();
            pokeTicks = 0;
            pokeTarget = null;
        }
    }
}

