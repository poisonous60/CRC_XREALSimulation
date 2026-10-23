using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Finds the RadVis server with mDNS/DNS-SD so its IP never has to be typed.
/// </summary>
public sealed class MdnsServerDiscovery
{
    private const string MulticastAddress = "224.0.0.251";
    private const string MulticastAddressV6 = "ff02::fb";
    private const int MulticastPort = 5353;
    private const int Ipv4PreferenceMilliseconds = 700;
    private const int ReceivePollMilliseconds = 20;
    private const int IdlePollMilliseconds = 150;
    private const int RecordTypeA = 1;
    private const int RecordTypePtr = 12;
    private const int RecordTypeAaaa = 28;
    private const int RecordTypeSrv = 33;

    private readonly string serviceType;
    private readonly int queryIntervalMilliseconds;
    private readonly object resultLock = new object();

    private Thread workerThread;
    private volatile bool isAlive;
    private volatile bool isQuerying;
    private List<string> pendingAddresses;
    private int pendingPort;

    public MdnsServerDiscovery(string serviceType, float queryIntervalSeconds)
    {
        this.serviceType = string.IsNullOrWhiteSpace(serviceType)
            ? "_radvis._tcp.local"
            : serviceType.Trim().TrimEnd('.');
        queryIntervalMilliseconds = Mathf.RoundToInt(Mathf.Max(0.5f, queryIntervalSeconds) * 1000f);
    }

    public bool IsRunning => isQuerying;

    public void Start()
    {
        isQuerying = true;

        if (workerThread != null)
            return;

        isAlive = true;
        workerThread = new Thread(QueryLoop);
        workerThread.IsBackground = true;
        workerThread.Start();
    }

    public void Stop()
    {
        isQuerying = false;
        ClearResult();
    }

    public void Shutdown()
    {
        isQuerying = false;
        isAlive = false;

        Thread stoppingThread = workerThread;
        workerThread = null;

        if (stoppingThread != null)
            stoppingThread.Join(queryIntervalMilliseconds + Ipv4PreferenceMilliseconds);

        ClearResult();
    }

    public bool TryTakeResult(out List<string> addresses, out int port)
    {
        lock (resultLock)
        {
            addresses = pendingAddresses;
            port = pendingPort;
            pendingAddresses = null;
            pendingPort = 0;
        }

        return addresses != null && addresses.Count > 0 && port > 0;
    }

    private void ClearResult()
    {
        lock (resultLock)
        {
            pendingAddresses = null;
            pendingPort = 0;
        }
    }

    private void PublishResult(List<string> addresses, int port)
    {
        if (!isQuerying)
            return;

        lock (resultLock)
        {
            pendingAddresses = addresses;
            pendingPort = port;
        }
    }

    private void QueryLoop()
    {
        byte[] query = BuildPointerQuery(serviceType);

        while (isAlive)
        {
            if (!isQuerying)
            {
                Thread.Sleep(IdlePollMilliseconds);
                continue;
            }

            int elapsedMilliseconds = 0;
            UdpClient ipv4Client = null;
            UdpClient ipv6Client = null;

            try
            {
                // Android takes no IPv4 lease on a network that offers the DHCP IPv6-only option
                // (an iPhone hotspot), so the query also goes out over IPv6.
                Exception sendError = null;
                ipv4Client = TrySendQuery(AddressFamily.InterNetwork, query, ref sendError);
                ipv6Client = TrySendQuery(AddressFamily.InterNetworkV6, query, ref sendError);

                if (ipv4Client == null && ipv6Client == null)
                    Debug.LogWarning($"[MdnsServerDiscovery] Query failed: {sendError?.Message}");
                else
                    WaitForReply(ipv4Client, ipv6Client, ref elapsedMilliseconds);
            }
            catch (Exception queryException)
            {
                Debug.LogWarning($"[MdnsServerDiscovery] Query failed: {queryException.Message}");
            }
            finally
            {
                if (ipv4Client != null)
                    ipv4Client.Close();

                if (ipv6Client != null)
                    ipv6Client.Close();
            }

            int remainingMilliseconds = queryIntervalMilliseconds - elapsedMilliseconds;

            if (isAlive && isQuerying && remainingMilliseconds > 0)
                Thread.Sleep(remainingMilliseconds);
        }
    }

