using Amonya.Constants;
using Amonya.CustomClasses;
using Amonya.Helpers;
using Amonya.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils.Json;
using static Amonya.CustomClasses.CustomBulletsManager;

namespace Amonya.Generators
{
    [Injectable(InjectionType.Singleton)]
    public class QuestGenerator(
        ISptLogger<Amonya> logger,
        ConfigLoader configLoader,
        ModDatabaseLoader modDatabaseLoader,
        IdDatabaseManager idDatabaseManager,
        CustomBulletsManager customBulletsManager,
        CustomWeaponsManager customWeaponsManager,
        CustomLocales customLocales,
        ModsCompatibility modsCompatibility,
        CustomTraderCreator customTraderCreator,
        ModDataStorage modDataStorage
    )
    {
        private readonly List<string> allVariantsAddedToQuests = [];
        private readonly Dictionary<string, List<string>> variantsAddedToQuests = [];
        private readonly Dictionary<string, List<string>> variantsAddedToTrader = new() { ["LL1"] = [], ["LL2"] = [], ["LL3"] = [], ["LL4"] = [] };
        private readonly List<string> currencies = ["Roubles", "Dollars", "Euros", "GPCoins"];
        public int questsGenerated = 0;
        public Dictionary<MongoId, Quest> AddedQuests { get; set; } = [];
        private TraderAssort? TraderAssort { get; set; } = null;
        private Dictionary<string, Dictionary<MongoId, MongoId>> QuestTraderAssort { get; set; } = [];

        public void Initialize()
        {
            TraderAssort = modDataStorage.Traders[modDatabaseLoader.TraderBase.Id].Assort;
            QuestTraderAssort = modDataStorage.Traders[modDatabaseLoader.TraderBase.Id].QuestAssort;
            RegisterQuestHandoverCategories();
        }

        public void AddBulletVariantToQuest(string id, string questId)
        {
            allVariantsAddedToQuests.Add(id);
            if (variantsAddedToQuests.TryGetValue(questId, out _))
            {
                variantsAddedToQuests[questId].Add(id);
            } else
            {
                if (variantsAddedToTrader.TryGetValue(questId, out var ll))
                    ll.Add(id);
                else
                    variantsAddedToQuests[questId] = [id];
            }
        }

        public void GenerateQuests()
        {
            var questsConfig = modDatabaseLoader.DbQuests;
            foreach (var (caliberName, questCategory) in questsConfig)
            {
                if (questCategory.Mod is not null && !modsCompatibility.ModCheck(questCategory.Mod)) continue;

                AddModdedBulletsToQuest(caliberName, questCategory);

                var questNumber = 0;
                foreach (var (questName, quest) in questCategory.Quests)
                {
                    questNumber++;
                    var newQuestId = idDatabaseManager.GetCustomId($"{questName}:ID");
                    Quest newQuest = new()
                    {
                        Id = newQuestId,
                        QuestName = questName,
                        Rewards = new Dictionary<string, List<Reward>>
                        {
                            ["Fail"] = [],
                            ["Started"] = [],
                            ["Success"] = []
                        },
                        Conditions = new QuestConditionTypes
                        {
                            Fail = [],
                            AvailableForStart = [],
                            AvailableForFinish = []
                        },
                        CanShowNotificationsInGame = true,
                        TraderId = modDatabaseLoader.TraderBase.Id,
                        Location = "any",
                        Image = $"/files/quest/icon/{ChooseCorrectQuestImage(caliberName, questName)}",
                        Type = QuestTypeEnum.Elimination,
                        Restartable = false,
                        Side = "Pmc",
                        SecretQuest = false,
                        InstantComplete = false,

                        AcceptPlayerMessage = $"{newQuestId} acceptPlayerMessage",
                        CompletePlayerMessage = $"{newQuestId} completePlayerMessage",
                        StartedMessageText = $"{newQuestId} startedMessageText",
                        SuccessMessageText = $"{newQuestId} successMessageText",
                        Description = $"{newQuestId} description",
                        Name = $"{newQuestId} name"
                    };
                    // TODO: CHECK MOD/DEFAULT
                    configLoader.QConfig.Difficulties.TryGetValue(quest.Diff, out var diff);
                    if (diff is null)
                    {
                        logger.LogWithColor($"[{GetType().Namespace}] Difficulty '{quest.Diff}' is missing in QuestConfig!", LogTextColor.Red);
                        continue;
                    }

                    customLocales.RegisterTag("questName", questCategory.Quests.Count > 1 ? $"{questCategory.Caliber.ShortName} {{Difficulty:{quest.Diff}:Name}}" : caliberName);
                    customLocales.RegisterTag("caliberName", questCategory.Caliber.Name);
                    var questDescription = "";
                    if (customLocales.KeyExistsInDefaultLang($"{questName}:Lore") && !configLoader.Config.EnableNonPonyMode)
                    {
                        questDescription += $"{{{questName}:Lore}}\n\n";
                    }

                    var bulletsInQuest = quest.Unlocks ?? [];
                    variantsAddedToQuests.TryGetValue(questName, out var variantsAdded);
                    if (variantsAdded is not null) bulletsInQuest.AddRange(variantsAdded);
                    questDescription += bulletsInQuest.Count > 0 ? $"{{Difficulty:{quest.Diff}:description}}" : $"{{Difficulty:{quest.Diff}:descriptionNoUnlock}}";
                    var questDescriptionUnlocks = string.Empty;
                    var questSuccessMessageText = string.Empty;
                    Dictionary<string, BulletsDatabase> unlocks = [];
                    foreach (var unlock in bulletsInQuest)
                    {
                        var bulletForUnlock = customBulletsManager.GetBulletByName(unlock);
                        if (bulletForUnlock != null)
                        {
                            unlocks.Add(bulletForUnlock.Id, bulletForUnlock);
                            var ammoDescription = $"{{{bulletForUnlock.Id} Name}} [{bulletForUnlock.DMG}/{bulletForUnlock.PEN}]";
                            questDescriptionUnlocks += $"· {ammoDescription}\n";
                            questSuccessMessageText += $"{ammoDescription}, ";
                        } else logger.LogWithColor($"[{GetType().Namespace}] Bullet '{unlock}' not found!", LogTextColor.Red);
                    }

                    if (customLocales.KeyExistsInDefaultLang($"{newQuestId} description"))
                    {
                        customLocales.AddLocale($"{newQuestId} description", $"{questDescription} {{{newQuestId} description}}");
                    }
                    else
                    {
                        customLocales.AddLocale($"{newQuestId} description", questDescription);
                    }

                    var amountOfAmmo = questCategory.Category == "Assault" ? 200 : (questCategory.Category == "Shotgun" ? 80 : (questCategory.Category == "Pistol" ? 400 : 5));
                    newQuest.Rewards.TryGetValue("Started", out List<Reward>? rewardsStarted); rewardsStarted ??= [];
                    newQuest.Rewards.TryGetValue("Success", out List<Reward>? rewardsSuccess); rewardsSuccess ??= [];
                    foreach (var (bulletId, bullet) in unlocks)
                    {
                        var reward = GenerateReward(questName, "Started", bulletId, Math.Ceiling((double)amountOfAmmo / unlocks.Count));
                        rewardsStarted.Add(reward);
                        var unlock = GenerateAssortUnlock(questName, bulletId, bullet);
                        rewardsSuccess.Add(unlock);
                    }

                    foreach (var (reward, amount) in diff.Rewards)
                    {
                        switch (reward)
                        {
                            case "MagicAmmoT1":
                                rewardsSuccess.Add(GenerateReward(questName, "Success", idDatabaseManager.GetCustomId("Eldritch Rounds:ID"), amount));
                                break;
                            case "MagicAmmoT2":
                                rewardsSuccess.Add(GenerateReward(questName, "Success", idDatabaseManager.GetCustomId("Mystic Rounds:ID"), amount));
                                break;
                            case "MagicAmmoT3":
                                rewardsSuccess.Add(GenerateReward(questName, "Success", idDatabaseManager.GetCustomId("Arcane Rounds:ID"), amount));
                                break;
                            case "Experience":
                                rewardsSuccess.Add(new Reward
                                {
                                    Id = idDatabaseManager.GetCustomId($"{questName}:SUCCESSREWARD:{reward}"),
                                    //Index = rewardsSuccess.Count,
                                    Type = SPTarkov.Server.Core.Models.Enums.RewardType.Experience,
                                    Value = amount
                                });
                                break;
                            case "TraderStanding":
                                rewardsSuccess.Add(new Reward
                                {
                                    Id = idDatabaseManager.GetCustomId($"{questName}:SUCCESSREWARD:{reward}"),
                                    //Index = rewardsSuccess.Count,
                                    Type = SPTarkov.Server.Core.Models.Enums.RewardType.TraderStanding,
                                    Value = amount,
                                    Target = modDatabaseLoader.TraderBase.Id
                                });
                                break;
                        }
                    }
                    if (quest.Trader > 1)
                    {
                        newQuest.Conditions.AvailableForStart.Add(new QuestCondition
                        {
                            Id = idDatabaseManager.GetCustomId($"{questName}:AFS:TraderNeeded"),
                            DynamicLocale = false,
                            ConditionType = "TraderLoyalty",
                            CompareMethod = ">=",
                            Target = new ListOrT<string>(null, modDatabaseLoader.TraderBase.Id),
                            Value = quest.Trader
                        });
                    }
                    var questForStartName = string.Empty;
                    if ((questNumber == 1 && questCategory.QuestForStart != "") || questNumber > 1)
                    {
                        questForStartName = questNumber == 1 ? questCategory.QuestForStart : $"{caliberName}Q{questNumber - 1}";
                        newQuest.Conditions.AvailableForStart.Add(new QuestCondition
                        {
                            Id = idDatabaseManager.GetCustomId($"{questName}:AFS:QuestForStart"),
                            DynamicLocale = false,
                            ConditionType = "Quest",
                            Status = [QuestStatusEnum.Success, QuestStatusEnum.Fail],
                            Target = new ListOrT<string>(null, idDatabaseManager.GetCustomId($"{questForStartName}:ID"))
                        });
                    }
                    List<string> visibilityConditionsIds = [];
                    foreach (var (req, amount) in diff.Requires)
                    {
                        customLocales.RegisterTag("value", amount.ToString());
                        switch (req)
                        {
                            case "AnyKills":
                            case "Kills":
                            case "Boss":
                            case "PMC":
                                var killAFF = CreateKillCondition($"{questName}:AFF:{req}", amount);

                                if (req != "AnyKills")
                                {
                                    var weaponsToKillWith = customWeaponsManager.GetWeaponIds(questCategory.Caliber.Id, ["ALL"], false, false);
                                    if (weaponsToKillWith.Count == 0)
                                    {
                                        logger.LogWithColor($"[{GetType().Namespace}] Kill conditions \"kill with weapon\" for quest {questName} can't find any weapons in database. Maybe \"{questCategory.Caliber.Id}\" caliber is incorrect?", LogTextColor.Yellow);
                                    }
                                    killAFF.Counter.Conditions.First().Weapon = [.. weaponsToKillWith];
                                }

                                if (req == "Boss")
                                {
                                    killAFF.Counter.Conditions.First().Target = new ListOrT<string>(null, "Savage");
                                    killAFF.Counter.Conditions.First().SavageRole = configLoader.QConfig.BossNames;
                                }
                                if (req == "PMC")
                                {
                                    killAFF.Counter.Conditions.First().Target = new ListOrT<string>(null, "AnyPmc");
                                }
                                newQuest.Conditions.AvailableForFinish.Add(killAFF);
                                customLocales.AddLocale(killAFF.Id, $"Requires{req}");

                                visibilityConditionsIds.Add(killAFF.Id);
                                break;
                            case "Mastery":
                                var weaponCountM = customWeaponsManager.GetWeaponIds(questCategory.Caliber.Id, ["ALL"], false, false, true).Count;
                                var categories = WeaponCategories.GetAllPlural();
                                foreach (var cat in categories)
                                {
                                    var weaponsInCat = customWeaponsManager.GetWeaponIds(questCategory.Caliber.Id, [cat], false, false, true);
                                    if (weaponsInCat.Count == 0) continue;
                                    var killsAmount = Math.Ceiling(amount * ((float)weaponsInCat.Count / (float)weaponCountM));
                                    var killAFFMastery = CreateKillCondition($"{questName}:AFF:{req}:{cat}", killsAmount);
                                    killAFFMastery.Counter.Conditions.First().Weapon = [.. customWeaponsManager.GetWeaponIds(questCategory.Caliber.Id, [cat])];
                                    newQuest.Conditions.AvailableForFinish.Add(killAFFMastery);

                                    customLocales.RegisterTag("category", $"{{WeaponCategory.{cat}}}");
                                    customLocales.RegisterTag("value", killsAmount.ToString());
                                    customLocales.AddLocale(killAFFMastery.Id, $"Requires{req}");

                                    visibilityConditionsIds.Add(killAFFMastery.Id);
                                }
                                break;
                            case "Collector":
                                var collectorWeapons = customWeaponsManager.GetWeaponIds(questCategory.Caliber.Id, ["ALL"], false, false, true);
                                foreach (var weap in collectorWeapons)
                                {
                                    var allWeapons = new List<MongoId> { weap };
                                    allWeapons.AddRange(customWeaponsManager.GetAllCopiesFromBaseWeapon(weap));
                                    var killsAmount = Math.Ceiling((1f / (float)collectorWeapons.Count) * amount);
                                    var killAFFCollector = CreateKillCondition($"{questName}:AFF:{req}:{weap}", killsAmount);
                                    killAFFCollector.Counter.Conditions.First().Weapon = [.. allWeapons.Select(id => id.ToString())];
                                    newQuest.Conditions.AvailableForFinish.Add(killAFFCollector);
                                    customLocales.RegisterTag("weapon", $"{{{weap} ShortName}}");
                                    customLocales.RegisterTag("value", killsAmount.ToString());
                                    customLocales.AddLocale(killAFFCollector.Id, $"Requires{req}");
                                    visibilityConditionsIds.Add(killAFFCollector.Id);
                                }
                                break;
                            default:
                                configLoader.QConfig.RequiresCategories.TryGetValue(req, out var category); category ??= [];
                                if (category.Count == 0) logger.LogWithColor($"[{GetType().Namespace}] Category {req} not found in questConfig!", LogTextColor.Red);
                                var barterId = idDatabaseManager.GetCustomId($"{questName}:AFF:{req}");
                                newQuest.Conditions.AvailableForFinish.Add(new QuestCondition
                                {
                                    Id = barterId,
                                    DynamicLocale = false,
                                    ConditionType = "HandoverItem",
                                    OnlyFoundInRaid = !currencies.Contains(req) && configLoader.Config.QuestBarterFoundInRaid,
                                    Target = new ListOrT<string>(category, null),
                                    Value = amount,
                                    VisibilityConditions = GenerateVisibilityConditions(visibilityConditionsIds, questName, req)
                                });
                                customLocales.AddToExistingLocale($"{newQuestId} description", $"· {amount} {{Category{req}}}\n");
                                customLocales.AddLocale(barterId, $"Requires{req}");
                                break;
                        }
                    }
                    
                    // LOCALE PART
                    customLocales.AddToExistingLocale($"{newQuestId} description", $"\n\n<b>{{Difficulty:{quest.Diff}:descriptionPart2}}</b>\n{questDescriptionUnlocks}");

                    customLocales.AddLocale($"{newQuestId} name", $"questName");
                    customLocales.AddLocale($"{newQuestId} startedMessageText", $"Difficulty:{quest.Diff}:startedMessageText");
                    customLocales.AddLocale($"{newQuestId} acceptPlayerMessage", $"Difficulty:{quest.Diff}:acceptPlayerMessage");
                    customLocales.AddLocale($"{newQuestId} completePlayerMessage", $"Difficulty:{quest.Diff}:completePlayerMessage");

                    if (questSuccessMessageText != string.Empty)
                        customLocales.AddLocale($"{newQuestId} successMessageText", $"{{Difficulty:{quest.Diff}:successMessageText}}. {{Difficulty:{quest.Diff}:descriptionPart2}} {questSuccessMessageText[..^2]}" , true);
                    else
                        customLocales.AddLocale($"{newQuestId} successMessageText", $"Difficulty:{quest.Diff}:successMessageText");

                    if (questForStartName != string.Empty)
                    {
                        var questForStartId = idDatabaseManager.GetCustomId($"{questForStartName}:ID");
                        if (customLocales.KeyExistsInDefaultLang($"{questForStartId} description"))
                        {
                            if (!customLocales.DefaultLangLocaleContainsText($"{questForStartId} description", "(LL:"))
                            {
                                customLocales.AddToExistingLocale($"{questForStartId} description", $"{{Difficulty:{quest.Diff}:questUnlock}}· {{questName}} (LL:{quest.Trader})\n");
                            }
                            else
                            {
                                customLocales.AddToExistingLocale($"{questForStartId} description", $"· {{questName}} (LL:{quest.Trader})\n");
                            }
                        }
                        else
                        {
                            customLocales.AddLocale($"{questForStartId} description", $"· {{questName}} (LL:{quest.Trader})\n");
                        }
                    }
                    questsGenerated++;
                    modDataStorage.Quests.Add(newQuest.Id, newQuest);
                    if (configLoader.Config.DebugFiles.Quests)
                        AddedQuests.Add(newQuest.Id, newQuest);
                }
            }
            customLocales.RegisterLocales();
            foreach (var (ll, bullets) in variantsAddedToTrader)
            {
                var loyalLevel = Int32.Parse(ll.Replace("LL", ""));
                foreach (var bullet in bullets)
                {
                    var bulletForUnlock = customBulletsManager.GetBulletByName(bullet);
                    if (bulletForUnlock is not null)
                        GenerateAssortForVariantBullet(bullet, bulletForUnlock, loyalLevel);
                }  
            }
        }

        private Reward GenerateReward(string questName, string type, string itemId, double amount)
        {
            Reward reward = new()
            {
                FindInRaid = false,
                Id = idDatabaseManager.GetCustomId($"{questName}:{type}REWARD:{itemId}:1"),
                //Index = rewardsStarted.Count,
                Items = [
                    new Item {
                        Id = idDatabaseManager.GetCustomId($"{questName}:{type}REWARD:{itemId}:2"),
                        Template = itemId,
                        Upd = new Upd
                        {
                            StackObjectsCount = amount
                        }
                    }
                ],
                Target = idDatabaseManager.GetCustomId($"{questName}:{type}REWARD:{itemId}:2"),
                Type = (SPTarkov.Server.Core.Models.Enums.RewardType?)2,
                Value = amount
            };
            return reward;
        }

        private string GenerateAssortForVariantBullet(string itemId, BulletsDatabase bullet, int loyalLevel = 1)
        {
            var idForAssort = idDatabaseManager.GetCustomId($"QUESTASSORT:{itemId}");
            TraderAssort.Items ??= [];
            TraderAssort.Items.Add(new Item
            {
                Id = idForAssort,
                Template = itemId,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = configLoader.Config.AmmoPrice.UnlimitedCount,
                    StackObjectsCount = configLoader.Config.AmmoPrice.Max
                }
            });
            TraderAssort.BarterScheme ??= [];
            TraderAssort.BarterScheme.Add(idForAssort, [[new BarterScheme {
                Template = "5449016a4bdc2d6f028b456f",
                Count = bullet.NewPrice
            }]]);
            TraderAssort.LoyalLevelItems ??= [];
            TraderAssort.LoyalLevelItems.Add(idForAssort, loyalLevel);
            return idForAssort;
        }



