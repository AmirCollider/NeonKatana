namespace NeonKatana
{
    /// <summary>
    /// The languages the game ships in. Adding or swapping one means adding the matching field to
    /// <see cref="LocalizationTable.Entry"/> — the two are meant to be edited together.
    /// The numbers are saved and sent to the server, so never renumber an existing language.
    /// </summary>
    public enum GameLanguage
    {
        English = 0,
        Persian = 1,
        Japanese = 2,
    }
}
