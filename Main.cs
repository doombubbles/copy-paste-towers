using System;
using Assets.Scripts.Models.Towers;
using Assets.Scripts.Simulation.SMath;
using Assets.Scripts.Simulation.Towers;
using Assets.Scripts.Unity;
using Assets.Scripts.Unity.UI_New.InGame;
using Assets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using HarmonyLib;
using MelonLoader;
using NinjaKiwi.Common;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = Assets.Scripts.Simulation.SMath.Vector3;

[assembly: MelonInfo(typeof(CopyPasteTowers.Main), "Copy/Paste Towers", "1.0.0", "doombubbles")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace CopyPasteTowers
{
    public class Main : BloonsTD6Mod
    {
        private static TowerModel clipboard;
        private static double cost;
        private static bool payForIt;
        private static bool justPastedTower;

        public override void OnUpdate()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.C) &&
                    TowerSelectionMenu.instance != null &&
                    TowerSelectionMenu.instance.selectedTower != null)
                {
                    CopyTower(TowerSelectionMenu.instance.selectedTower.tower);
                }

                if (Input.GetKeyDown(KeyCode.V) || justPastedTower)
                {
                    PasteTower();
                }
            }

            justPastedTower = false;
        }

        private static void CopyTower(Tower tower)
        {
            var inGameModel = InGame.instance.GetGameModel();
            clipboard = inGameModel.GetTowerWithName(tower.towerModel.name);

            cost = CalculateCost(clipboard);

            var name = LocalizationManager.Instance.GetText(tower.towerModel.baseId);
            Game.instance.ShowMessage($"Copied {name}\n\nTotal Cost is ${(int)cost}");
        }

        private static double CalculateCost(TowerModel towerModel, Vector3 pos = default)
        {
            var inGameModel = InGame.instance.GetGameModel();
            var towerManager = InGame.instance.GetTowerManager();

            var total = 0.0;

            var discountMult = 0f;
            if (pos != default)
            {
                var zoneDiscount = towerManager.GetZoneDiscount(pos, 0, 0);
                discountMult = towerManager.GetDiscountMultiplier(zoneDiscount);
            }

            total += (1 - discountMult) * towerModel.cost;

            foreach (var appliedUpgrade in towerModel.appliedUpgrades)
            {
                var upgrade = inGameModel.GetUpgrade(appliedUpgrade);

                discountMult = 0f;
                if (pos != default)
                {
                    var zoneDiscount = towerManager.GetZoneDiscount(pos, upgrade.path, upgrade.tier);
                    discountMult = towerManager.GetDiscountMultiplier(zoneDiscount);
                }

                total += upgrade.cost * (1 - discountMult);
            }

            return total;
        }

        private static double CalculateCost(Tower tower)
        {
            return CalculateCost(tower.towerModel, tower.Position);
        }

        private static void PasteTower()
        {
            var inputManager = InGame.instance.InputManager;
            if (inputManager.inPlacementMode || InGame.instance.GetCash() < cost)
            {
                return;
            }

            inputManager.EnterPlacementMode(clipboard, new Action<Vector2>(pos =>
            {
                try
                {
                    payForIt = true;
                    inputManager.CreatePlacementTower(pos);
                    justPastedTower = true;
                }
                catch (Exception e)
                {
                    MelonLogger.Error(e);
                }
                finally
                {
                    payForIt = false;
                }
            }));
        }

        [HarmonyPatch(typeof(TowerManager.TowerCreateDef), nameof(TowerManager.TowerCreateDef.Invoke))]
        internal class TowerManager_CreateTower
        {
            [HarmonyPostfix]
            internal static void Postfix(Tower tower)
            {
                if (payForIt)
                {
                    tower.worth = (float) CalculateCost(tower);
                    payForIt = false;
                }
            }
        }
    }
}