        private Reward GenerateAssortUnlock(string questName, string itemId, BulletsDatabase bullet)
        {
            var idForAssort = GenerateAssortForVariantBullet(itemId, bullet);

            var newQuestId = idDatabaseManager.GetCustomId($"{questName}:ID");

            QuestTraderAssort.TryGetValue("success", out var questTraderAssortSuccess);
            if (questTraderAssortSuccess is null)
            {
                QuestTraderAssort.Add("success", []);
                QuestTraderAssort.TryGetValue("success", out questTraderAssortSuccess);
            }
            questTraderAssortSuccess?.Add(idForAssort, newQuestId);
            
            Reward reward = new()
            {
                Id = idDatabaseManager.GetCustomId($"{questName}:UNLOCK:{itemId}:1"),
                //Index = rewardsStarted.Count,
                Items = [
                    new Item {
                        Id = idDatabaseManager.GetCustomId($"{questName}:UNLOCK:{itemId}:2"),
                        Template = itemId
                    }
                ],
                LoyaltyLevel = 1,
                Target = idDatabaseManager.GetCustomId($"{questName}:UNLOCK:{itemId}:2"),
                TraderId = modDatabaseLoader.TraderBase.Id,
                Type = (SPTarkov.Server.Core.Models.Enums.RewardType?)7
            };
            return reward;
        }
        
