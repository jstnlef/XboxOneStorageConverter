using System.Globalization;
using System.Xml.Linq;

namespace XboxOneStorageConverter.Cli;

internal static class PlistValueParser
{
    public static IReadOnlyDictionary<string, object?> ParseRootDictionary(string xml)
    {
        var document = XDocument.Parse(xml);
        var plist = document.Root ?? throw new InvalidDataException("Missing plist root.");
        var rootValue = plist.Elements().SingleOrDefault()
            ?? throw new InvalidDataException("Missing plist value.");

        return ParseDictionary(rootValue);
    }

    public static string? GetString(IReadOnlyDictionary<string, object?> dictionary, string key)
    {
        return dictionary.TryGetValue(key, out var value) ? value as string : null;
    }

    public static bool GetBool(IReadOnlyDictionary<string, object?> dictionary, string key, bool defaultValue = false)
    {
        return dictionary.TryGetValue(key, out var value) && value is bool boolValue
            ? boolValue
            : defaultValue;
    }

    public static long GetInt64(IReadOnlyDictionary<string, object?> dictionary, string key, long defaultValue = 0)
    {
        return dictionary.TryGetValue(key, out var value) && value is long longValue
            ? longValue
            : defaultValue;
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> GetDictionaryArray(IReadOnlyDictionary<string, object?> dictionary, string key)
    {
        if (!dictionary.TryGetValue(key, out var value) || value is not List<object?> array)
        {
            return [];
        }

        return array
            .OfType<IReadOnlyDictionary<string, object?>>()
            .ToList();
    }

    private static IReadOnlyDictionary<string, object?> ParseDictionary(XElement element)
    {
        if (element.Name.LocalName != "dict")
        {
            throw new InvalidDataException($"Expected dict but found '{element.Name.LocalName}'.");
        }

        var values = element.Elements().ToList();
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        for (var index = 0; index < values.Count; index += 2)
        {
            if (index + 1 >= values.Count)
            {
                throw new InvalidDataException("Malformed plist dictionary.");
            }

            var keyElement = values[index];
            if (keyElement.Name.LocalName != "key")
            {
                throw new InvalidDataException("Malformed plist dictionary key.");
            }

            result[keyElement.Value] = ParseValue(values[index + 1]);
        }

        return result;
    }

    private static object? ParseValue(XElement element)
    {
        return element.Name.LocalName switch
        {
            "dict" => ParseDictionary(element),
            "array" => element.Elements().Select(ParseValue).ToList(),
            "string" => element.Value,
            "integer" => long.Parse(element.Value, CultureInfo.InvariantCulture),
            "true" => true,
            "false" => false,
            _ => null,
        };
    }
}
