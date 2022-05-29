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
        var tcpTask = Task.Run(() => server.RunTcp());
        var udpTask = Task.Run(() => server.RunUdp());
        tcpTask.Wait();
        udpTask.Wait();   
    }
    catch (Exception e)
    {
        Log.Logger.Fatal($"Ultra error: {e}");
    }
}