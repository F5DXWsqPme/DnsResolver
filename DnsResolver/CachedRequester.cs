using System.Net;
using DNS.Protocol;

namespace DnsResolver;

public class CachedRequester : IRequester
{
    private IRequester requester = new Requester();
    private Dictionary<(String, IPAddress), (Response, DateTime, TimeSpan)> cache = new Dictionary<(String, IPAddress), (Response, DateTime, TimeSpan)>();
    private int requestsCounter = 0;
    private readonly int maxRequestsCount = 1000;
    
    public void ResetRequestCounter()
    {
        requestsCounter = 0;
    }

    private TimeSpan GetResponseTtl(Response response)
    {
        var ttl = TimeSpan.MaxValue;
        foreach (var record in response.AdditionalRecords.Concat(response.AnswerRecords).Concat(response.AuthorityRecords))
        {
            if (record.TimeToLive < ttl)
            {
                ttl = record.TimeToLive;
            }
        }

        return ttl;
    }

    public class RequestCounterMoreThanMax : Exception
    {
    }
    
    public Response GetResponseFromQuestion(IPAddress serverIp, Question question)
    {
        Log.Logger.Debug($"Request {requestsCounter}/{maxRequestsCount}");
        
        if (requestsCounter > maxRequestsCount)
        {
            Log.Logger.Error("Detected requester counter overflow");
            throw new RequestCounterMoreThanMax();
        }
        requestsCounter++;
        
        Log.Logger.Debug($"Searching request {question.Name} ({question.Type}) to {serverIp} in cache");
        var key = (question.ToString(), serverIp);
        if (cache.ContainsKey(key))
        {
            var (response, time, ttl) = cache[key];
            Log.Logger.Debug($"Found response in cache at {time} with ttl={ttl}");
            if (ttl != TimeSpan.MaxValue && time + ttl <= DateTime.Now)
            {
                Log.Logger.Debug($"Response too old, removing from cache");
                cache.Remove(key);
            }
            else
            {
                return response;
            }
        }

        {
            Log.Logger.Debug("Response not found in cache");

            var response = requester.GetResponseFromQuestion(serverIp, question);
            var ttl = GetResponseTtl(response);
            var time = DateTime.Now;
            cache.Add(key, (response, time, ttl));
            
            Log.Logger.Debug($"Response with ttl={ttl} added to cache by key {key}");

            return response;
        }
    }
}