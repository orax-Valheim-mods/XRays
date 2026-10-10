using HarmonyLib;

namespace XRays
{
    /// <summary>
    /// Menu.UpdateNavigation wires the gamepad's up / down chain over the buttons the
    /// menu knows about - and it runs every time the menu opens. The XRays entry is not
    /// one of those buttons, so this postfix puts the two links it breaks back: the
    /// entry keeps the place it has in the list, after Settings. Without it the entry
    /// is still clicked with the pointer, and only never reached with a D-pad.
    ///
    /// The method is private in the game: patched by NAME with nameof, which names it
    /// without emitting any member reference - the build reads the publicized
    /// assemblies, the game runs the original ones, and a direct call to a private
    /// method would compile here and throw there.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.UpdateNavigation))]
    internal static class Patch_MenuNavigation
    {
        private static void Postfix() => PauseMenuEntry.WireNavigation();
    }
}
