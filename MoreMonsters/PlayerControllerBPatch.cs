/*
using GameNetcodeStuff;
using HarmonyLib;

namespace MoreMonsters.PlayerBControllerPatches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    public class PlayerControllerBPatch
    {
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        private static void patchControllerUpdate()
        {
            MoreMonstersBase.myGUI.guiIsHost = MoreMonstersBase.isHost;
            MoreMonstersBase.Instance.updateCFGVarsViaGui();
        }
    }
}
*/
