using System;

public static class DetectorId
{
    public static string Normalize(string rawQrText)
    {
        return string.IsNullOrWhiteSpace(rawQrText) ? "" : rawQrText.Trim();
    }

    public static bool Equals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
