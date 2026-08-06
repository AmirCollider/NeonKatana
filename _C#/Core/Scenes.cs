namespace NeonKatana
{
    /// <summary>
    /// The scenes by name, in code rather than in a text field on some component. A rename now
    /// breaks the build instead of breaking a button nobody pressed until release.
    /// Both names must also be in File ▸ Build Profiles ▸ Scene List.
    /// </summary>
    public static class Scenes
    {
        public const string MainMenu = "MainMenu";
        public const string MainGame = "MainGame";
    }
}
