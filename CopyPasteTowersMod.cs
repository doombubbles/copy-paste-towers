using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.ModOptions;
using CopyPasteTowers;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CopyPasteTowersMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace CopyPasteTowers;

public class CopyPasteTowersMod : BloonsTD6Mod
{
    public static readonly ModSettingHotkey CopyTower = new(KeyCode.C, HotkeyModifier.Ctrl);
    public static readonly ModSettingHotkey PasteTower = new(KeyCode.V, HotkeyModifier.Ctrl);
    public static readonly ModSettingHotkey CutTower = new(KeyCode.X, HotkeyModifier.Ctrl);

    public override void OnUpdate() => CopyPasteTowersUtility.Update();
    
    public static MelonLogger.Instance MelonLogger => ModContent.GetInstance<CopyPasteTowersMod>().LoggerInstance;
}