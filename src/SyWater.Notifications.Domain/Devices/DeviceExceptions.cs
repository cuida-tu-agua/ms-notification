using SyWater.Notifications.Domain.Common;

namespace SyWater.Notifications.Domain.Devices;

/// <summary>A device report with a state that is not OPEN or CLOSED. Not HTTP (goes to the dead-letter queue).</summary>
public sealed class InvalidValveReportException(string? state)
    : DomainException("device.invalid_valve_report", $"Invalid valve state '{state}'.");
