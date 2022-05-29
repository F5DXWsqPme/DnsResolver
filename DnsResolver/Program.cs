using DNS.Protocol;
using DnsResolver;
/*
var resolver = new Resolver();
while (true)
{
    var name = Console.ReadLine();
    if (name != null)
    {
        resolver.ResetRequestCounter();
        Console.WriteLine(resolver.Resolve(name, RecordType.A));
    }
}
*/
var config = new Config("config.txt");
var server = new DnsServer();

while (true)
{
    try
    {
        var tcpTask = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tcpTask.Add(Task.Run(() => server.RunTcp()));
        }
        var udpTask = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            udpTask.Add(Task.Run(() => server.RunUdp()));
        }

        for (int i = 0; i < 10; i++)
        {
            tcpTask[i].Wait();
            udpTask[i].Wait();
        }
    }
    catch (Exception e)
    {
        Log.Logger.Fatal($"Ultra error: {e}");
    }
}