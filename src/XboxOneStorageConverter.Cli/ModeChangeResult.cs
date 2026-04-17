namespace XboxOneStorageConverter.Cli;

internal sealed record ModeChangeResult(
    string DeviceIdentifier,
    DeviceMode NewMode,
    string NewModeDisplayName,
    string PostActionMessage);
