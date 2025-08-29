using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DbgEngMcp;

public class DebuggerClientFactory
{
    private readonly ILogger<DebuggerClientFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public DebuggerClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DebuggerClientFactory>();
    }
    
    public bool TryCreate(
        [NotNullWhen(true)] out DebuggerImpl? debugger,
        [NotNullWhen(false)] out string? errorMessage)
    {
        debugger = null;
        var iid = Native.IID_IDebugClient;
        if (!Native.CheckHr(Native.DebugCreate(ref iid, out var unkClient), nameof(Native.DebugCreate), _logger, out errorMessage))
        {
            return false;
        }
        
        var client = (IDebugClient)unkClient;
        var control = (IDebugControl)unkClient;

        var localDebugger = new  DebuggerImpl(client, control, _loggerFactory);
        if (!Native.CheckHr(client.SetOutputCallbacks(localDebugger), nameof(TryCreate), _logger, out errorMessage))
        {
            return false;
        }
        
        debugger = localDebugger;

        return true;
    }
}