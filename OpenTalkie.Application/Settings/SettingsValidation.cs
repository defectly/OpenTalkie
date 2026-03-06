namespace OpenTalkie.Application.Settings;

internal static class SettingsValidation
{
    public static OperationResult ValidateVolume(float volumeGain)
    {
        return volumeGain is < 0f or > 4f
            ? OperationResult.Fail("Volume must be in range 0.0 - 4.0.")
            : OperationResult.Success();
    }

    public static OperationResult ValidatePositiveInteger(string value, string fieldName)
    {
        return !int.TryParse(value, out int intValue) || intValue <= 0
            ? OperationResult.Fail($"{fieldName} must be a positive integer.")
            : OperationResult.Success();
    }

    public static OperationResult ValidateNotEmpty(string value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? OperationResult.Fail($"{fieldName} cannot be empty.")
            : OperationResult.Success();
    }
}
