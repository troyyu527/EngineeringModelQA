using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Application.Contracts;

/// <summary>Import progress: stage text and elements copied so far.</summary>
public sealed record ImportProgress(string Stage, int ElementsRead);

/// <summary>Reads a model file into plain records. Implementations dispose the source model before returning.</summary>
public interface IModelReader
{
    /// <exception cref="ModelReadException">The file cannot be used; the message is meant for the user.</exception>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    Task<ModelSnapshot> ReadAsync(string path, IProgress<ImportProgress>? progress, CancellationToken cancellationToken);
}

/// <summary>A model file could not be read. <see cref="Exception.Message"/> is shown to the user as is.</summary>
public sealed class ModelReadException(string message, Exception? innerException = null) : Exception(message, innerException);
