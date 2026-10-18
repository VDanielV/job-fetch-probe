using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobFetchProbe.Web.Api;

/// <summary>How the API writes JSON: camelCase names, and enums (states, steps…) as their names.</summary>
internal static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
