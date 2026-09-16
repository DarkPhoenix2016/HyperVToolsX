using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Interfaces;

public interface ITargetValidator
{
    Task<TargetValidationResult> ValidateAsync(
        HyperVTarget target,
        CancellationToken cancellationToken = default);
}