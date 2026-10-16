namespace JobFetchProbe.Services.Catalog;

/// <summary>The jobs database does not exist yet: the console app creates it on its first run.</summary>
public sealed class JobStoreUnavailableException()
    : Exception("The jobs database does not exist yet. Run the console app first: it creates the database and fills it.");