    // IPv4 stays the first choice, so an IPv6 reply waits briefly for an IPv4 one still in flight.
    private void WaitForReply(UdpClient ipv4Client, UdpClient ipv6Client, ref int elapsedMilliseconds)
    {
        List<string> ipv6ReplyAddresses = null;
        int ipv6ReplyPort = 0;
        int ipv6ReplyMilliseconds = 0;

        while (isAlive && isQuerying && elapsedMilliseconds < queryIntervalMilliseconds)
        {
            List<string> addresses;
            int port;

            if (TryReceiveReply(ipv4Client, out addresses, out port))
            {
                PublishResult(addresses, port);
                return;
            }

            if (ipv6ReplyAddresses == null && TryReceiveReply(ipv6Client, out addresses, out port))
            {
                ipv6ReplyAddresses = addresses;
                ipv6ReplyPort = port;
                ipv6ReplyMilliseconds = elapsedMilliseconds;
            }

            if (ipv6ReplyAddresses != null &&
                elapsedMilliseconds - ipv6ReplyMilliseconds >= Ipv4PreferenceMilliseconds)
            {
                break;
            }

            Thread.Sleep(ReceivePollMilliseconds);
            elapsedMilliseconds += ReceivePollMilliseconds;
        }

        if (ipv6ReplyAddresses != null)
            PublishResult(ipv6ReplyAddresses, ipv6ReplyPort);
    }

    private static UdpClient TrySendQuery(AddressFamily family, byte[] query, ref Exception sendError)
    {
        UdpClient client = null;

        try
        {
            if (family == AddressFamily.InterNetworkV6)
            {
                client = new UdpClient(new IPEndPoint(IPAddress.IPv6Any, 0));
                SendOnEveryIPv6Adapter(client, query);
            }
            else
            {
                client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
                SendOnEveryAdapter(client, query);
            }

            return client;
        }
        catch (Exception exception)
        {
            sendError = exception;

            if (client != null)
                client.Close();

            return null;
        }
    }

    private static bool TryReceiveReply(UdpClient client, out List<string> addresses, out int port)
    {
        addresses = null;
        port = 0;

        if (client == null)
            return false;

        try
        {
            while (client.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(
                    client.Client.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any,
                    0);
                byte[] response = client.Receive(ref sender);

                if (!TryParseResponse(response, out addresses, out port) ||
                    !TryMoveReachableAddressFirst(addresses, sender.Address))
                {
                    continue;
                }

                return true;
            }
        }
        catch (SocketException)
        {
            // Windows surfaces an earlier ICMP port-unreachable as a reset on the next receive.
        }

        addresses = null;
        port = 0;
        return false;
    }

    // The reply reached this socket, so its sender is the server adapter reachable from here.
    // An IPv6 sender is link-local, whose zone index a ws:// URL cannot carry, so the advertised
    // address on one of this device's own /64 prefixes stands in for it. Without one the IPv6
    // reply is dropped: on an IPv6-only network its A records point nowhere.
    private static bool TryMoveReachableAddressFirst(List<string> addresses, IPAddress sender)
    {
        string reachableAddress;

        if (sender.AddressFamily == AddressFamily.InterNetworkV6)
        {
            reachableAddress = sender.IsIPv6LinkLocal
                ? FindAddressOnLocalPrefix(addresses)
                : new IPAddress(sender.GetAddressBytes()).ToString();
        }
        else
        {
            reachableAddress = sender.ToString();
        }

        if (reachableAddress == null)
            return false;

        addresses.Remove(reachableAddress);
        addresses.Insert(0, reachableAddress);
        return true;
    }

