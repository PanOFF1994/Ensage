﻿namespace GoldSpender.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Objects;

    using global::GoldSpender.Utils;

    internal class NearDeath : GoldManager
    {
        #region Fields

        private readonly Team enemyTeam;

        #endregion

        #region Constructors and Destructors

        public NearDeath()
        {
            enemyTeam = Hero.GetEnemyTeam();
        }

        #endregion

        #region Public Methods and Operators

        public override void BuyItems()
        {
            var enabledItems =
                Variables.ItemsToBuyNearDeath.OrderByDescending(x => Menu.GetNearDeathItemPriority(x.Key))
                    .Where(x => Menu.IsNearDeathItemActive(x.Key))
                    .Select(x => x.Value)
                    .ToList();

            if (enabledItems.Contains(0))
            {
                enabledItems.InsertRange(enabledItems.FindIndex(x => x == 0), Player.QuickBuyItems);
                enabledItems.Remove(0);
            }

            var itemsToBuy = new List<Tuple<Unit, uint>>();
            var unreliableGold = UnreliableGold;
            var reliableGold = ReliableGold;

            if (ShouldSaveForBuyback(Menu.NearDeathSaveBuybackTime))
            {
                SaveBuyBackGold(out reliableGold, out unreliableGold);
            }

            var courier = ObjectManager.GetEntities<Courier>().FirstOrDefault(x => x.Team != enemyTeam && x.IsAlive);

            foreach (var itemID in enabledItems)
            {
                Unit unit;

                if (ItemsData.IsPurchasable(itemID, Hero.ActiveShop))
                {
                    unit = Hero;
                }
                else if (courier != null && ItemsData.IsPurchasable(itemID, courier.ActiveShop))
                {
                    unit = courier;
                }
                else
                {
                    continue;
                }

                switch (itemID)
                {
                    case 42: // observer
                    case 43: // sentry
                        if (GetWardsCount(itemID) >= 2)
                        {
                            continue;
                        }
                        break;
                    case 46: // teleport scroll
                        if (GetItemCount(itemID) >= 2 || Hero.FindItem("item_travel_boots", true) != null
                            || Hero.FindItem("item_travel_boots_2", true) != null)
                        {
                            continue;
                        }
                        break;
                    case 59: // energy booster
                        if (Hero.FindItem("item_arcane_boots") != null)
                        {
                            continue;
                        }
                        break;
                    case 188: // smoke
                        if (GetItemCount(itemID) >= 1)
                        {
                            continue;
                        }
                        break;
                }

                var cost = Ability.GetAbilityDataByID(itemID).Cost;

                if (unreliableGold >= cost)
                {
                    itemsToBuy.Add(Tuple.Create(unit, itemID));
                    unreliableGold -= (int)cost;
                }
                else if (reliableGold + unreliableGold >= cost)
                {
                    itemsToBuy.Add(Tuple.Create(unit, itemID));
                    break;
                }
            }

            if (!Hero.IsAlive)
            {
                return;
            }

            itemsToBuy.ForEach(x => Player.BuyItem(x.Item1, x.Item2));

            if (itemsToBuy.Any())
            {
                Sleeper.Sleep(20000);
            }
        }

        public override bool ShouldSpendGold()
        {
            if (Sleeper.Sleeping)
            {
                return false;
            }

            if (!Menu.NearDeathEnabled || GoldLossOnDeath <= 20)
            {
                return false;
            }

            var distance = Menu.NearDeathEnemyDistance;

            return Hero.Health <= Menu.NearDeathHpThreshold
                   && (distance <= 0
                       || Heroes.GetByTeam(enemyTeam)
                              .Any(x => !x.IsIllusion && x.IsAlive && x.Distance2D(Hero) <= distance));
        }

        #endregion

        #region Methods

        private uint GetItemCount(uint itemId)
        {
            return (Hero.Inventory.Items.FirstOrDefault(x => x.ID == itemId)?.CurrentCharges ?? 0)
                   + (Hero.Inventory.StashItems.FirstOrDefault(x => x.ID == itemId)?.CurrentCharges ?? 0);
        }

        private uint GetWardsCount(uint itemId)
        {
            var inventoryDispenser = Hero.Inventory.Items.FirstOrDefault(x => x.ID == 218);
            var stashDispenser = Hero.Inventory.StashItems.FirstOrDefault(x => x.ID == 218);

            return GetItemCount(itemId)
                   + (itemId == 42
                          ? (inventoryDispenser?.CurrentCharges ?? 0) + (stashDispenser?.CurrentCharges ?? 0)
                          : (inventoryDispenser?.SecondaryCharges ?? 0) + (stashDispenser?.SecondaryCharges ?? 0));
        }

        #endregion
    }
}