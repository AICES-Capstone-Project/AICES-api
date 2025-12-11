using System.Text.Json;

namespace BusinessObjectLayer.Common
{
    public static class JsonUtils
    {
        public static string SerializeRawJsonSafe(object rawJson)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            switch (rawJson)
            {
                case JsonElement je:
                    return je.GetRawText();
                case string rawString:
                    // If it's already JSON object/array, keep as-is; otherwise serialize as string
                    if (IsValidJson(rawString))
                    {
                        return rawString;
                    }
                    return JsonSerializer.Serialize(rawString, options);
                default:
                    return JsonSerializer.Serialize(rawJson, options);
            }
        }

        public static bool IsValidJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            if (!(value.StartsWith("{") && value.EndsWith("}")) && !(value.StartsWith("[") && value.EndsWith("]")))
                return false;

            try
            {
                JsonDocument.Parse(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

