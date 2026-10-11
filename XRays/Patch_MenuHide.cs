using HarmonyLib;

namespace XRays
{
    /// <summary>
    /// Every close of the pause menu is stamped with the state it leaves behind: the
    /// plugin's own flows and the game's calls land in the same list, so a close that
    /// does not take effect - or one fired from the unexpected place - shows itself.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.Hide))]
    internal static class Patch_MenuHide
    {
        private static void Postfix()
        {
            XRays.Instance?.Log.LogInfo("Menu.Hide, IsVisible now " + Menu.IsVisible() + ".");
        }
    }
}
