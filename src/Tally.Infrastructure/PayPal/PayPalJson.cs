using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tally.Infrastructure.PayPal;

internal static class PayPalJson
{
    // PayPal uses snake_case ("due_date"); C# uses PascalCase ("DueDate")
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}