        private QuestCondition CreateKillCondition(string customIdStart, double amount)
        {
            var condition = new QuestCondition
            {
                Counter = new QuestConditionCounter
                {
                    Conditions = [
                        new QuestConditionCounterCondition {
                            ConditionType = "Kills",
                            CompareMethod = ">=",
                            Id = idDatabaseManager.GetCustomId($"{customIdStart}:1"),
                            Target = new ListOrT<string>(null, "Any")
                        }
                    ],
                    Id = idDatabaseManager.GetCustomId($"{customIdStart}:2")
                },
                Id = idDatabaseManager.GetCustomId($"{customIdStart}:3"),
                DynamicLocale = false,
                ConditionType = "CounterCreator",
                Type = "Elimination",
                Value = amount
            };

            return condition;
        }

        private List<VisibilityCondition> GenerateVisibilityConditions(List<string> ids, string questName, string categoryName)
        {
            var visConditions = new List<VisibilityCondition>();
            foreach (var id in ids)
            {
                visConditions.Add(
                    new VisibilityCondition
                    {
                        ConditionType = "CompleteCondition",
                        Id = idDatabaseManager.GetCustomId($"{questName}:AFFVIS:{categoryName}:{id}"),
                        Target = id
                    }
                );
            }
            return visConditions;
        }

