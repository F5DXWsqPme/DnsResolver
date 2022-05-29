using System.Net;
using Newtonsoft.Json;

namespace DnsResolver;

public class Redirection
{
    [JsonProperty("From")]
    public String? From { set; get; }
    
    [JsonProperty("To")]
    public String? To { set; get; }
}