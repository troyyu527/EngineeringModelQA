using System.Globalization;
using System.Security.Cryptography;
using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;

namespace EngineeringModelQA.Ifc;

/// <summary>
/// Reads IFC4 STEP files with xBIM into plain <see cref="ElementRecord"/>s, then disposes the model.
/// No xBIM object leaves this class.
/// </summary>
public sealed class IfcModelReader : IModelReader
{
    private const int ProgressEvery = 500;

    public Task<ModelSnapshot> ReadAsync(string path, IProgress<ImportProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Read(path, progress, cancellationToken), cancellationToken);

    private static ModelSnapshot Read(string path, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        var fileName = Path.GetFileName(path);
        if (!File.Exists(path))
            throw new ModelReadException($"The model file '{fileName}' was not found.");

        ct.ThrowIfCancellationRequested();
        progress?.Report(new ImportProgress("Opening file", 0));

        string fingerprint;
        IModel model;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (var sha = SHA256.Create())
                fingerprint = ToHex(sha.ComputeHash(stream));
            stream.Position = 0;
            if (stream.Length == 0)
                throw new ModelReadException($"'{fileName}' is empty. Choose an IFC4 file that contains a model.");
            model = MemoryModel.OpenReadStep21(stream, (ReportProgressDelegate?)null);
        }
        catch (ModelReadException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ModelReadException($"You do not have permission to read '{fileName}'.", ex);
        }
        catch (IOException ex)
        {
            throw new ModelReadException($"'{fileName}' could not be opened. It may be open in another program.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ModelReadException($"'{fileName}' is not a readable IFC file. The IFC header or data could not be read.", ex);
        }

        using (model)
        {
            if (model.SchemaVersion != XbimSchemaVersion.Ifc4)
                throw new ModelReadException(
                    $"'{fileName}' uses the {SchemaName(model.SchemaVersion)} schema. This version reads IFC4 files only.");
            ct.ThrowIfCancellationRequested();

            return Copy(model, path, fingerprint, progress, ct);
        }
    }

    private static ModelSnapshot Copy(IModel model, string path, string fingerprint, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        var warnings = new List<string>();
        var skippedProperties = new Dictionary<string, int>(StringComparer.Ordinal);
        var unsupportedMaterials = new Dictionary<string, int>(StringComparer.Ordinal);

        progress?.Report(new ImportProgress("Indexing relationships", 0));
        var relations = new Relations(model, ct);

        var inScope = new List<(IIfcBuildingElement Element, string Type)>();
        inScope.AddRange(model.Instances.OfType<IIfcBeam>().Select(e => ((IIfcBuildingElement)e, SupportedEntityTypes.Beam)));
        inScope.AddRange(model.Instances.OfType<IIfcColumn>().Select(e => ((IIfcBuildingElement)e, SupportedEntityTypes.Column)));
        inScope.AddRange(model.Instances.OfType<IIfcSlab>().Select(e => ((IIfcBuildingElement)e, SupportedEntityTypes.Slab)));
        inScope.AddRange(model.Instances.OfType<IIfcWall>().Select(e => ((IIfcBuildingElement)e, SupportedEntityTypes.Wall)));
        inScope.Sort((a, b) => a.Element.EntityLabel.CompareTo(b.Element.EntityLabel));

        var records = new List<ElementRecord>(inScope.Count);
        foreach (var (element, type) in inScope)
        {
            ct.ThrowIfCancellationRequested();
            records.Add(new ElementRecord(
                element.GlobalId.ToString(),
                type,
                element.Name?.ToString(),
                ReadProperties(element, relations, skippedProperties),
                ReadMaterial(element, relations, unsupportedMaterials),
                relations.Storeys.TryGetValue(element.EntityLabel, out var storey) ? storey : null,
                "#" + element.EntityLabel.ToString(CultureInfo.InvariantCulture)));
            if (records.Count % ProgressEvery == 0)
                progress?.Report(new ImportProgress("Reading elements", records.Count));
        }
        progress?.Report(new ImportProgress("Reading elements", records.Count));

        if (records.Count == 0)
            warnings.Add("The model contains no IfcBeam, IfcColumn, IfcSlab or IfcWall elements.");

        var scopedLabels = new HashSet<int>(inScope.Select(e => e.Element.EntityLabel));
        var outOfScope = model.Instances.OfType<IIfcBuildingElement>()
            .Where(e => !scopedLabels.Contains(e.EntityLabel))
            .GroupBy(e => e.ExpressType.ExpressName)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in outOfScope)
            warnings.Add($"{group.Count()} {group.Key} element(s) are outside the checked types and were not read.");
        foreach (var pair in skippedProperties.OrderBy(p => p.Key, StringComparer.Ordinal))
            warnings.Add($"{pair.Value} {pair.Key} propert(ies) skipped: only single-value properties are read.");
        foreach (var pair in unsupportedMaterials.OrderBy(p => p.Key, StringComparer.Ordinal))
            warnings.Add($"{pair.Value} element(s) use {pair.Key} as material, which is not supported; treated as no material.");

