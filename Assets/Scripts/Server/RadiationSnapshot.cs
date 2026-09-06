using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The newest CPS snapshot from the measurement server and how old it is.
/// </summary>
public class RadiationSnapshot
{
    private readonly HashSet<string> liveDetectorIds =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    private bool serverConnected;
    private bool received;
    private float receivedTime = float.NegativeInfinity;
    private bool lastFreshnessState;
    private float latestAggregateValue = -1f;

    public bool ServerConnected => serverConnected;
    public float LatestAggregateValue => latestAggregateValue;
    public int LiveDetectorCount => liveDetectorIds.Count;
    public bool LastFreshnessState => lastFreshnessState;

    public void Reset()
    {
        received = false;
        lastFreshnessState = false;
        receivedTime = float.NegativeInfinity;
        liveDetectorIds.Clear();
        latestAggregateValue = -1f;
    }

    // Returns true when the connection state changed.
    public bool SetServerConnection(bool connected)
    {
        bool changed = serverConnected != connected;
        serverConnected = connected;

        // A snapshot from the previous WebSocket generation must never make
        // restored detector values look live on the replacement connection.
        if (changed)
            Reset();

        return changed;
    }

    // Returns the highest valid reading in the snapshot, or -1 when it holds none.
    public float Ingest(IReadOnlyDictionary<string, float> data)
    {
        received = true;
        receivedTime = Time.unscaledTime;
        liveDetectorIds.Clear();

        float maximumValue = -1f;

        foreach (KeyValuePair<string, float> pair in data)
        {
            string detectorId = DetectorId.Normalize(pair.Key);

            if (string.IsNullOrEmpty(detectorId) ||
                pair.Value < 0f ||
                float.IsNaN(pair.Value) ||
                float.IsInfinity(pair.Value))
            {
                continue;
            }

            liveDetectorIds.Add(detectorId);
            if (pair.Value > maximumValue)
                maximumValue = pair.Value;
        }

        latestAggregateValue = maximumValue;
        return maximumValue;
    }

    public bool IsFresh(float maximumAgeSeconds)
    {
        return serverConnected &&
               received &&
               Time.unscaledTime - receivedTime <= Mathf.Max(0.5f, maximumAgeSeconds);
    }

    public void RememberFreshness(bool fresh)
    {
        lastFreshnessState = fresh;
    }
}
