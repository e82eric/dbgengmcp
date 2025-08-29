using System.Text;
using Microsoft.Extensions.Logging;

namespace DbgEngMcp;

public class DebuggerImpl : IDebugOutputCallbacks
{
    private readonly IDebugClient _client;
    private readonly IDebugControl _control;
    private readonly ILogger<DebuggerImpl> _logger;
    private readonly StringBuilder _outputStringBuilder;
    
    //Careful using the to indicate if the debugger is open also
    private string? _debuggableFilePath;
    
    private readonly object _bufferLock = new();
    private readonly object _dbgEngLock = new();

    public DebuggerImpl(IDebugClient client, IDebugControl debugControl, ILoggerFactory loggerFactory)
    {
        _client = client;
        _control = debugControl;
        _logger = loggerFactory.CreateLogger<DebuggerImpl>();
        _outputStringBuilder = new StringBuilder();
    }
    
    public DebuggerResult OpenDebuggableFile(string path)
    {
        _logger.LogInformation("Opening debuggable file: {path}", path);

        lock (_dbgEngLock)
        {
            if (_debuggableFilePath != null)
            {
                var msg = $"Debugger already attached to: {_debuggableFilePath}";
                _logger.LogError(msg);
                return DebuggerResult.Fail(msg);
            }

            if(!Native.CheckHr(_client.OpenDumpFile(path), nameof(_client.OpenDumpFile), _logger, out var errorMessage))
            {
                return DebuggerResult.Fail(errorMessage);
            }
            if (!Native.CheckHr(_control.WaitForEvent(0, Native.INFINITE), nameof(_control.WaitForEvent), _logger, out errorMessage))
            {
                return DebuggerResult.Fail(errorMessage);
            }

            _debuggableFilePath = path;
        }
        _logger.LogInformation("Opened debuggable file: {path}", path);
        return DebuggerResult.Ok();
    }

    public DebuggerResult<string?> ExecuteCommand(string command)
    {
        _logger.LogTrace("Executing command: {command}", command);
        lock (_dbgEngLock)
        {
            if (_debuggableFilePath is null)
            {
                return DebuggerResult<string?>.Fail("No debuggable file is open");
            }

            lock (_bufferLock)
            {
                _outputStringBuilder.Clear();
            }
            if(!Native.CheckHr(
                   _control.Execute((int)Native.DEBUG_OUTCTL_ALL_CLIENTS, command, (int)Native.DEBUG_EXECUTE_DEFAULT),
                   nameof(_control.Execute),
                   _logger,
                   out var errorMessage))
            {
                return DebuggerResult<string?>.Fail(errorMessage);
            }

            lock (_bufferLock)
            {
                var result = _outputStringBuilder.ToString();
                _logger.LogTrace("Done Executing command: {command}, result: {result}", command, result);
                return DebuggerResult<string?>.Ok(result);
            }
        }
    }

    public DebuggerResult Close()
    {
        lock (_dbgEngLock)
        {
            _logger.LogInformation("Closing debuggable file: {path}", _debuggableFilePath);
            if (!Native.CheckHr(_client.EndSession(Native.DEBUG_END_PASSIVE), nameof(_client.EndSession), _logger, out var errorMessage))
            {
                return DebuggerResult.Fail(errorMessage);
            }
        }
        
        _logger.LogInformation("Done closing debuggable file: {path}", _debuggableFilePath);
        return DebuggerResult.Ok();
    }
    
    public int Output(uint mask, string text)
    {
        lock (_bufferLock)
        {
            _outputStringBuilder.Append(text);
        }
        return 0;
    }
}