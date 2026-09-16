using System.Net;
using System.Net.NetworkInformation;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.HyperV;

namespace HyperVToolsX.Infrastructure.Validation;

public class TargetValidator : ITargetValidator
{
    private readonly HyperVProvider _hyperVProvider;

    public TargetValidator(HyperVProvider hyperVProvider)
    {
        _hyperVProvider = hyperVProvider;
    }

    public async Task<TargetValidationResult> ValidateAsync(
        HyperVTarget target,
        CancellationToken cancellationToken = default)
    {
        var result = new TargetValidationResult
        {
            TargetName = target.Name,
            StartedAt = DateTime.Now,
            Status = TargetValidationStatus.Pending
        };

        try
        {
            // ---------------------------------------------------------
            // 1. DNS / NAME RESOLUTION
            // ---------------------------------------------------------

            result.Status = TargetValidationStatus.ResolvingName;

            var addresses = await Dns.GetHostAddressesAsync(
                target.Name,
                cancellationToken);

            if (addresses.Length == 0)
            {
                result.Status =
                    TargetValidationStatus.NameResolutionFailed;

                result.ErrorMessage =
                    $"Unable to resolve '{target.Name}'.";

                return Complete(result);
            }

            result.NameResolved = true;

            result.ResolvedAddress =
                addresses
                    .FirstOrDefault(a =>
                        a.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.ToString()
                ?? addresses[0].ToString();


            // ---------------------------------------------------------
            // 2. PING
            // ---------------------------------------------------------

            result.Status = TargetValidationStatus.Pinging;

            using var ping = new Ping();

            var pingReply = await ping.SendPingAsync(
                target.Name,
                3000);

            if (pingReply.Status != IPStatus.Success)
            {
                result.Status =
                    TargetValidationStatus.PingFailed;

                result.ErrorMessage =
                    $"Ping failed for '{target.Name}'. " +
                    $"Status: {pingReply.Status}";

                return Complete(result);
            }

            result.PingSucceeded = true;


            // ---------------------------------------------------------
            // 3. HYPER-V CONNECTIVITY
            // ---------------------------------------------------------

            result.Status = TargetValidationStatus.Connecting;

            var host = await _hyperVProvider.GetHostAsync(
                target.Name,
                cancellationToken);

            if (host == null)
            {
                result.Status =
                    TargetValidationStatus.ConnectionFailed;

                result.ErrorMessage =
                    $"Unable to connect to Hyper-V host '{target.Name}'.";

                return Complete(result);
            }

            result.HyperVConnectionSucceeded = true;


            // ---------------------------------------------------------
            // 4. TARGET IDENTIFICATION
            // ---------------------------------------------------------

            // For the first implementation, a successful
            // Get-VMHost connection identifies this as a
            // standalone Hyper-V host.
            //
            // Cluster detection will be added in the next stage.

            result.IsCluster = false;

            result.Status = TargetValidationStatus.Ready;

            return Complete(result);
        }
        catch (OperationCanceledException)
        {
            result.Status = TargetValidationStatus.Failed;
            result.ErrorMessage = "Validation cancelled.";

            return Complete(result);
        }
        catch (Exception ex)
        {
            result.Status = DetermineFailureStage(result);
            result.ErrorMessage = ex.Message;

            return Complete(result);
        }
    }

    private static TargetValidationStatus DetermineFailureStage(
        TargetValidationResult result)
    {
        if (!result.NameResolved)
        {
            return TargetValidationStatus.NameResolutionFailed;
        }

        if (!result.PingSucceeded)
        {
            return TargetValidationStatus.PingFailed;
        }

        if (!result.HyperVConnectionSucceeded)
        {
            return TargetValidationStatus.ConnectionFailed;
        }

        return TargetValidationStatus.Failed;
    }

    private static TargetValidationResult Complete(
        TargetValidationResult result)
    {
        result.CompletedAt = DateTime.Now;
        return result;
    }
}