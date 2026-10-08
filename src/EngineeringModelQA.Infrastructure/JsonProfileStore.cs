using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Core.Rules;

namespace EngineeringModelQA.Infrastructure;

/// <summary>
/// Loads rule profiles from JSON and validates them completely before a check can use them. Comments and trailing
/// commas are allowed; unknown fields are errors. All problems are reported at once.
/// </summary>
public sealed class JsonProfileStore : IProfileStore
{
    private static readonly Regex IdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

    private static readonly string[] RootFields =
        { "profileId", "name", "version", "scope", "entityTypes", "propertySet", "propertyName", "rules" };

    private static readonly string[] RuleFields =
    {
        "id", "name", "type", "severity", "enabled", "entityTypes", "propertySet", "propertyName", "pattern",
        "uniqueScope", "excludeWhen",
    };

    private static readonly string[] ExclusionFields = { "property", "equals" };

    private static readonly Dictionary<string, RuleType> RuleTypes = new(StringComparer.Ordinal)
    {
        ["requiredProperty"] = RuleType.RequiredProperty,
        ["uniqueValue"] = RuleType.UniqueValue,
        ["namingPattern"] = RuleType.NamingPattern,
        ["materialAssigned"] = RuleType.MaterialAssigned,
        ["storeyAssigned"] = RuleType.StoreyAssigned,
    };

    private static readonly Dictionary<string, Severity> Severities = new(StringComparer.Ordinal)
    {
        ["error"] = Severity.Error,
        ["warning"] = Severity.Warning,
        ["info"] = Severity.Info,
    };

    private static readonly Dictionary<string, UniqueScope> UniqueScopes = new(StringComparer.Ordinal)
    {
        ["project"] = UniqueScope.Project,
        ["storey"] = UniqueScope.Storey,
    };

