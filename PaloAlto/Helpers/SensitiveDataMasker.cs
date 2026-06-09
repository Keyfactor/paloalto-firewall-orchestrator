using System;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

public static class SensitiveDataMasker
{
    public static string MaskSensitiveData(string json)
    {
        try
        {
            JObject jsonObject = JObject.Parse(json);

            // Replace all keys named "Password" or similar
            MaskKey(jsonObject, "StorePassword");
            MaskKey(jsonObject, "ServerPassword");
            MaskKey(jsonObject, "PrivateKeyPassword");

            return jsonObject.ToString(Newtonsoft.Json.Formatting.Indented);
        }
        catch (JsonException ex)
        {
            Console.WriteLine("Invalid JSON provided: " + ex.Message);
            return json; // Return the original JSON if parsing fails
        }
    }

    private static void MaskKey(JObject jsonObject, string key)
    {
        foreach (var property in jsonObject.Properties())
        {
            if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                property.Value = "*****";
            }
            else if (property.Value.Type == JTokenType.Object)
            {
                MaskKey((JObject)property.Value, key);
            }
            else if (property.Value.Type == JTokenType.String)
            {
                // Optionally handle nested JSON strings
                string value = property.Value.ToString();
                if (value.StartsWith("{") && value.EndsWith("}"))
                {
                    try
                    {
                        JObject nestedObject = JObject.Parse(value);
                        MaskKey(nestedObject, key);
                        property.Value = nestedObject.ToString(Newtonsoft.Json.Formatting.None);
                    }
                    catch
                    {
                        // Not a valid JSON string, skip
                    }
                }
            }
        }
    }
}
