namespace JobFetchProbe.Services.Journal;

/// <summary>The journal cannot be read: the database does not exist yet (the console app creates it on its first run).</summary>
public sealed class JournalUnavailableException()
    : Exception("The jobs database does not exist yet, so there is no journal. Run the console app first: it creates the database.");