    public ProfileLoadResult Load(string path)
    {
        var fileName = Path.GetFileName(path);
        if (!File.Exists(path))
            return Fail($"The profile file '{fileName}' was not found.");
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"The profile file '{fileName}' could not be read: {ex.Message}");
        }
        return Parse(json);
    }

    /// <summary>Validates profile JSON text.</summary>
    public ProfileLoadResult Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (line {line + 1}, position {(ex.BytePositionInLine ?? 0) + 1})" : "";
            return Fail($"The profile is not valid JSON{where}. Check for missing commas, quotes or brackets.");
        }

        using (document)
        {
            var errors = new List<string>();
            var profile = Validate(document.RootElement, errors);
            return errors.Count == 0 && profile is not null ? ProfileLoadResult.Valid(profile) : ProfileLoadResult.Invalid(errors);
        }
    }

    private static ProfileLoadResult Fail(string error) => ProfileLoadResult.Invalid(new[] { error });

    // ------------------------------------------------------------------ validation

    private static RuleProfile? Validate(JsonElement root, List<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add("The profile must be a JSON object with profileId, name, version and rules.");
            return null;
        }

        const string where = "Profile";
        CheckFields(root, RootFields, where, errors);
        var profileId = Text(root, "profileId", where, errors, required: true);
        if (profileId is not null && !IdPattern.IsMatch(profileId))
            errors.Add($"{where}: profileId '{profileId}' may only contain letters, digits, '.', '_' and '-'.");
        var name = Text(root, "name", where, errors, required: true);
        var version = Text(root, "version", where, errors, required: true);
        var scope = Text(root, "scope", where, errors, required: false);
        var entityTypes = EntityTypes(root, where, errors) ?? SupportedEntityTypes.All;
        var propertySet = Text(root, "propertySet", where, errors, required: false);
        var propertyName = Text(root, "propertyName", where, errors, required: false);
        if ((propertySet is null) != (propertyName is null))
            errors.Add($"{where}: set propertySet and propertyName together.");

        var rules = new List<RuleDefinition>();
        if (!root.TryGetProperty("rules", out var rulesElement))
            errors.Add($"{where}: 'rules' is missing. Add at least one rule.");
        else if (rulesElement.ValueKind != JsonValueKind.Array || rulesElement.GetArrayLength() == 0)
            errors.Add($"{where}: 'rules' must be a non-empty list of rules.");
        else
        {
            var index = 0;
            foreach (var ruleElement in rulesElement.EnumerateArray())
            {
                var rule = ValidateRule(ruleElement, ++index, entityTypes, propertySet, propertyName, errors);
                if (rule is not null)
                    rules.Add(rule);
            }

            foreach (var duplicate in rules.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                errors.Add($"Rule id '{duplicate.Key}' is used {duplicate.Count()} times. Rule ids must be unique.");
        }

        if (errors.Count > 0)
            return null;

        scope ??= string.Join(", ", entityTypes);
        var fingerprint = Fingerprint(profileId!, name!, version!, scope, entityTypes, propertySet, propertyName, rules);
        return new RuleProfile(profileId!, name!, version!, scope, entityTypes, propertySet, propertyName, rules, fingerprint);
    }

    private static RuleDefinition? ValidateRule(JsonElement element, int index, IReadOnlyList<string> profileTypes,
        string? profileSet, string? profileName, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Rule {index}: must be a JSON object.");
            return null;
        }

        var before = errors.Count;
        var id = element.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
        var where = string.IsNullOrWhiteSpace(id) ? $"Rule {index}" : $"Rule '{id}'";

        CheckFields(element, RuleFields, where, errors);
        id = Text(element, "id", where, errors, required: true);
        if (id is not null && !IdPattern.IsMatch(id))
            errors.Add($"{where}: id may only contain letters, digits, '.', '_' and '-'.");
        var name = Text(element, "name", where, errors, required: true);
        var type = Choice(element, "type", RuleTypes, where, errors, required: true);
        var severity = Choice(element, "severity", Severities, where, errors, required: true);
        var enabled = Bool(element, "enabled", where, errors) ?? true;
        var entityTypes = EntityTypes(element, where, errors) ?? profileTypes;
        var ruleSet = Text(element, "propertySet", where, errors, required: false);
        var ruleName = Text(element, "propertyName", where, errors, required: false);
        var pattern = Text(element, "pattern", where, errors, required: false);
        var uniqueScope = Choice(element, "uniqueScope", UniqueScopes, where, errors, required: false);
        var exclusions = Exclusions(element, where, errors);

        string? propertySet = null, propertyName = null;
        if (type is RuleType.RequiredProperty or RuleType.UniqueValue or RuleType.NamingPattern)
        {
            propertySet = ruleSet ?? profileSet;
            propertyName = ruleName ?? profileName;
            if (propertySet is null || propertyName is null)
                errors.Add($"{where}: needs propertySet and propertyName, on the rule or on the profile.");
        }
        else if (type is not null && (ruleSet is not null || ruleName is not null))
            errors.Add($"{where}: propertySet/propertyName are not used by {Name(type.Value)} rules.");

        if (type == RuleType.NamingPattern)
        {
            if (pattern is null)
                errors.Add($"{where}: namingPattern rules need a 'pattern' (a regular expression).");
            else
            {
                try
                {
                    _ = new Regex(pattern, RegexOptions.CultureInvariant, NamingPatternRule.MatchTimeout);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"{where}: pattern '{pattern}' is not a valid regular expression: {ex.Message}");
                }
            }
        }
        else if (type is not null && pattern is not null)
            errors.Add($"{where}: 'pattern' is only used by namingPattern rules.");

        if (type is not null && type != RuleType.UniqueValue && uniqueScope is not null)
            errors.Add($"{where}: 'uniqueScope' is only used by uniqueValue rules.");

        if (errors.Count > before)
            return null;

        return new RuleDefinition(id!, name!, type!.Value, severity!.Value, entityTypes)
        {
            Enabled = enabled,
            PropertySet = propertySet,
            PropertyName = propertyName,
            Pattern = pattern,
            UniqueScope = uniqueScope ?? UniqueScope.Project,
            Exclusions = exclusions,
        };
    }

    private static void CheckFields(JsonElement obj, string[] allowed, string where, List<string> errors)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                errors.Add($"{where}: unknown field '{property.Name}'. Allowed fields: {string.Join(", ", allowed)}.");
        }
    }

    private static string? Text(JsonElement obj, string field, string where, List<string> errors, bool required)
    {
        if (!obj.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
                errors.Add($"{where}: '{field}' is missing.");
            return null;
        }
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            errors.Add($"{where}: '{field}' must be a non-empty text value.");
            return null;
        }
        return value.GetString()!.Trim();
    }

    private static bool? Bool(JsonElement obj, string field, string where, List<string> errors)
    {
        if (!obj.TryGetProperty(field, out var value))
            return null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        errors.Add($"{where}: '{field}' must be true or false.");
        return null;
    }

    private static T? Choice<T>(JsonElement obj, string field, Dictionary<string, T> choices, string where,
        List<string> errors, bool required) where T : struct
    {
        var text = Text(obj, field, where, errors, required);
        if (text is null)
            return null;
        if (choices.TryGetValue(text, out var choice))
            return choice;
        errors.Add($"{where}: {field} '{text}' is not one of {string.Join(", ", choices.Keys)}.");
        return null;
    }

    private static IReadOnlyList<string>? EntityTypes(JsonElement obj, string where, List<string> errors)
    {
        if (!obj.TryGetProperty("entityTypes", out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            errors.Add($"{where}: 'entityTypes' must be a non-empty list such as [\"IfcBeam\", \"IfcColumn\"].");
            return null;
        }
        var types = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            var type = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (type is null || !SupportedEntityTypes.IsSupported(type))
                errors.Add($"{where}: entity type '{(type ?? item.ToString())}' is not supported. Use {string.Join(", ", SupportedEntityTypes.All)}.");
            else if (!types.Contains(type))
                types.Add(type);
        }
        return types;
    }

    private static IReadOnlyList<PropertyExclusion> Exclusions(JsonElement obj, string where, List<string> errors)
    {
        if (!obj.TryGetProperty("excludeWhen", out var value))
            return Array.Empty<PropertyExclusion>();
        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{where}: 'excludeWhen' must be a list of {{ \"property\": \"Pset.Prop\", \"equals\": \"value\" }}.");
            return Array.Empty<PropertyExclusion>();
        }
        var list = new List<PropertyExclusion>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{where}: every excludeWhen entry must be an object.");
                continue;
            }
            CheckFields(item, ExclusionFields, where + " excludeWhen", errors);
            var property = Text(item, "property", where + " excludeWhen", errors, required: true);
            var equals = Text(item, "equals", where + " excludeWhen", errors, required: true);
            if (property is not null && !IsQualifiedKey(property))
                errors.Add($"{where}: excludeWhen property '{property}' must be written as PropertySet.PropertyName.");
            else if (property is not null && equals is not null)
                list.Add(new PropertyExclusion(property, equals));
        }
        return list;
    }

    private static bool IsQualifiedKey(string key)
    {
        var dot = key.IndexOf('.');
        return dot > 0 && dot < key.Length - 1;
    }

    private static string Name(RuleType type) => RuleTypes.First(p => p.Value == type).Key;

    // ------------------------------------------------------------------ fingerprint

    /// <summary>SHA-256 of the validated profile written in a fixed order, so formatting and comments do not matter.</summary>
    private static string Fingerprint(string profileId, string name, string version, string scope,
        IReadOnlyList<string> entityTypes, string? propertySet, string? propertyName, IReadOnlyList<RuleDefinition> rules)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("profileId", profileId);
            w.WriteString("name", name);
            w.WriteString("version", version);
            w.WriteString("scope", scope);
            WriteList(w, "entityTypes", entityTypes);
            w.WriteString("propertySet", propertySet);
            w.WriteString("propertyName", propertyName);
            w.WriteStartArray("rules");
            foreach (var r in rules)
            {
                w.WriteStartObject();
                w.WriteString("id", r.Id);
                w.WriteString("name", r.Name);
                w.WriteString("type", Name(r.Type));
                w.WriteString("severity", r.Severity.ToString().ToLowerInvariant());
                w.WriteBoolean("enabled", r.Enabled);
                WriteList(w, "entityTypes", r.EntityTypes);
                w.WriteString("propertySet", r.PropertySet);
                w.WriteString("propertyName", r.PropertyName);
                w.WriteString("pattern", r.Pattern);
                w.WriteString("uniqueScope", r.UniqueScope.ToString().ToLowerInvariant());
                w.WriteStartArray("excludeWhen");
                foreach (var x in r.Exclusions)
                {
                    w.WriteStartObject();
                    w.WriteString("property", x.Property);
                    w.WriteString("equals", x.EqualsValue);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", "").ToLowerInvariant();
    }

    private static void WriteList(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
            w.WriteStringValue(value);
        w.WriteEndArray();
    }
}
