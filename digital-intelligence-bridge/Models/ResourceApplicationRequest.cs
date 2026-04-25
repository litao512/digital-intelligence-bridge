namespace DigitalIntelligenceBridge.Models;

public sealed record ResourceApplicationRequest(
    string ResourceId,
    string ResourceName,
    string PluginCode,
    string ResourceType);

public sealed record ResourceApplicationDialogResult(bool IsConfirmed, string Reason);
