namespace JobFetchProbe.Application.Dtos;

/// <summary>One redirect seen while answering a request. Only the host is kept, never the path.</summary>
public sealed record RedirectHop(int Status, string ToHost, bool Followed);
