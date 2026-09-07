using UnityEngine;

/// <summary>
/// Reading to risk band, and band to marker color.
/// </summary>
public static class MarkerRisk
{
    public static RadiationRiskBand GetBand(float radiationValue, MarkerVisualSettings settings)
    {
        if (settings == null ||
            float.IsNaN(radiationValue) ||
            float.IsInfinity(radiationValue) ||
            radiationValue < 0f)
        {
            return RadiationRiskBand.Unknown;
        }

        float hiddenThreshold = Mathf.Max(0f, settings.hiddenMaxCps);
        float greenThreshold = Mathf.Max(hiddenThreshold, settings.greenMaxCps);
        float redThreshold = Mathf.Max(greenThreshold, settings.dangerThresholdCps);

        if (radiationValue <= hiddenThreshold)
            return RadiationRiskBand.Hidden;
        if (radiationValue <= greenThreshold)
            return RadiationRiskBand.Green;
        if (radiationValue <= redThreshold)
            return RadiationRiskBand.Yellow;
        return RadiationRiskBand.Red;
    }

    public static Color GetColor(float radiationValue, MarkerVisualSettings settings)
    {
        RadiationRiskBand band = GetBand(radiationValue, settings);

        if (MarkVisualConfig.TryLoad(out MarkVisualConfig visualSetting))
        {
            if (band == RadiationRiskBand.Hidden)
                return WithMarkerAlpha(visualSetting.LowCpsColor, settings);

            if (visualSetting.TryGetStatusColor(radiationValue, out Color bandColor))
                return WithMarkerAlpha(bandColor, settings);
        }

        switch (band)
        {
            case RadiationRiskBand.Green:
            case RadiationRiskBand.Hidden:
                return WithMarkerAlpha(new Color(0.0f, 1.0f, 0.0f), settings);
            case RadiationRiskBand.Yellow:
                return WithMarkerAlpha(new Color(1.0f, 1.0f, 0.0f), settings);
            case RadiationRiskBand.Red:
                return WithMarkerAlpha(new Color(1.0f, 0.0f, 0.0f), settings);
            default:
                return WithMarkerAlpha(new Color(0.65f, 0.65f, 0.65f), settings);
        }
    }

    public static Color GetColorForBand(RadiationRiskBand band, MarkerVisualSettings settings)
    {
        if (settings == null)
            return GetColor(-1f, null);

        switch (band)
        {
            case RadiationRiskBand.Red:
                return GetColor(Mathf.Max(0f, settings.dangerThresholdCps) + 1f, settings);
            case RadiationRiskBand.Yellow:
                return GetColor(Mathf.Max(0f, settings.greenMaxCps) + 1f, settings);
            default:
                return GetColor(Mathf.Max(0f, settings.greenMaxCps), settings);
        }
    }

    private static Color WithMarkerAlpha(Color rgb, MarkerVisualSettings settings)
    {
        float alpha = settings != null ? settings.markerAlpha : 1f;
        return new Color(rgb.r, rgb.g, rgb.b, alpha);
    }
}
