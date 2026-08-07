namespace NeonKatana
{
    /// <summary>
    /// The languages the game ships in. Adding one means adding a column to the table in
    /// <see cref="GameText"/> and a case to <see cref="PlayerProgress.LanguageTag"/> — the three
    /// are meant to be edited together.
    /// The numbers are saved and sent to the server, so never renumber an existing language.
    /// </summary>
    public enum GameLanguage
    {
        English = 0,
        Persian = 1,
        Japanese = 2,
    }
}
