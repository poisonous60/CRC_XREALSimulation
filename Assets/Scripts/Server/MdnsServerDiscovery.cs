using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Finds the RadVis server with mDNS/DNS-SD so its IP never has to be typed.
/// </summary>
public sealed class MdnsServerDiscovery
{
    private const string MulticastAddress = "224.0.0.251";
    private const int MulticastPort = 5353;
    private const int ReceiveTimeoutMilliseconds = 700;
    private const int IdlePollMilliseconds = 150;
    private const int RecordTypeA = 1;
    private const int RecordTypePtr = 12;
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
            stoppingThread.Join(queryIntervalMilliseconds + ReceiveTimeoutMilliseconds);

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

            try
            {
                using (UdpClient client = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
                {
                    client.Client.ReceiveTimeout = ReceiveTimeoutMilliseconds;
                    client.Send(query, query.Length, MulticastAddress, MulticastPort);

                    while (isAlive && isQuerying && elapsedMilliseconds < queryIntervalMilliseconds)
                    {
                        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        byte[] response;

                        elapsedMilliseconds += ReceiveTimeoutMilliseconds;

                        try
                        {
                            response = client.Receive(ref sender);
                        }
                        catch (SocketException)
                        {
                            break;
                        }

                        List<string> addresses;
                        int port;

                        if (!TryParseResponse(response, out addresses, out port))
                            continue;

                        // The reply reached this socket, so its sender is the server adapter reachable from here.
                        string senderAddress = sender.Address.ToString();
                        addresses.Remove(senderAddress);
                        addresses.Insert(0, senderAddress);

                        if (!isQuerying)
                            break;

                        lock (resultLock)
                        {
                            pendingAddresses = addresses;
                            pendingPort = port;
                        }

                        break;
                    }
                }
            }
            catch (Exception queryException)
            {
                Debug.LogWarning($"[MdnsServerDiscovery] Query failed: {queryException.Message}");
            }

            int remainingMilliseconds = queryIntervalMilliseconds - elapsedMilliseconds;

            if (isAlive && isQuerying && remainingMilliseconds > 0)
                Thread.Sleep(remainingMilliseconds);
        }
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
                addresses.Add($"{data[offset]}.{data[offset + 1]}.{data[offset + 2]}.{data[offset + 3]}");
            else if (recordType == RecordTypeSrv && dataLength >= 6 && port == 0)
                port = ReadUInt16(data, offset + 4);

            offset += dataLength;
        }

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
