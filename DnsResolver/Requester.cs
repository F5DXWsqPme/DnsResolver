using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DNS.Protocol;

namespace DnsResolver;

public class Requester : IRequester
{
    private UInt16 requestId = 0;
    private int timeout = 2000;

    private Response GetResponse(IPAddress serverIp, Request request)
    {
        var tokenSource = new CancellationTokenSource(timeout);
        
        var sendSize = new List<byte> {(byte)((request.Size >> 8) & 0xFF), (byte)(request.Size & 0xFF)};
        var requestBytes = sendSize.Concat(request.ToArray()).ToArray();
        
        using var tcpClient = new TcpClient();
        var rootServer = new IPEndPoint(serverIp, 53); // Dns server port
        tcpClient.ConnectAsync(rootServer, tokenSource.Token).AsTask().Wait();
        
        using var stream = tcpClient.GetStream();

        stream.WriteAsync(requestBytes, 0, requestBytes.Length, tokenSource.Token).Wait();

        var answerLengthBuffer = new byte[2];
        var readTask = stream.ReadAsync(answerLengthBuffer, 0, 2, tokenSource.Token);
        readTask.Wait();
        if (readTask.Result != 2)
        {
            throw new NetworkInformationException();
        }

        var answerLength = (UInt16)(answerLengthBuffer[1] | (answerLengthBuffer[0] << 8));
        var answerBuffer = new byte[answerLength];
        readTask = stream.ReadAsync(answerBuffer, 0, answerLength, tokenSource.Token);
        readTask.Wait();
        if (readTask.Result != answerLength)
        {
            throw new NetworkInformationException();
        }
        
        return Response.FromArray(answerBuffer);
    }

    public Response GetResponseFromQuestion(IPAddress serverIp, Question question)
    {
        Log.Logger.Debug($"Sending request {question.Name} ({question.Type}) to {serverIp}");
        
        var request = new Request();
        request.RecursionDesired = true;
        request.Id = requestId++;

        request.Questions.Add(question);

        var response = GetResponse(serverIp, request);
        
        return response;
    }
}