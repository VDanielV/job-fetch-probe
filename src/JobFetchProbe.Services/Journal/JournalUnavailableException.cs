namespace JobFetchProbe.Services.Journal;

/// <summary>The journal cannot be read: the database does not exist yet (the API creates it when it starts).</summary>
public sealed class JournalUnavailableException()
    : Exception("The jobs database does not exist yet, so there is no journal. Start the API: it creates the database.");
