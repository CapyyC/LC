using System;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Entry point. The game's built-in mod loader (Add_On) and BaseMod / Lobotomy Mod Manager load every dll in
    /// LobotomyCorp_Data/BaseMods/*/ and instantiate the class named exactly "Harmony_Patch".
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class Harmony_Patch
    {
        public Harmony_Patch()
        {
            try
            {
                Plugin.Init();
            }
            catch (Exception e)
            {
                try
                {
                    Log.Error("Lobotomy Corporation Archipelago failed to initialize", e);
                }
                catch (Exception)
                {
                    UnityEngine.Debug.LogError("[Archipelago] failed to initialize: " + e);
                }
            }
        }
    }
}
