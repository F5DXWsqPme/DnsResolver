using Serilog;
using Serilog.Core;

namespace DnsResolver;

public class Log
{
    public static Logger Logger { get; } = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console()
        /*.WriteTo.File(@"logs.txt")*/
        .CreateLogger();
}