    private static string FindAddressOnLocalPrefix(List<string> addresses)
    {
        List<IPAddress> localAddresses = GetLocalGlobalIPv6Addresses();

        foreach (string candidate in addresses)
        {
            IPAddress parsed;

            if (!IPAddress.TryParse(candidate, out parsed) || parsed.AddressFamily != AddressFamily.InterNetworkV6)
                continue;

            byte[] candidateBytes = parsed.GetAddressBytes();

            foreach (IPAddress localAddress in localAddresses)
            {
                if (SharesPrefix64(candidateBytes, localAddress.GetAddressBytes()))
                    return candidate;
            }
        }

        return null;
    }

    private static bool SharesPrefix64(byte[] first, byte[] second)
    {
        for (int index = 0; index < 8; index++)
        {
            if (first[index] != second[index])
                return false;
        }

        return true;
    }

    // Windows sends multicast out one adapter, the one whose 224.0.0.0/4 route has the lowest
    // metric. A VPN tunnel or a link-dead NIC wins that race and the send fails with
    // WSAEHOSTUNREACH, so the query goes out every adapter instead. One datagram each.
    private static void SendOnEveryAdapter(UdpClient client, byte[] query)
    {
        bool sentAny = false;

        foreach (IPAddress localAddress in GetLocalIPv4Addresses())
        {
            try
            {
                client.Client.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastInterface,
                    localAddress.GetAddressBytes());
                client.Send(query, query.Length, MulticastAddress, MulticastPort);
                sentAny = true;
            }
            catch (SocketException)
            {
            }
        }

