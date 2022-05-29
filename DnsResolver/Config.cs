using System.Net;
using Newtonsoft.Json;

namespace DnsResolver;

public class Config
{
    public Dictionary<String, IPAddress> Redirections { get; } = new Dictionary<string, IPAddress>();

    public Config(String fileName)
    {
        try
        {
            var redirs = new List<Redirection>();
            var text = File.ReadAllText(fileName);
            redirs = JsonConvert.DeserializeObject<List<Redirection>>(text) ?? redirs;

            foreach (var rd in redirs)
            {
                if (rd.From != null && rd.To != null)
                {
                    var from = rd.From;
                    var to = rd.To;
                    var toIp = IPAddress.Parse(to);
                    Redirections.Add(from, toIp);
                }
            }
        }
        catch (Exception e)
        {
            Log.Logger.Error($"Config loading error {e}");
        }
    }

    public Config()
    {
    }
}