using System.Net.Sockets;

namespace OpenTalkie;

public class Endpoint : IDisposable
{
    public int FrameCount;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Hostname { get; set; }
    public int Port { get; set; }
    public bool StreamState { get; set; }
    public UdpClient UdpClient { get; private set; }

    public Endpoint(string name, string hostname, int port)
    {
        Name = name.Length > 16 ? name.Substring(0, 16) : name;
        Hostname = hostname;
        Port = port;
        UdpClient = new(Hostname, Port);
    }

    public void CorrectPacket(byte[] packet)
    {
        for (int i = 0; i < Name.Length; i++)
            packet[i + 8] = (byte)Name[i];

        FrameCount++;
        var convertedCounter = BitConverter.GetBytes(FrameCount);

        for (int i = 24; i < 28; i++)
            packet[i] = convertedCounter[i - 24];
    }

    public void Dispose()
    {
        UdpClient.Dispose();

        GC.SuppressFinalize(this);
    }
}
