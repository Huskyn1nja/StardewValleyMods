using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;

namespace KindredCrows
{
    public class ModEntry : Mod
    {
        public const string FriendshipKey = "Huskyn1nja.KindredCrows/Friendship";
        public const string NeglectKey = "Huskyn1nja.KindredCrows/Neglect";
        public const string GiftKey = "Huskyn1nja.KindredCrows/Gift";
        public const string FeederId = "Huskyn1nja.CrowFeeder";
        public const int MaxPoints = 2500;

        private const string DietPath = "Mods/Huskyn1nja.KindredCrows/Diet";
        private const string LootPath = "Mods/Huskyn1nja.KindredCrows/Loot";
        private const string ThemesPath = "Huskyn1nja.KindredCrows/Themes";
        private const string TexturePrefix = "Mods/Huskyn1nja.KindredCrows/Textures/";
        private const string ChestId = "FeederChest";
        private const int LootThreshold = 1500;
        private const int NeglectDays = 10;
        private const int NeglectPenalty = 25;

        private static readonly Dictionary<string, string> ThemeFiles = new()
        {
            ["Default"] = "assets/Feeder_Vanilla.png",
            ["Earthy"] = "assets/Feeder_Earthy.png",
            ["VPR"] = "assets/Feeder_VPR.png"
        };

        private static readonly (string Tier, int Points)[] DietTiers =
        {
            ("Loved", 25),
            ("Liked", 12),
            ("Neutral", 5)
        };

        private static readonly string[] FeederLocations = { "Farm", "Woods" };

        private ModConfig config;

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>();
            new Harmony(ModManifest.UniqueID).PatchAll();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Content.AssetRequested += OnAssetRequested;

            helper.ConsoleCommands.Add("crow_setfriendship", helper.Translation.Get("command.setfriendship"), SetFriendship);
            helper.ConsoleCommands.Add("crow_getfriendship", helper.Translation.Get("command.getfriendship"), GetFriendship);
        }

        public static int GetData(string key)
        {
            return Game1.player.modData.TryGetValue(key, out string raw) && int.TryParse(raw, out int value) ? value : 0;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            gmcm.Register(
                ModManifest,
                reset: () => config = new ModConfig(),
                save: () =>
                {
                    Helper.WriteConfig(config);
                    Helper.GameContent.InvalidateCache("Data/Buildings");
                });

            gmcm.AddTextOption(
                ModManifest,
                getValue: () => config.FeederTheme,
                setValue: value => config.FeederTheme = value,
                name: () => "Feeder Theme",
                tooltip: () => "Changes the visual style of the Crow Feeder.",
                allowedValues: Helper.GameContent.Load<Dictionary<string, string>>(ThemesPath).Keys.ToArray());
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree || !e.Button.IsActionButton())
                return;

            Building building = Game1.currentLocation.getBuildingAt(e.Cursor.GrabTile);
            if (!IsFeeder(building) || building.GetBuildingChest(ChestId) is not Chest chest)
                return;

