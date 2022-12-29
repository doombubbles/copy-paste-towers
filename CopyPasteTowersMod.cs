using System;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Simulation.Input;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using CopyPasteTowers;
using HarmonyLib;
using Il2CppNinjaKiwi.Common;
using MelonLoader;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = Il2CppAssets.Scripts.Simulation.SMath.Vector3;

[assembly: MelonInfo(typeof(CopyPasteTowersMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace CopyPasteTowers;

public class CopyPasteTowersMod : BloonsTD6Mod
{
    private static TowerModel? clipboard;
    private static double cost;
    private static int payForIt;
    private static bool justPastedTower;
    private static bool lastCopyWasCut;

    private static readonly ModSettingHotkey CopyHotkey = new(KeyCode.C, HotkeyModifier.Ctrl);
    private static readonly ModSettingHotkey PasteHotkey = new(KeyCode.V, HotkeyModifier.Ctrl);
    private static readonly ModSettingHotkey CutHotkey = new(KeyCode.X, HotkeyModifier.Ctrl);

    public override void OnLateUpdate()
    {
        if (!InGame.instance)
        {
            return;
        }

        if (TowerSelectionMenu.instance)
        {
            var selectedTower = TowerSelectionMenu.instance.selectedTower;
            if (selectedTower is { IsParagon: false } && !selectedTower.tower.towerModel.IsHero())
            {
                lastCopyWasCut = CutHotkey.JustPressed();
                if (CutHotkey.JustPressed() || CopyHotkey.JustPressed())
                {
                    CopyTower(selectedTower.tower);

                    if (CutHotkey.JustPressed())
                    {
                        TowerSelectionMenu.instance.Sell();
                        lastCopyWasCut = true;
                    }
                    else
                    {
                        lastCopyWasCut = false;
                    }
                }
            }
        }

        if (PasteHotkey.JustPressed() || justPastedTower && (PasteHotkey.IsPressed() || Input.GetKey(KeyCode.LeftShift)))
        {
            PasteTower();
        }

        justPastedTower = false;
        if (--payForIt < 0) payForIt = 0;
    }

    private static void CopyTower(Tower tower)
    {
        var inGameModel = InGame.instance.GetGameModel();
        clipboard = inGameModel.GetTowerWithName(tower.towerModel.name);

        cost = CalculateCost(clipboard);

        var name = LocalizationManager.Instance.GetText(tower.towerModel.name);
        Game.instance.ShowMessage($"Copied {name}\n\nTotal Cost is ${(int) cost}");
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

    private static double CalculateCost(Tower tower) => CalculateCost(tower.towerModel, tower.Position);

    private static void PasteTower()
    {
        var inputManager = InGame.instance.InputManager;
        if (clipboard == null || inputManager.IsInPlacementMode() || InGame.instance.GetCash() < cost)
        {
            return;
        }

        inputManager.EnterPlacementMode(clipboard, new Action<Vector2>(pos =>
        {
            try
            {
                payForIt = 30;
                inputManager.CreatePlacementTower(pos);
            }
            catch (Exception e)
            {
                ModHelper.Error<CopyPasteTowersMod>(e);
            }
        }), new ObjectId {data = (uint) InGame.instance.UnityToSimulation.GetInputId()});
    }

    [HarmonyPatch(typeof(Tower), nameof(Tower.OnPlace))]
    internal static class Tower_OnPlace
    {
        [HarmonyPrefix]
        private static void Prefix(Tower __instance)
        {
            if (payForIt > 0)
            {
                __instance.worth = (float) CalculateCost(__instance);
                InGame.instance.AddCash(-__instance.worth + __instance.towerModel.cost);
                payForIt = 0;
                justPastedTower = true;

                if (lastCopyWasCut)
                {
                    var tts = __instance.GetTowerToSim();
                    TowerSelectionMenu.instance.SelectTower(tts);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TowerInventory), nameof(TowerInventory.HasUpgradeInventory))]
    internal static class TowerInventory_HasUpgradeInventory
    {
        [HarmonyPostfix]
        private static void Postfix(TowerModel def, ref bool __result)
        {
            if (__result == false && def.name == clipboard?.name && ModHelper.HasMod("Unlimited5thTiers"))
            {
                __result = true;
            }
        }
    }
}