using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hands RadiationReceiver the server address found over mDNS.
/// </summary>
public class ServerAutoConnector : MonoBehaviour
{
    [Header("Receiver")]
    [SerializeField] private RadiationReceiver radiationReceiver;

    [Header("Discovery")]
    [Tooltip("Service the RadDetectorServer advertises. Must match MDNS_SERVICE_TYPE in DataWebServer.py.")]
    [SerializeField] private string serviceType = "_radvis._tcp.local";
    [SerializeField, Min(0.5f)] private float queryInterval = 2f;

    private MdnsServerDiscovery discovery;
    private string lastDiscoveredIp = "";

    public string LastDiscoveredAddress => lastDiscoveredIp;

    private void OnEnable()
    {
        if (radiationReceiver == null)
            radiationReceiver = FindFirstObjectByType<RadiationReceiver>();

        if (radiationReceiver == null)
        {
            Debug.LogWarning("[ServerAutoConnector] No RadiationReceiver in the scene. Discovery stays off.");
            enabled = false;
            return;
        }

        if (discovery == null)
            discovery = new MdnsServerDiscovery(serviceType, queryInterval);

        discovery.Start();
    }

    private void OnDisable()
    {
        if (discovery != null)
            discovery.Stop();
    }

    private void OnDestroy()
    {
        if (discovery != null)
            discovery.Shutdown();

        discovery = null;
    }

    private void Update()
    {
        if (discovery == null || radiationReceiver == null)
            return;

        if (radiationReceiver.IsConnected)
        {
            discovery.Stop();
            return;
        }

        if (!discovery.IsRunning)
            discovery.Start();

        // A typed address is the operator's decision, so an attempt on one is never interrupted.
        if (radiationReceiver.IsConnecting && !radiationReceiver.IsDiscoveredServerAttempt)
            return;

        List<string> addresses;
        int port;

        if (!discovery.TryTakeResult(out addresses, out port))
            return;

        string discoveredIp = addresses[0];

        // An untouched prefill is not a target; HasSavedServerIp turns true only once a connection used it.
        if (radiationReceiver.HasSavedServerIp &&
            discoveredIp == radiationReceiver.CurrentServerIp &&
            port == radiationReceiver.CurrentServerPort)
        {
            return;
        }

        lastDiscoveredIp = discoveredIp;
        radiationReceiver.ConnectToServerWithIp(discoveredIp, port);
    }
}
