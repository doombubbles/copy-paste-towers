using System;
using Assets.Scripts.Models.Towers;
using Assets.Scripts.Simulation.SMath;
using Assets.Scripts.Simulation.Towers;
using Assets.Scripts.Unity;
using Assets.Scripts.Unity.UI_New.InGame;
using Assets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using CopyPasteTowers;
using HarmonyLib;
using MelonLoader;
using NinjaKiwi.Common;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = Assets.Scripts.Simulation.SMath.Vector3;

[assembly: MelonInfo(typeof(CopyPasteTowersMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace CopyPasteTowers;

public class CopyPasteTowersMod : BloonsTD6Mod
{
    private static TowerModel clipboard;
    private static double cost;
    private static bool payForIt;
    private static bool justPastedTower;
    private static bool lastCopyWasCut;

    private static readonly ModSettingHotkey CopyHotkey = new ModSettingHotkey(KeyCode.C, HotkeyModifier.Ctrl);
    private static readonly ModSettingHotkey PasteHotkey = new ModSettingHotkey(KeyCode.V, HotkeyModifier.Ctrl);
    private static readonly ModSettingHotkey CutHotkey = new ModSettingHotkey(KeyCode.X, HotkeyModifier.Ctrl);

    public override void OnUpdate()
    {
        if (!InGame.instance)
        {
            return;
        }

        if (TowerSelectionMenu.instance)
        {
            var selectedTower = TowerSelectionMenu.instance.selectedTower;
            if (selectedTower != null && !selectedTower.IsParagon && !selectedTower.tower.towerModel.IsHero())
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

        if (PasteHotkey.JustPressed() || justPastedTower && PasteHotkey.IsPressed())
        {
            PasteTower();
        }

        justPastedTower = false;
    }

    private static void CopyTower(Tower tower)
    {
        var inGameModel = InGame.instance.GetGameModel();
        clipboard = inGameModel.GetTowerWithName(tower.towerModel.name);

        cost = CalculateCost(clipboard);

        var name = LocalizationManager.Instance.GetText(tower.towerModel.baseId);
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
                ModHelper.Error<CopyPasteTowersMod>(e);
            }
            finally
            {
                payForIt = false;
            }
        }));

        if (Input.GetKey(KeyCode.LeftShift))
        {
            inputManager.TryPlace();
        }
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

                if (lastCopyWasCut)
                {
                    var tts = tower.GetTowerToSim();
                    TowerSelectionMenu.instance.SelectTower(tts);
                }
            }
        }
    }
}