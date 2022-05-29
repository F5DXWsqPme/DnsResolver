using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;

namespace DnsResolver;

public class DnsServer
{
    private int port = 53;
    private TcpListener tcpServer;
    private UdpClient udpServer;
    private Config config = new Config("config.txt");

    public DnsServer()
    {
        tcpServer = new TcpListener(new IPEndPoint(IPAddress.Any, port));
        udpServer = new UdpClient(port);
        tcpServer.Start();
    }

    private byte[] ProcessRequestBuffer(byte[] requestBuffer, String logString, Resolver resolver)
    {
        Log.Logger.Debug(logString);

        var request = Request.FromArray(requestBuffer);
        var response = Response.FromRequest(request);
        var isFail = false;
        foreach (var question in request.Questions)
        {
            Log.Logger.Debug($"Processing question {question}");

            try
            {
                if (question.Class == RecordClass.IN && question.Type == RecordType.A)
                {
                    if (config.Redirections.ContainsKey(question.Name.ToString()))
                    {
                        Log.Logger.Information($"Result fromm config {question.Name} -> {config.Redirections[question.Name.ToString()]}");
                        response.AnswerRecords.Add(new IPAddressResourceRecord(question.Name, config.Redirections[question.Name.ToString()]));
                    }
                    else
                    {
                        resolver.ResetRequestCounter();
                        var answer = resolver.Resolve(question.Name.ToString(), question.Type);
                        if (answer != null)
                        {
                            response.AnswerRecords.Add(new IPAddressResourceRecord(question.Name, answer));
                            Log.Logger.Debug("Answer added");
                        }
                        else
                        {
                            Log.Logger.Warning("Resolve failed");
                            isFail = true;
                        }
                    }
                }
                else
                {
                    isFail = true; 
                }
            }
            catch (CachedRequester.RequestCounterMoreThanMax)
            {
                isFail = true; 
                Log.Logger.Debug("Resolve failed with error request counter overflow");
            }
        }

        if (isFail)
        {
            Log.Logger.Warning("Sending refused response");
            response.ResponseCode = ResponseCode.Refused;
        }
        else
        {
            Log.Logger.Information("Sending success response");
        }

        return response.ToArray();
    }
    
    public void RunTcp()
    {
        var resolver = new Resolver();
        while (true)
        {
            try
            {
                using var client = tcpServer.AcceptTcpClient();
                var stream = client.GetStream();
                var buffer = new byte[2];
                var length = (UInt16)(buffer[1] | (buffer[0] << 8));
                buffer = new byte[length];
                var answer = ProcessRequestBuffer(buffer,
                    $"Processing request from tcp client {client.Client.RemoteEndPoint}", resolver);
                var sendSize = new List<byte> { (byte)((answer.Length >> 8) & 0xFF), (byte)(answer.Length & 0xFF) };
                stream.Write(sendSize.ToArray(), 0, 2);
                stream.Write(answer, 0, answer.Length);
            }
            catch (Exception e)
            {
                Log.Logger.Error($"Processing error {e}");
            }
        }
    }
    
    public void RunUdp()
    {
        var resolver = new Resolver();
        while (true)
        {
            try
            {
                IPEndPoint? client = new IPEndPoint(IPAddress.Any, 0);
                var buffer = udpServer.Receive(ref client);
                var answer = ProcessRequestBuffer(buffer, $"Processing request from udp client {client}", resolver);
                udpServer.Send(answer, client);
            }
            catch (Exception e)
            {
                Log.Logger.Error($"Processing error {e}");
            }
        }
    }
}
