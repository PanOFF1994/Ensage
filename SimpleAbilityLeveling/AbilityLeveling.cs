﻿namespace SimpleAbilityLeveling
{
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Objects;
    using Ensage.Common.Objects.UtilityObjects;

    using SharpDX;

    internal class AbilityLeveling
    {
        #region Fields

        private IEnumerable<Ability> abilities;

        private AbilityBuilder abilitylBuilder;

        private Hero hero;

        private MenuManager menuManager;

        private Sleeper sleeper;

        #endregion

        #region Public Methods and Operators

        public void OnClose()
        {
            menuManager.OnClose();
        }

        public void OnDraw()
        {
            if (!menuManager.IsOpen)
            {
                return;
            }

            var build = abilitylBuilder.GetBestBuild().ToList();
            var uniqueAbilities =
                build.GroupBy(x => x)
                    .Select(x => x.First())
                    .OrderBy(x => x.AbilitySlot)
                    .Select(x => x.StoredName())
                    .ToList();

            var ratio = HUDInfo.RatioPercentage();
            var xStart = HUDInfo.ScreenSizeX() * 0.35f;
            var yStart = HUDInfo.ScreenSizeY() * 0.55f;

            var text = "Auto build preview (Win rate: " + abilitylBuilder.BestBuildWinRate + ")";

            Drawing.DrawRect(
                new Vector2(xStart - 2, yStart - 35 * ratio),
                new Vector2(
                    Drawing.MeasureText(text, "Arial", new Vector2(35 * ratio), FontFlags.None).X + 2,
                    35 * ratio),
                new Color(75, 75, 75, 175),
                false);
            Drawing.DrawRect(
                new Vector2(xStart - 2, yStart),
                new Vector2((build.Count + 1) * 48 * ratio + 2, uniqueAbilities.Count * 40 * ratio),
                new Color(75, 75, 75, 175),
                false);
            Drawing.DrawText(
                text,
                "Arial",
                new Vector2(xStart, yStart - 35 * ratio),
                new Vector2(35 * ratio),
                Color.Orange,
                FontFlags.None);

            var positions = new Dictionary<string, float>();
            for (var i = 0; i < uniqueAbilities.Count; i++)
            {
                Drawing.DrawRect(
                    new Vector2(xStart, yStart + i * 40 * ratio),
                    new Vector2(45 * ratio, 40 * ratio),
                    Textures.GetSpellTexture(uniqueAbilities[i]));
                positions.Add(uniqueAbilities[i], xStart + i * 40 * ratio - 63 * ratio);
                Drawing.DrawRect(
                    new Vector2(xStart - 2, yStart - 2 + i * 40 * ratio),
                    new Vector2((build.Count + 1) * 48 * ratio, 2),
                    Color.Silver);
            }

            Drawing.DrawRect(
                new Vector2(xStart - 2, yStart - 2 + uniqueAbilities.Count * 40 * ratio),
                new Vector2((build.Count + 1) * 48 * ratio, 2),
                Color.Silver);

            for (var i = 0; i < build.Count; i++)
            {
                var number = (i + 1).ToString();
                var size = Drawing.MeasureText(number, "Arial", new Vector2(35 * ratio), FontFlags.None);
                Drawing.DrawText(
                    number,
                    "Arial",
                    new Vector2(
                        xStart + 45 * ratio + i * 48 * ratio + (48 * ratio - size.X) / 2,
                        positions[build[i].StoredName()]),
                    new Vector2(35 * ratio),
                    Color.White,
                    FontFlags.None);
                Drawing.DrawRect(
                    new Vector2(xStart - 2 + i * 48 * ratio, yStart - 2),
                    new Vector2(2, uniqueAbilities.Count * 40 * ratio),
                    Color.Silver);
            }

            Drawing.DrawRect(
                new Vector2(xStart - 2 + build.Count * 48 * ratio, yStart - 2),
                new Vector2(2, uniqueAbilities.Count * 40 * ratio),
                Color.Silver);
            Drawing.DrawRect(
                new Vector2(xStart - 2 + (build.Count + 1) * 48 * ratio, yStart - 2),
                new Vector2(2, uniqueAbilities.Count * 40 * ratio + 2),
                Color.Silver);
        }

        public void OnLoad()
        {
            hero = ObjectManager.LocalHero;
            abilities = hero.Spellbook.Spells.Where(x => !x.IsHidden && !IgnoredAbilities.List.Contains(x.StoredName()));
            menuManager = new MenuManager(abilities.Select(x => x.StoredName()).ToList(), hero.Name);
            sleeper = new Sleeper();
            abilitylBuilder = new AbilityBuilder(hero);

            sleeper.Sleep(10000);
        }

        public void OnUpdate()
        {
            if (sleeper.Sleeping)
            {
                return;
            }

            sleeper.Sleep(1000);

            if (hero.AbilityPoints <= 0 || Game.IsPaused)
            {
                return;
            }

            if (menuManager.IsEnabledAuto)
            {
                var ability = abilitylBuilder.GetAbility(hero.Level);

                if (ability == null || ability.IsHidden)
                {
                    return;
                }

                if (!IsLearnable(ability))
                {
                    ability = abilities.FirstOrDefault(IsLearnable);
                }

                Player.UpgradeAbility(hero, ability);
            }
            else if (menuManager.IsEnabledManual && !menuManager.IsLevelIgnored(hero.Level))
            {
                var learnableAbilities =
                    abilities.OrderByDescending(x => menuManager.GetAbilityPriority(x.StoredName()))
                        .Where(
                            x => menuManager.AbilityActive(x.StoredName()) && IsLearnable(x) // x.IsLearnable
                        ).ToList();

                var upgrade = learnableAbilities.FirstOrDefault(ForceLearn)
                              ?? learnableAbilities.FirstOrDefault(x => !IsLocked(x, learnableAbilities));

                Player.UpgradeAbility(hero, upgrade ?? learnableAbilities.FirstOrDefault());
            }
        }

        #endregion

        #region Methods

        private bool ForceLearn(Ability ability)
        {
            var forceHeroLevel = menuManager.ForceAbilityLearnHeroLevel(ability.StoredName());

            if (forceHeroLevel <= 0 || ability.Level >= 1)
            {
                return false;
            }

            return hero.Level >= forceHeroLevel;
        }

        private bool IsLearnable(Ability ability)
        {
            var abilityLevel = ability.Level;
            var heroLevel = hero.Level;

            switch (ability.AbilityType)
            {
                case AbilityType.Ultimate:
                    if (hero.ClassID == ClassID.CDOTA_Unit_Hero_Invoker)
                    {
                        abilityLevel--;
                    }
                    if (abilityLevel >= 3)
                    {
                        return false;
                    }
                    switch (hero.ClassID)
                    {
                        case ClassID.CDOTA_Unit_Hero_Meepo:
                            switch (abilityLevel)
                            {
                                case 0:
                                    if (heroLevel < 3)
                                    {
                                        return false;
                                    }
                                    break;
                                case 1:
                                    if (heroLevel < 10)
                                    {
                                        return false;
                                    }
                                    break;
                                case 2:
                                    if (heroLevel < 17)
                                    {
                                        return false;
                                    }
                                    break;
                            }
                            break;
                        default:
                            switch (abilityLevel)
                            {
                                case 0:
                                    if (heroLevel < 6)
                                    {
                                        return false;
                                    }
                                    break;
                                case 1:
                                    if (heroLevel < 11)
                                    {
                                        return false;
                                    }
                                    break;
                                case 2:
                                    if (heroLevel < 16)
                                    {
                                        return false;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                default:
                    // ability.MaximumLevel
                    var maxLevel = ability.AbilityType == AbilityType.Attribute
                                       ? 10
                                       : hero.ClassID == ClassID.CDOTA_Unit_Hero_Invoker ? 7 : 4;

                    if (abilityLevel >= maxLevel || heroLevel <= abilityLevel * 2)
                    {
                        return false;
                    }

                    break;
            }
            return true;
        }

        private bool IsLocked(Ability ability, IEnumerable<Ability> learnableAbilities)
        {
            var lockLevel = menuManager.AbilityLockLevel(ability.StoredName());

            if (lockLevel <= 0)
            {
                return false;
            }

            var abilityLevel = ability.Level;

            var otherNotLockedAbility =
                learnableAbilities.Any(
                    x =>
                    !x.Equals(ability) && x.AbilityType != AbilityType.Attribute
                    && (menuManager.AbilityLockLevel(x.StoredName()) == 0
                        || menuManager.AbilityLockLevel(x.StoredName()) > x.Level)
                    && !menuManager.AbilityFullyLocked(x.StoredName()));

            return abilityLevel >= lockLevel
                   && (otherNotLockedAbility || menuManager.AbilityFullyLocked(ability.StoredName()));
        }

        #endregion
    }
}