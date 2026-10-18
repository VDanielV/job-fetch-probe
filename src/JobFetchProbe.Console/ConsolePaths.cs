namespace JobFetchProbe.Console;

/// <summary>The folders and the database file shown on the console, as full paths. Built in Program.cs from the settings.</summary>
/// <param name="Database">The SQLite database file.</param>
/// <param name="Input">The folder step 1 reads saved search pages from.</param>
/// <param name="Archive">The folder step 1 moves the pages it read to.</param>
public sealed record ConsolePaths(string Database, string Input, string Archive);