            chest.playerChest.Value = true;
            chest.mutex.RequestLock(chest.ShowMenu);
            Helper.Input.Suppress(e.Button);
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(DietPath))
            {
                e.LoadFromModFile<Dictionary<string, List<string>>>("assets/offerings.json", AssetLoadPriority.Medium);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(LootPath))
            {
                e.LoadFromModFile<List<LootData>>("assets/loot.json", AssetLoadPriority.Medium);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(ThemesPath))
            {
                e.LoadFrom(() => ThemeFiles.Keys.ToDictionary(theme => theme, theme => TexturePrefix + theme), AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(EditBuildings);
            }
            else
            {
                foreach (var (theme, file) in ThemeFiles)
                {
                    if (e.NameWithoutLocale.IsEquivalentTo(TexturePrefix + theme))
                    {
                        e.LoadFromModFile<Texture2D>(file, AssetLoadPriority.Medium);
                        break;
                    }
                }
            }
        }

        private void EditBuildings(IAssetData asset)
        {
            var themes = Helper.GameContent.Load<Dictionary<string, string>>(ThemesPath);
            if (!themes.TryGetValue(config.FeederTheme, out string texture))
                texture = TexturePrefix + "Default";

            asset.AsDictionary<string, BuildingData>().Data[FeederId] = new BuildingData
            {
                Name = "Crow Feeder",
                Description = Helper.Translation.Get("feeder.description"),
                Builder = "Robin",
                BuildCost = 1000,
                BuildDays = 1,
                BuildMaterials = new List<BuildingMaterial>
                {
                    new() { ItemId = "(O)388", Amount = 50 },
                    new() { ItemId = "(O)335", Amount = 2 },
                    new() { ItemId = "(O)82", Amount = 1 }
                },
                Size = new Point(2, 2),
                CollisionMap = "OO\nXX",
                Texture = texture,
                Chests = new List<BuildingChest>
                {
                    new() { Id = ChestId, DisplayTile = new Vector2(-9999, -9999) }
                }
            };
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Context.IsMainPlayer || GetData(FriendshipKey) <= 0)
                return;

            foreach (var (location, feeder) in GetFeeders())
            {
                int count = Game1.random.Next(1, 4);
                for (int i = 0; i < count; i++)
                {
                    int x = feeder.tileX.Value + Game1.random.Next(feeder.tilesWide.Value);
                    int y = feeder.tileY.Value + Game1.random.Next(feeder.tilesHigh.Value);
                    location.addCritter(new Crow(x, y));
                }
            }
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            int points = GetData(FriendshipKey);
            int neglect = GetData(NeglectKey);

            if (TryFeed(ref points))
            {
                neglect = 0;
            }
            else if (++neglect >= NeglectDays)
            {
                points = Math.Max(0, points - NeglectPenalty);
                neglect = 0;
            }

            Game1.player.modData[FriendshipKey] = points.ToString();
            Game1.player.modData[NeglectKey] = neglect.ToString();
        }

        private bool TryFeed(ref int points)
        {
            var diet = Helper.GameContent.Load<Dictionary<string, List<string>>>(DietPath);

            foreach (var (_, feeder) in GetFeeders())
            {
                Chest chest = feeder.GetBuildingChest(ChestId);
                if (chest == null)
                    continue;

                for (int i = 0; i < chest.Items.Count; i++)
                {
                    Item item = chest.Items[i];
                    int gain = item == null || item.modData.ContainsKey(GiftKey) ? 0 : GetGain(diet, item.QualifiedItemId);
                    if (gain == 0)
                        continue;

                    points = Math.Min(MaxPoints, points + gain);
                    chest.Items[i] = item.ConsumeStack(1);

                    if (points >= LootThreshold && RollLoot(points) is Item loot)
                    {
                        loot.modData[GiftKey] = "1";
                        chest.addItem(loot);
                    }

                    return true;
                }
            }

            return false;
        }

        private static int GetGain(Dictionary<string, List<string>> diet, string itemId)
        {
            foreach (var (tier, points) in DietTiers)
            {
                if (diet.TryGetValue(tier, out var items) && items.Contains(itemId))
                    return points;
            }

            return 0;
        }

        private Item RollLoot(int points)
        {
            var eligible = Helper.GameContent.Load<List<LootData>>(LootPath)
                .Where(entry => points >= entry.MinPoints)
                .OrderBy(entry => entry.MinPoints)
                .ToList();

            if (eligible.Count == 0)
                return null;

            int[] weights = eligible.Select((_, i) => i < 7 ? 100 >> i : 1).ToArray();
            int roll = Game1.random.Next(weights.Sum());

            for (int i = 0; i < eligible.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                    return CreateLoot(eligible[i].ItemId);
            }

            return CreateLoot(eligible[0].ItemId);
        }

        private static Item CreateLoot(string itemId)
        {
            switch (itemId)
            {
                case "SEASONAL_SEEDS":
                    return ItemRegistry.Create(GetSeasonalSeeds());

                case "VANILLA_TRINKET":
                    string trinket = Game1.player.stats.Get("trinketSlots") > 0 ? GetRandomTrinket() : null;
                    return ItemRegistry.Create(trinket ?? "(O)74");

                default:
                    return ItemRegistry.Create(itemId);
            }
        }

        private static string GetSeasonalSeeds()
        {
            Season season = Game1.season;
            if (Game1.dayOfMonth >= 25)
                season = (Season)(((int)season + 1) % 4);

            return season switch
            {
                Season.Summer => "(O)496",
                Season.Fall => "(O)497",
                Season.Winter => "(O)498",
                _ => "(O)495"
            };
        }

        private static string GetRandomTrinket()
        {
            var trinkets = DataLoader.Trinkets(Game1.content)
                .Where(pair => pair.Value.DropsNaturally)
                .Select(pair => "(TR)" + pair.Key)
                .ToList();

            return trinkets.Count > 0 ? trinkets[Game1.random.Next(trinkets.Count)] : null;
        }

        private static IEnumerable<(GameLocation Location, Building Feeder)> GetFeeders()
        {
            foreach (string name in FeederLocations)
            {
                GameLocation location = Game1.getLocationFromName(name);
                if (location == null)
                    continue;

                foreach (Building building in location.buildings)
                {
                    if (IsFeeder(building))
                        yield return (location, building);
                }
            }
        }

        private static bool IsFeeder(Building building)
        {
            return building?.buildingType.Value == FeederId && !building.isUnderConstruction();
        }

        private void SetFriendship(string command, string[] args)
        {
            if (!Context.IsWorldReady || args.Length == 0 || !int.TryParse(args[0], out int val))
                return;

            Game1.player.modData[FriendshipKey] = Math.Clamp(val, 0, MaxPoints).ToString();
            Monitor.Log(Helper.Translation.Get("command.success", new { val }), LogLevel.Info);
        }

        private void GetFriendship(string command, string[] args)
        {
            if (!Context.IsWorldReady)
                return;

            int p = GetData(FriendshipKey);
            Monitor.Log(Helper.Translation.Get("command.current", new { p, hearts = p / 250 }), LogLevel.Info);
        }
    }

    public class LootData
    {
        public string ItemId { get; set; }
        public int MinPoints { get; set; }
    }

    [HarmonyPatch(typeof(Farm), "addCrows")]
    internal static class FarmCrowPatch
    {
        private static bool Prefix()
        {
            double skipChance = ModEntry.GetData(ModEntry.FriendshipKey) switch
            {
                >= 1250 => 1.0,
                >= 1000 => 0.75,
                >= 750 => 0.5,
                >= 500 => 0.3,
                >= 250 => 0.1,
                _ => 0.0
            };

            return Game1.random.NextDouble() >= skipChance;
        }
    }

    [HarmonyPatch(typeof(Woods), nameof(Woods.DayUpdate))]
    internal static class WoodsCrowPatch
    {
        private static void Postfix(Woods __instance)
        {
            __instance.critters ??= new List<Critter>();

            if (!__instance.critters.OfType<Crow>().Any())
                __instance.addCritter(new Crow(18, 6));
        }
    }
}
