using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quentra.Application;
using Quentra.Core;

namespace Quentra.Infrastructure;

// Shared by the CLI and the GUI so both describe a bad input file the same way.
public static class JsonErrors
{
    // System.Text.Json messages name .NET types; restate them in terms of the JSON document.
    public static string Describe(JsonException e)
    {
        var where = e.Path is { } path ? $" at {path}" + (e.LineNumber is { } line ? $" (line {line + 1})" : "") : "";
        var message = e.Message;
        if (message == UtcDateTimeConverter.MissingOffset) return $"{message}{where}.";
        if (Regex.Match(message, "The JSON property '(.+?)' could not be mapped") is { Success: true } unknown)
            return $"unknown property '{unknown.Groups[1].Value}'{where}. Check the spelling and that this is the right kind of file.";
        if (Regex.Match(message, "missing required properties including: (.+?)\\.( |$)") is { Success: true } required)
            return $"missing required properties {required.Groups[1].Value}{where}.";
        if (Regex.Match(message, "could not be converted to ([^.|]+(?:\\.[A-Za-z0-9_`]+)*)") is { Success: true } converted)
            return $"{Expected(converted.Groups[1].Value, e.Path)}{where}.";
        if (e.Path is null && e.LineNumber is { } badLine)
            return $"not valid JSON (line {badLine + 1}, character {(e.BytePositionInLine ?? 0) + 1}). The file may be cut short, or have a missing or extra comma, bracket, brace or quote.";
        if (message.Contains("System.", StringComparison.Ordinal) || message.Contains("Quentra.", StringComparison.Ordinal))
            return $"the document does not match the expected format{where}.";
        return message;
    }

    // Restates a .NET target type as the values the JSON field accepts. For records the serializer names
    // the containing type, so the field's own type is looked up from the last segment of the JSON path.
    private static string Expected(string typeName, string? path)
    {
        var type = new[] { typeof(OverrideField).Assembly, typeof(TakeoffSnapshot).Assembly }
            .Select(a => a.GetType(typeName)).FirstOrDefault(t => t is not null);
        var member = path is null ? null : Regex.Match(path, @"\.([A-Za-z0-9_]+)(\[\d+\])*$").Groups[1].Value;
        if (type is { IsEnum: false } && !string.IsNullOrEmpty(member) &&
            type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.PropertyType is { } propertyType)
        {
            type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            typeName = type.FullName ?? type.Name;
        }
        if (type is { IsEnum: true }) return $"value is not allowed; use one of {string.Join(", ", Enum.GetNames(type))} (case-sensitive)";
        if (typeName.Contains("Double", StringComparison.Ordinal) || typeName.Contains("Int", StringComparison.Ordinal)) return "value must be a number";
        if (typeName.Contains("Boolean", StringComparison.Ordinal)) return "value must be true or false";
        if (typeName.Contains("DateTimeOffset", StringComparison.Ordinal)) return "value must be a date and time such as 2026-09-27T10:00:00Z";
        if (typeName.Contains("String", StringComparison.Ordinal)) return "value must be text in quotes";
        return "value has the wrong type (for example a list where one value is expected)";
    }
}
