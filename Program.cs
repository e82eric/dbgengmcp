using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Events;
using DbgEngMcp;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/dbgeng.log",
        restrictedToMinimumLevel: LogEventLevel.Information,
        shared: true,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} "
        + "{Message:lj}{NewLine}{Exception}")
    .CreateLogger();

ILoggerFactory loggerFactory = LoggerFactory.Create(lb =>
{
    lb.ClearProviders();
    lb.AddSerilog(Log.Logger, dispose: false);
});

if (!TryCreateMcpServer(args, loggerFactory, out var mcpServer, out var errorMessage))
{
    Console.Error.WriteLine($"Failed to create mcp server. {errorMessage}");
    Environment.Exit(1);
}

McpServerOptions options = new()
{
    ServerInfo = new Implementation { Name = "DbgEngServer", Version = "1.0.0" },
    Capabilities = new ServerCapabilities
    {
        Tools = new ToolsCapability
        {
            ListToolsHandler = mcpServer.HandleListTools,
            CallToolHandler = mcpServer.Handle,
        }
    },
};

await using var server = McpServerFactory.Create(new StdioServerTransport("DbgEngServer"), options, loggerFactory);
await server.RunAsync();

return 0;

static bool TryCreateMcpServer(string[] args, ILoggerFactory loggerFactory, [NotNullWhen(true)] out DbgEngMcpServer? server, out string? errorMessage)
{
    errorMessage = null;
    server = null;
    var logger = loggerFactory.CreateLogger(nameof(TryCreateMcpServer));
    var amd64Dir = args[0].TrimEnd('\\');
    var dbgengPath = Path.Combine(amd64Dir, "dbgeng.dll");
    var ttdDir = Path.Combine(amd64Dir, "ttd");

    if (!Native.CheckHr(Native.CoInitializeEx(IntPtr.Zero, Native.COINIT.COINIT_MULTITHREADED), nameof(Native.CoInitializeEx), logger, out errorMessage))
    {
        return false;
    }

    if (!Native.SetDllDirectoryW(amd64Dir))
    {
        errorMessage = Native.GetLastError();
        logger.LogError("Failed to set dll directory {errorMessage} {directory}", errorMessage, amd64Dir);
        return false;
    }

    if (Native.AddDllDirectory(ttdDir) == IntPtr.Zero)
    {
        var message = Native.GetLastError();
        logger.LogWarning("Failed to add dll directory {directory}, ttd commands may not work. {errorMessage}", ttdDir, errorMessage);
    }

    if (Native.LoadLibraryW(dbgengPath) == IntPtr.Zero)
    {
        errorMessage = Native.GetLastError();
        logger.LogError("Failed to load dbgeng dll {errorMessage}", errorMessage);
        return false;
    }

    IntPtr h = Native.GetModuleHandleW("dbgeng.dll");
    if (h != IntPtr.Zero)
    {
        var sb = new StringBuilder(260);
        if (Native.GetModuleFileNameW(h, sb, sb.Capacity) > 0)
        {
            logger.LogInformation("{dll} loaded from: {fileName}", "dbgeng.dll", sb);
        }
    }
    
    server = new DbgEngMcpServer(new DebuggerClientFactory(loggerFactory), loggerFactory);
    return true;
}