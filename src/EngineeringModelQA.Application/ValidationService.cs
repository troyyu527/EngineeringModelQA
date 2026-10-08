using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Core.Rules;

namespace EngineeringModelQA.Application;

public enum CheckStage
{
    Importing,
    Validating,
}

/// <summary>Progress of a check, for the status line.</summary>
public sealed record CheckProgress(CheckStage Stage, string Text, int ElementsRead);

/// <summary>Reads a model and runs the enabled rules of a validated profile on it.</summary>
public sealed class ValidationService
{
    private readonly IModelReader _reader;
    private readonly RuleEngine _engine;
    private readonly Func<DateTimeOffset> _clock;

    public ValidationService(IModelReader reader, RuleEngine? engine = null, Func<DateTimeOffset>? clock = null)
    {
        _reader = reader;
        _engine = engine ?? new RuleEngine();
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>Runs on worker threads; <paramref name="progress"/> decides where reports are delivered.</summary>
    /// <exception cref="ModelReadException">The model file cannot be used.</exception>
    /// <exception cref="OperationCanceledException">The run was canceled; no partial run is returned.</exception>
    public async Task<ValidationRun> RunAsync(string modelPath, RuleProfile profile,
        IProgress<CheckProgress>? progress, CancellationToken cancellationToken)
    {
        var startedAt = _clock();
        progress?.Report(new CheckProgress(CheckStage.Importing, "Opening file", 0));

        var importProgress = new Forward<ImportProgress>(p =>
            progress?.Report(new CheckProgress(CheckStage.Importing, p.Stage, p.ElementsRead)));
        var snapshot = await _reader.ReadAsync(modelPath, importProgress, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new CheckProgress(CheckStage.Validating, "Validating", snapshot.Elements.Count));
        var results = await Task.Run(() =>
        {
            var list = new List<RuleResult>();
            foreach (var rule in profile.Rules.Where(r => r.Enabled))
            {
                cancellationToken.ThrowIfCancellationRequested();
                list.Add(_engine.Evaluate(snapshot.Elements, rule));
            }
            return list;
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var markKey = profile.PropertyKey ?? profile.Rules.Select(r => r.PropertyKey).FirstOrDefault(k => k is not null);
        var labels = snapshot.Elements.ToDictionary(e => e.GlobalId, e => LabelFor(e, markKey), StringComparer.Ordinal);

        return new ValidationRun(Guid.NewGuid(), startedAt, _clock(), snapshot.SourcePath, snapshot.SourceFingerprint,
            snapshot.Schema, profile.ProfileId, profile.Name, profile.Version, profile.Fingerprint,
            snapshot.Elements.Count, snapshot.Warnings, results, labels);
    }

    /// <summary>The element's mark, or e.g. "Beam #412 (no mark)" from the STEP label.</summary>
    public static string LabelFor(ElementRecord element, string? markKey)
    {
        var mark = markKey is null ? null : element.GetProperty(markKey);
        if (!string.IsNullOrWhiteSpace(mark))
            return mark!.Trim();
        var word = element.EntityType.StartsWith("Ifc", StringComparison.Ordinal) ? element.EntityType.Substring(3) : element.EntityType;
        return $"{word} {element.SourceLabel} (no mark)";
    }

    /// <summary>Forwards reports synchronously; the caller's IProgress decides about thread marshalling.</summary>
    private sealed class Forward<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