        return new ModelSnapshot(path, fingerprint, "IFC4", records, warnings);
    }

    /// <summary>Occurrence property sets first; property sets on the element's type only fill gaps.</summary>
    private static IReadOnlyDictionary<string, string> ReadProperties(IIfcObject element, Relations relations, Dictionary<string, int> skipped)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        IEnumerable<IIfcPropertySetDefinition> occurrenceSets = relations.PropertySets.TryGetValue(element.EntityLabel, out var sets)
            ? sets
            : Enumerable.Empty<IIfcPropertySetDefinition>();
        IEnumerable<IIfcPropertySetDefinition> typeSets = relations.Types.TryGetValue(element.EntityLabel, out var type)
            ? type.HasPropertySets
            : Enumerable.Empty<IIfcPropertySetDefinition>();
        foreach (var set in occurrenceSets.Concat(typeSets).OfType<IIfcPropertySet>())
        {
            var setName = set.Name?.ToString() ?? "";
            foreach (var property in set.HasProperties)
            {
                if (property is IIfcPropertySingleValue single)
                {
                    var key = setName + "." + single.Name;
                    if (!properties.ContainsKey(key))
                        properties[key] = Format(single.NominalValue);
                }
                else
                {
                    var kind = property.ExpressType.ExpressName;
                    skipped[kind] = skipped.TryGetValue(kind, out var n) ? n + 1 : 1;
                }
            }
        }
        return properties;
    }

    private static string Format(IIfcValue? value) => value?.Value switch
    {
        null => "",
        bool b => b ? "TRUE" : "FALSE",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString() ?? "",
    };

    /// <summary>Material on the element, else on its type. Null when neither has one.</summary>
    private static string? ReadMaterial(IIfcObject element, Relations relations, Dictionary<string, int> unsupported)
    {
        string? FromAssociations(int label) =>
            relations.Materials.TryGetValue(label, out var selects)
                ? selects.Select(s => Describe(s, unsupported)).FirstOrDefault(m => m is not null)
                : null;

        return FromAssociations(element.EntityLabel)
               ?? (relations.Types.TryGetValue(element.EntityLabel, out var type) ? FromAssociations(type.EntityLabel) : null);
    }

    private static string? Describe(IIfcMaterialSelect? select, Dictionary<string, int> unsupported)
    {
        static string Join(IEnumerable<string?> names)
        {
            var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal).ToList();
            return list.Count == 0 ? "(unnamed material)" : string.Join(" / ", list);
        }

        static string? Name(IIfcMaterial? material) => material?.Name.ToString();

        switch (select)
        {
            case null:
                return null;
            case IIfcMaterial material:
                return Join(new[] { Name(material) });
            case IIfcMaterialLayerSetUsage usage:
                return Join(usage.ForLayerSet.MaterialLayers.Select(l => Name(l.Material)));
            case IIfcMaterialLayerSet layerSet:
                return Join(layerSet.MaterialLayers.Select(l => Name(l.Material)));
            case IIfcMaterialLayer layer:
                return Join(new[] { Name(layer.Material) });
            case IIfcMaterialProfileSetUsage profileUsage:
                return Join(profileUsage.ForProfileSet.MaterialProfiles.Select(p => Name(p.Material)));
            case IIfcMaterialProfileSet profileSet:
                return Join(profileSet.MaterialProfiles.Select(p => Name(p.Material)));
            case IIfcMaterialProfile profile:
                return Join(new[] { Name(profile.Material) });
            case IIfcMaterialConstituentSet constituents:
                return Join(constituents.MaterialConstituents.Select(c => Name(c.Material)));
            case IIfcMaterialConstituent constituent:
                return Join(new[] { Name(constituent.Material) });
            case IIfcMaterialList list:
                return Join(list.Materials.Select(Name));
            default:
                var kind = select.ExpressType.ExpressName;
                unsupported[kind] = unsupported.TryGetValue(kind, out var n) ? n + 1 : 1;
                return null;
        }
    }

    /// <summary>
    /// Relationship lookups by entity label, built with one pass over each relationship type. Asking every element
    /// for its inverse relations (IsDefinedBy, HasAssociations, ...) was far slower, even with xBIM's inverse cache.
    /// </summary>
    private sealed class Relations
    {
        public Relations(IModel model, CancellationToken ct)
        {
            foreach (var rel in model.Instances.OfType<IIfcRelDefinesByProperties>())
            {
                var sets = rel.RelatingPropertyDefinition?.PropertySetDefinitions.ToList();
                if (sets is null) continue;
                foreach (var obj in rel.RelatedObjects)
                {
                    if (!PropertySets.TryGetValue(obj.EntityLabel, out var list))
                        PropertySets[obj.EntityLabel] = list = new List<IIfcPropertySetDefinition>();
                    list.AddRange(sets);
                }
            }
            ct.ThrowIfCancellationRequested();

            foreach (var rel in model.Instances.OfType<IIfcRelDefinesByType>())
            {
                foreach (var obj in rel.RelatedObjects)
                {
                    if (rel.RelatingType is not null && !Types.ContainsKey(obj.EntityLabel))
                        Types[obj.EntityLabel] = rel.RelatingType;
                }
            }
            ct.ThrowIfCancellationRequested();

            foreach (var rel in model.Instances.OfType<IIfcRelAssociatesMaterial>())
            {
                foreach (var obj in rel.RelatedObjects)
                {
                    if (!Materials.TryGetValue(obj.EntityLabel, out var list))
                        Materials[obj.EntityLabel] = list = new List<IIfcMaterialSelect>();
                    list.Add(rel.RelatingMaterial);
                }
            }
            ct.ThrowIfCancellationRequested();

            // Direct containment in an IfcBuildingStorey only (see docs/limitations.md).
            foreach (var rel in model.Instances.OfType<IIfcRelContainedInSpatialStructure>())
            {
                if (rel.RelatingStructure is not IIfcBuildingStorey storey) continue;
                var name = storey.Name?.ToString() ?? "(unnamed storey)";
                foreach (var element in rel.RelatedElements)
                {
                    if (!Storeys.ContainsKey(element.EntityLabel))
                        Storeys[element.EntityLabel] = name;
                }
            }
        }

        /// <summary>Occurrence property sets per object, in relationship order.</summary>
        public Dictionary<int, List<IIfcPropertySetDefinition>> PropertySets { get; } = new();

        public Dictionary<int, IIfcTypeObject> Types { get; } = new();

        /// <summary>Material associations per object or type.</summary>
        public Dictionary<int, List<IIfcMaterialSelect>> Materials { get; } = new();

        public Dictionary<int, string> Storeys { get; } = new();
    }

    private static string SchemaName(XbimSchemaVersion version) => version switch
    {
        XbimSchemaVersion.Ifc2X3 => "IFC2X3",
        XbimSchemaVersion.Ifc4x3 => "IFC4X3",
        _ => version.ToString().ToUpperInvariant(),
    };

    private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
}
