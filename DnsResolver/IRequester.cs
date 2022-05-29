using System.Net;
using DNS.Protocol;

namespace DnsResolver;

public interface IRequester
{
    Response GetResponseFromQuestion(IPAddress serverIp, Question question);
}