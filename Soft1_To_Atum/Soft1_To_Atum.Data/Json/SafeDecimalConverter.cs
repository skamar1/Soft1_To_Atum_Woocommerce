using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soft1_To_Atum.Data.Json;

public class SafeDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetDecimal(out var decimalValue))
                    return decimalValue;

                // If the number is too large for decimal, try to get it as double and clamp it
                if (reader.TryGetDouble(out var doubleValue))
                {
                    if (doubleValue > (double)decimal.MaxValue)
                        return decimal.MaxValue;
                    if (doubleValue < (double)decimal.MinValue)
                        return decimal.MinValue;
                    return (decimal)doubleValue;
                }
                return 0m;

            case JsonTokenType.String:
                var stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue))
                    return 0m;

                if (decimal.TryParse(stringValue, out var parsedDecimal))
                    return parsedDecimal;

                // Try parsing as double first, then convert to decimal
                if (double.TryParse(stringValue, out var parsedDouble))
                {
                    if (parsedDouble > (double)decimal.MaxValue)
                        return decimal.MaxValue;
                    if (parsedDouble < (double)decimal.MinValue)
                        return decimal.MinValue;
                    return (decimal)parsedDouble;
                }
                return 0m;

            case JsonTokenType.Null:
                return 0m;

            default:
                return 0m;
        }
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}