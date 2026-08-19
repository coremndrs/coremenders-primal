using Game.Simulation;

namespace Game.Persistence
{
    /// <summary>
    /// Convenience wrapper — delegates to SaveSystem.LoadTemplate().
    /// All file I/O now goes through SaveSystem (Newtonsoft-backed) rather than JsonUtility.
    /// </summary>
    public static class TemplateLoader
    {
        public static (WorldState world, MapEntityLayer mapLayer) Load() => SaveSystem.LoadTemplate();
    }
}
