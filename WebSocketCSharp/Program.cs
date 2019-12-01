using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualBasic;
class MainVB
{
    public static void Main()
    {
        string ip = "127.0.0.1";
        ushort port = 8888;
        ServerWebSocket server = new ServerWebSocket(ip, port);
        server.Start();
        Console.WriteLine("Server has started on {0}:{1}", ip, port);

        Console.ReadLine();
    }
}

public class ServerWebSocket
{
    private List<TcpClient> Clients = new List<TcpClient>();
    private string Ip;
    private ushort Port;
    public void Start()
    {
        var server = new TcpListener(IPAddress.Parse(Ip), Port);
        server.Start();

        Thread thread = new Thread(() =>
        {
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Thread session = new Thread(() =>
                {
                    Connect(client);
                }
);
                session.Start();
            }
        });
        thread.Start();
    }
    public ServerWebSocket(string ip, ushort port)
    {
        this.Ip = ip;
        this.Port = port;
    }

    public byte[] Read(ref NetworkStream stream)
    {
        byte[] data = new byte[1024];

        using (MemoryStream memoryStream = new MemoryStream())
        {
            int count = 0;
            do
            {
                count = stream.Read(data, 0, data.Length);
                memoryStream.Write(data, 0, count);
            }
            while (stream.DataAvailable);

            return memoryStream.ToArray();
        }
    }

    private void Connect(TcpClient client)
    {
        NetworkStream stream = client.GetStream();

        bool isConnected = false;
        while (isConnected != true)
        {
            byte[] bytes = Read(ref stream);

            string s = Encoding.UTF8.GetString(bytes);

            if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
            {
                HandShaking(ref stream, ref s);
                Clients.Add(client);
                isConnected = true;
                Console.WriteLine("Client connected.");
            }
        }
    }

    public async Task<string> Receive(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        while (true)
        {
            while (client.Available < 3)
            {
                await Task.Delay(100);
                GC.Collect();
            }

            byte[] bytes = Read(ref stream);

            string s = Encoding.UTF8.GetString(bytes);

            // Decode Message
            // Dim decoded As Byte() = Decode(bytes)
            // Console.WriteLine("Client send {0} bytes: {1}", bytes.LongLength, Encoding.UTF8.GetString(decoded))

            // Send data from server to client
            byte[] data = Repeat(49, 5242880);

            Send(stream, ref data);
        }
    }

    public void BroadCast(ref byte[] data)
    {
        foreach (var client in Clients)
            Send(client.GetStream(), ref data);
    }
    private void HandShaking(ref NetworkStream stream, ref string s)
    {
        string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
        string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
        string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
        byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Constants.vbCrLf + "Connection: Upgrade" + Constants.vbCrLf + "Upgrade: websocket" + Constants.vbCrLf + "Sec-WebSocket-Accept: " + swkaSha1Base64 + Constants.vbCrLf + Constants.vbCrLf);
        stream.Write(response, 0, response.Length);
    }
    public void Send(NetworkStream stream, ref byte[] data)
    {
        byte[] send = new byte[] { 129 };
        byte[] actualLength;

        if (data.Length <= 125)
            actualLength = new byte[] { (byte)data.Length };
        else
        {
            byte[] PayLoadLength;
            if (data.Length <= 65535)
            {
                PayLoadLength = new byte[] { 126 };
                short Length = (short)data.Length;
                actualLength = BitConverter.GetBytes(Length);
            }
            else
            {
                PayLoadLength = new byte[] { 127 };
                long Length = data.LongLength;
                actualLength = BitConverter.GetBytes(Length);
            }
            Array.Reverse(actualLength);
            send = send.Concat(PayLoadLength).ToArray();
        }

        send = send.Concat(actualLength).ToArray();
        send = send.Concat(data).ToArray();
        stream.Write(send,0,send.Length);
    }

    public byte[] Decode(ref byte[] bytes)
    {
        bool mask = (bytes[1] & 0b10000000) != 0;
        int msglen = bytes[1] - 128;
        int offset = 2;
        byte[] decoded = new byte[] { };

        if (msglen == 126)
        {
            msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] },0);
            offset = 4;
        }
        else if (msglen == 127)
        {
            msglen = BitConverter.ToInt32(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] },0);
            offset = 10;
        }

        if (msglen == 0)
            Console.WriteLine("msglen == 0");
        else if (mask)
        {
            decoded = new byte[msglen - 1 + 1];
            byte[] masks = new byte[] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
            offset += 4;

            for (int i = 0; i <= msglen - 1; i++)
                decoded[i] = System.Convert.ToByte((bytes[offset + i] ^ masks[i % 4]));
        }
        else
        {
            Console.WriteLine("mask bit not set");
            return bytes;
        }

        return decoded;
    }

    private byte[] Repeat(byte value, long amount)
    {
        int i;
        List<byte> results = new List<byte>();

        for (i = 1; i <= amount; i++)
            results.Add(value);
        return results.ToArray();
    }
}
