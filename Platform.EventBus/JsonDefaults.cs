using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.EventBus;

/// <summary>Єдині правила серіалізації для publisher і consumer: camelCase, enum рядками, дати ISO 8601 (UTC).</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)    // camelCase + нечутливість до регістру
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
