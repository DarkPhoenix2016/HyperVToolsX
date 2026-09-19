using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Validation;
using Xunit;

namespace HyperVToolsX.Tests;

public class TargetValidatorTests
{
    [Fact]
    public async Task LocalHyperVHost_ShouldPassValidation()
    {
        var executor = new PowerShellExecutor();

        var validator = new TargetValidator(executor);

        var target = new HyperVTarget
        {
            Name = "localhost",
            Address = "localhost",
            Type = TargetType.StandaloneHost
        };

        var result = await validator.ValidateAsync(target);

        Assert.True(result.NameResolved);
        Assert.True(result.PingSucceeded);
        Assert.True(result.HyperVConnectionSucceeded);

        Assert.Equal(
            TargetValidationStatus.Ready,
            result.Status);

        Assert.True(result.CanCollect);
    }
}