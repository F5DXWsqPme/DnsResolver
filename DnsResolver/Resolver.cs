using System.Net;
using DNS.Protocol;

namespace DnsResolver;

public class Resolver
{
    private readonly RequestIterator requestIterator;
    // https://www.iana.org/domains/root/servers
    private readonly IEnumerable<IPAddress> rootNsAddresses = new List<IPAddress>
    {
        IPAddress.Parse("198.41.0.4"),
        IPAddress.Parse("199.9.14.201"),
        IPAddress.Parse("192.33.4.12"),
        IPAddress.Parse("199.7.91.13"),
        IPAddress.Parse("192.203.230.10"),
        IPAddress.Parse("192.5.5.241"),
        IPAddress.Parse("192.112.36.4"),
        IPAddress.Parse("198.97.190.53"),
        IPAddress.Parse("192.36.148.17"),
        IPAddress.Parse("192.58.128.30"),
        IPAddress.Parse("193.0.14.129"),
        IPAddress.Parse("199.7.83.42"),
        IPAddress.Parse("202.12.27.33")
    };

    public Resolver()
    {
        requestIterator = new RequestIterator(this);
    }

    public IEnumerable<IPAddress> ResolveEnumerable(String name, RecordType recordType = RecordType.ANY)
    {
        try
        {
            if (name == String.Empty)
            {
                Log.Logger.Debug("Ressolving root");
                return rootNsAddresses;
            }
            
            Log.Logger.Debug($"Resolving {name}");

            var nameSplit = name.Split(".").ToList();
            var currentName = name;//String.Empty;
            var currentServers = rootNsAddresses;

            while (nameSplit.Count > 0)
            {
                //currentName = nameSplit.Last() + (currentName.Length > 0 ? "." : String.Empty) + currentName;
                nameSplit.RemoveAt(nameSplit.Count - 1);

                currentServers = requestIterator.GetNextNsIpAddress(currentServers, currentName);
            }

            currentServers = requestIterator.GetFinalAddresses(currentServers, currentName, recordType);

            return currentServers;
        }
        catch (ImmediatlyAnswerException e)
        {
            Log.Logger.Debug($"One resolved address: {e.Answer}");
            return new List<IPAddress>{e.Answer};
        }
    }
    
    public IPAddress? Resolve(String name, RecordType recordType)
    {
        try
        {
            var currentServers = ResolveEnumerable(name, recordType);

            var enumerator = currentServers.GetEnumerator();
            if (enumerator.MoveNext())
            {
                return enumerator.Current;
            }

            Log.Logger.Warning($"Resolve {name} not found address");

            return null;
        }
        catch (ImmediatlyAnswerException e)
        {
            Log.Logger.Debug($"One resolved address: {e.Answer}");
            return e.Answer;
        }
    }
    
    public void ResetRequestCounter()
    {
        requestIterator.ResetRequestCounter();
    }
}