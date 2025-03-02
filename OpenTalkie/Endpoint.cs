using System.Net.Sockets;

namespace OpenTalkie;

public class Endpoint : IDisposable
{
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

    public void Dispose()
    {
        UdpClient.Dispose();

        GC.SuppressFinalize(this);
    }
}