        if (!sentAny)
            client.Send(query, query.Length, MulticastAddress, MulticastPort);
    }

    // ff02::fb is link-scoped, so each send names its adapter by the scope of that adapter's link-local address.
    private static void SendOnEveryIPv6Adapter(UdpClient client, byte[] query)
    {
        byte[] groupBytes = IPAddress.Parse(MulticastAddressV6).GetAddressBytes();
        bool sentAny = false;

        foreach (long scopeId in GetLocalIPv6Scopes())
        {
            try
            {
                client.Client.SetSocketOption(
                    SocketOptionLevel.IPv6,
                    SocketOptionName.MulticastInterface,
                    (int)scopeId);
                client.Send(query, query.Length, new IPEndPoint(new IPAddress(groupBytes, scopeId), MulticastPort));
                sentAny = true;
            }
            catch (SocketException)
            {
            }
        }

        if (!sentAny)
            client.Send(query, query.Length, new IPEndPoint(new IPAddress(groupBytes), MulticastPort));
    }

    private static List<IPAddress> GetLocalIPv4Addresses()
    {
        List<IPAddress> addresses = new List<IPAddress>();

        foreach (NetworkInterface adapter in GetMulticastAdapters())
        {
            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    addresses.Add(unicast.Address);
            }
        }

        return addresses;
    }

    private static List<long> GetLocalIPv6Scopes()
    {
        List<long> scopes = new List<long>();

        foreach (NetworkInterface adapter in GetMulticastAdapters())
        {
            long scopeId = 0;

            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                IPAddress address = unicast.Address;

                if (address.AddressFamily == AddressFamily.InterNetworkV6 &&
                    address.IsIPv6LinkLocal &&
                    address.ScopeId > 0)
                {
                    scopeId = address.ScopeId;
                    break;
                }
            }

            if (scopeId == 0)
                scopeId = GetIPv6InterfaceIndex(adapter);

            if (scopeId > 0 && !scopes.Contains(scopeId))
                scopes.Add(scopeId);
        }

        return scopes;
    }

    // Second source for the interface index, for platforms that leave ScopeId unset on enumerated addresses.
    private static long GetIPv6InterfaceIndex(NetworkInterface adapter)
    {
        try
        {
            if (!adapter.Supports(NetworkInterfaceComponent.IPv6))
                return 0;

            IPv6InterfaceProperties properties = adapter.GetIPProperties().GetIPv6Properties();
            return properties != null ? properties.Index : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static List<IPAddress> GetLocalGlobalIPv6Addresses()
    {
        List<IPAddress> addresses = new List<IPAddress>();

        foreach (NetworkInterface adapter in GetMulticastAdapters())
        {
            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                IPAddress address = unicast.Address;

                if (address.AddressFamily == AddressFamily.InterNetworkV6 &&
                    !address.IsIPv6LinkLocal &&
                    !address.IsIPv6Multicast &&
                    !IPAddress.IsLoopback(address))
                {
                    addresses.Add(address);
                }
            }
        }

        return addresses;
    }

    private static List<NetworkInterface> GetMulticastAdapters()
    {
        List<NetworkInterface> adapters = new List<NetworkInterface>();

        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                !adapter.SupportsMulticast ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            adapters.Add(adapter);
        }

        return adapters;
    }

    private static byte[] BuildPointerQuery(string name)
    {
        List<byte> message = new List<byte>();
        message.AddRange(new byte[] { 0x26, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        message.AddRange(EncodeName(name));
        message.Add((byte)(RecordTypePtr >> 8));
        message.Add((byte)RecordTypePtr);
        // QU bit: reply straight to this socket, which Android delivers without a MulticastLock.
        message.Add(0x80);
        message.Add(0x01);
        return message.ToArray();
    }

    private static List<byte> EncodeName(string name)
    {
        List<byte> encoded = new List<byte>();

        foreach (string label in name.Split('.'))
        {
            if (label.Length == 0)
                continue;

            encoded.Add((byte)label.Length);
            foreach (char character in label)
                encoded.Add((byte)character);
        }

        encoded.Add(0);
        return encoded;
    }

    private static bool TryParseResponse(byte[] data, out List<string> addresses, out int port)
    {
        addresses = new List<string>();
        port = 0;

        if (data == null || data.Length < 12)
            return false;

        List<string> ipv6Addresses = new List<string>();
        int questionCount = ReadUInt16(data, 4);
        int recordCount = ReadUInt16(data, 6) + ReadUInt16(data, 8) + ReadUInt16(data, 10);
        int offset = 12;

        for (int question = 0; question < questionCount; question++)
        {
            if (!TrySkipName(data, offset, out offset) || offset + 4 > data.Length)
                return false;

            offset += 4;
        }

        for (int record = 0; record < recordCount; record++)
        {
            if (!TrySkipName(data, offset, out offset) || offset + 10 > data.Length)
                break;

            int recordType = ReadUInt16(data, offset);
            int dataLength = ReadUInt16(data, offset + 8);
            offset += 10;

            if (offset + dataLength > data.Length)
                break;

            if (recordType == RecordTypeA && dataLength == 4)
            {
                addresses.Add($"{data[offset]}.{data[offset + 1]}.{data[offset + 2]}.{data[offset + 3]}");
            }
            else if (recordType == RecordTypeAaaa && dataLength == 16)
            {
                byte[] addressBytes = new byte[16];
                Array.Copy(data, offset, addressBytes, 0, 16);
                IPAddress address = new IPAddress(addressBytes);

                if (!address.IsIPv6LinkLocal && !address.IsIPv6Multicast)
                    ipv6Addresses.Add(address.ToString());
            }
            else if (recordType == RecordTypeSrv && dataLength >= 6 && port == 0)
            {
                port = ReadUInt16(data, offset + 4);
            }

            offset += dataLength;
        }

        addresses.AddRange(ipv6Addresses);
        return addresses.Count > 0 && port > 0;
    }

    private static bool TrySkipName(byte[] data, int offset, out int end)
    {
        end = offset;

        while (offset < data.Length)
        {
            int length = data[offset];

            if ((length & 0xC0) == 0xC0)
            {
                end = offset + 2;
                return end <= data.Length;
            }

            offset += 1 + length;

            if (length == 0)
            {
                end = offset;
                return end <= data.Length;
            }
        }

        return false;
    }

    private static int ReadUInt16(byte[] data, int offset)
    {
        return (data[offset] << 8) | data[offset + 1];
    }
}