        private string ChooseCorrectQuestImage(string caliberName, string questName)
        {
            var qImages = customTraderCreator.questImages;
            if (configLoader.Config.EnableNonPonyMode)
            {
                if(qImages.Contains($"{caliberName}NP")) return $"{caliberName}NP";
                return $"IntroductionNP";
            }
            if (qImages.Contains(questName)) return questName;
            return "Introduction";
        }

        private void AddModdedBulletsToQuest(string questInternalId, Interfaces.QuestData questCategory)
        {
            if (questCategory.Caliber is null) return;
            customBulletsManager.BulletsInCaliber.TryGetValue(questCategory.Caliber.Id, out var bulletsInCaliber);
            if (bulletsInCaliber is null)
            {
                if (configLoader.Config.Debug)
                    logger.LogWithColor($"[{GetType().Namespace}] Quest Tree {questInternalId} does not have any valid unlocks or caliber is incorrect", LogTextColor.Yellow);
                return;
            }
            var maxQuests = questCategory.Quests.Count;
            if (maxQuests <= 1) return; 
            var bulletsInQuests = new List<string>();
            var ratingsInQuests = new Dictionary<string, List<double>>();
            foreach (var (questId, quest) in questCategory.Quests)
            {
                var categoryRatings = new Dictionary<string, double>();
                foreach(var bullet in quest.Unlocks)
                {
                    var bulletDb = customBulletsManager.GetBulletByName(bullet);
                    if (bulletDb is null) continue;
                    bulletsInQuests.Add(bulletDb.Id);
                    ratingsInQuests.TryGetValue(bulletDb.Category, out var ratingsInQuest);
                    if (ratingsInQuest is null)
                        ratingsInQuests.Add(bulletDb.Category, [bulletDb.Rating]);
                    else
                        ratingsInQuest.Add(bulletDb.Rating);
                }
            }
            var moddedBullets = bulletsInCaliber.Where(e => !(allVariantsAddedToQuests.Contains(e.ToString()) || bulletsInQuests.Contains(e.ToString()))).ToHashSet();

            foreach (var bullet in moddedBullets)
            {
                customBulletsManager.bullets.TryGetValue(bullet, out var bulletDb);
                if (bulletDb is null) continue;
                ratingsInQuests.TryGetValue(bulletDb.Category, out var ratings);
                if (ratings is null)
                {
                    if (configLoader.Config.Debug)
                        logger.LogWithColor($"[{GetType().Namespace}] {questInternalId}: Can't determinate to what quest add {bulletDb.Name} as it is different category ({bulletDb.Category}) than the other bullets", LogTextColor.Yellow);
                    continue;
                }
                var ratingAdjusted = (bulletDb.Rating - ratings.Min()) / ratings.Max();
                var questNumber = Math.Clamp((int)(ratingAdjusted * (maxQuests - 1)), 0, maxQuests - 1);
                if (configLoader.Config.Debug)
                    logger.LogWithColor($"[{GetType().Namespace}] {questInternalId}: Added {bulletDb.Name} bullet of {bulletDb.Category} category to {questNumber} quest", LogTextColor.Magenta, LogBackgroundColor.White);
                AddBulletVariantToQuest(bullet, questCategory.Quests.Keys.ElementAt(questNumber));
            }
        }

        private void RegisterQuestHandoverCategories()
        {
            var notChangeCategories = new List<string>(["TarkoPops"]);
            foreach (var (category, items) in configLoader.QConfig.RequiresCategories)
            {
                if (currencies.Contains(category) || notChangeCategories.Contains(category)) continue;
                var itemNames = new List<string>();
                foreach (var item in items) {
                    itemNames.Add($"{{{item} Name}}");
                }
                customLocales.AddLocale($"Category{category}", $"<b>{{Category{category}}}</b>: {string.Join(", ", itemNames)}");
            }
        }
    }
}
