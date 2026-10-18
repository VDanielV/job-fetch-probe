namespace JobFetchProbe.Services.Catalog;

/// <summary>The jobs database does not exist yet: the API creates it when it starts.</summary>
public sealed class JobStoreUnavailableException()
    : Exception("The jobs database does not exist yet. Start the API: it creates the database; an acquisition fills it.");
