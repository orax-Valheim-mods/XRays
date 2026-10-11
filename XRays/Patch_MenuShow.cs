using System;
using HarmonyLib;

namespace XRays
{
    /// <summary>
    /// Menu.Show opens the pause menu, and its only caller is the Escape branch of
    /// Menu.Update: it runs once per opening, which makes this postfix the only place
    /// that has to (re)build the entry - no polling anywhere. Harmony runs a postfix
    /// even when a prefix stopped the method itself; XRays has no prefix, and a menu
    /// that stayed hidden would cost the build one null test and show nothing anyway.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
    internal static class Patch_MenuShow
    {
        private static void Postfix(Menu __instance) => PauseMenuEntry.Add(__instance);

        /// <summary>
        /// The opening is stamped with the caller's stack: every open of the menu
        /// shows who asked for it, so an opening at the wrong moment names itself.
        /// </summary>
        private static void Prefix()
        {
            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(1, false);
            XRays.Instance?.Log.LogInfo(
                "Menu.Show at " + DateTime.Now.ToString("HH:mm:ss.fff") + ", called from:\n" + trace
            );
        }
    }
}
