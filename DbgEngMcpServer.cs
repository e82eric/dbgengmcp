using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DbgEngMcp;

public class DbgEngMcpServer
{
    private readonly ILogger<DbgEngMcpServer> _logger;
    private readonly DebuggerClientFactory _debuggerFactory;
    private const string OpenDumpfile = "OpenDumpFile";
    private const string RunWinDbgCommand = "RunWindbgCommand";
    private const string CloseDumpFile = "CloseMemoryDumpOrTraceFile";
    private const string FilePathParamName = "filePath";
    private const string CommandParamName = "command";
    private readonly ConcurrentDictionary<string, DebuggerImpl> _debuggers = new(StringComparer.OrdinalIgnoreCase);

    public DbgEngMcpServer(DebuggerClientFactory debuggerFactory, ILoggerFactory loggerFactory)
    {
        _debuggerFactory = debuggerFactory;
        _logger = loggerFactory.CreateLogger<DbgEngMcpServer>();
    }
    
    static JsonElement CloseOrOpenDumpInputSchema()
    {
        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filePath = new
                {
                    type = "string",
                    description = "Full file path for .dmp (memory dump) or .run (time travel trace) file"
                }
            },
            required = new[] { FilePathParamName }
        });
    }

    static JsonElement RunWinDbgCommandSchema()
    {
        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filePath = new
                {
                    type = "string",
                    description = "Full file path for .dmp (memory dump) or .run (time travel trace) file"
                },
                command = new
                {
                    type = "string",
                    description = "Command to execute"
                }
            },
            required = new[] { FilePathParamName, CommandParamName }
        });
    }

    private static ListToolsResult ListTools()
    {
        var result = new ListToolsResult
        {
            Tools =
            [
                new Tool
                {
                    Name = OpenDumpfile,
                    Description = "Open a dump file or time trave trace file in windbg",
                    InputSchema = CloseOrOpenDumpInputSchema(),
                },
                new Tool
                {
                    Name = RunWinDbgCommand,
                    Description = "Execute a command in WinDbg",
                    InputSchema = RunWinDbgCommandSchema(),
                },
                new Tool
                {
                    Name = CloseDumpFile,
                    Description = "Closes dump file in Windbg",
                    InputSchema = CloseOrOpenDumpInputSchema()
                }
            ]
        };
        return result;
    }

    static string ReadRequiredStringArg(
        IReadOnlyDictionary<string, JsonElement>? args,
        string name)
    {
        if (args is null || !args.TryGetValue(name, out var el))
        {
            throw new McpException($"Missing required argument '{name}'");
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            throw new McpException($"Argument '{name}' must be a string");
        }

        var s = el.GetString();
        if (string.IsNullOrEmpty(s))
        {
            throw new McpException($"Argument '{name}' cannot be empty");
        }

        return s;
    }

    static CallToolResult TextResult(bool isError, string text) => new()
    {
        IsError = isError,
        Content = [new TextContentBlock { Type = "text", Text = text }]
    };
    
    private bool TryNormalizePath(string p, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        try
        {
            normalized = Path.GetFullPath(p);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to normalize path");
            return false;
        }
        _logger.LogTrace("Normalized path to {originalPath} {normalizePath}", p, normalized);
        return true;
    }

    ValueTask<CallToolResult> HandleOpen(RequestContext<CallToolRequestParams> request)
    {
        var filePath = ReadRequiredStringArg(request.Params!.Arguments, FilePathParamName);
        if (!TryNormalizePath(filePath, out var normalizedPath))
        {
            return ValueTask.FromResult(TextResult(true, $"Failed to parse path: {filePath}"));
        }
        
        if (_debuggers.TryGetValue(normalizedPath, out _))
        {
            return ValueTask.FromResult(TextResult(false, $"{filePath} already opened"));
        }

        if (!_debuggerFactory.TryCreate(out var debugger, out var errorMessage))
        {
            return ValueTask.FromResult(TextResult(true, errorMessage));
        }
        
        var r = debugger.OpenDebuggableFile(normalizedPath);
        if (!r.Success)
        {
            //This won't throw and isn't disposable
            debugger.Close();
            return ValueTask.FromResult(TextResult(isError: true, text: r.ErrorMessage!));
        }

        if (_debuggers.TryAdd(normalizedPath, debugger))
        {
            return ValueTask.FromResult(TextResult(isError: false, text: $"{normalizedPath} loaded successfully"));
        }
        
        // debugger already added by another request
        debugger.Close();
        return ValueTask.FromResult(TextResult(isError: false, text: $"{normalizedPath} already open"));
    }

    ValueTask<CallToolResult> HandleRun(RequestContext<CallToolRequestParams> request)
    {
        var filePath = ReadRequiredStringArg(request.Params!.Arguments, FilePathParamName);
        if (!TryNormalizePath(filePath, out var normalizedPath))
        {
            return ValueTask.FromResult(TextResult(true, $"Failed to parse path: {filePath}"));
        }
        
        if (!_debuggers.TryGetValue(normalizedPath, out var debugger))
        {
            return ValueTask.FromResult(TextResult(true, $"no open debugger found for {normalizedPath}"));
        }
        var command = ReadRequiredStringArg(request.Params!.Arguments, CommandParamName);
        var r = debugger.ExecuteCommand(command);
        return ValueTask.FromResult(TextResult(!r.Success, r.Success ? r.Result! : r.ErrorMessage));
    }

    ValueTask<CallToolResult> HandleClose(RequestContext<CallToolRequestParams> request)
    {
        var filePath = ReadRequiredStringArg(request.Params!.Arguments, FilePathParamName);
        if (!TryNormalizePath(filePath, out var normalizedPath))
        {
            return ValueTask.FromResult(TextResult(true, $"Failed to parse path: {filePath}"));
        }
        if (!_debuggers.TryRemove(normalizedPath, out var debugger))
        {
            return ValueTask.FromResult(TextResult(true, $"no open debugger found for {normalizedPath}"));
        }
        var r = debugger.Close();
        return ValueTask.FromResult(TextResult(!r.Success, r.Success ? $"Memory dump closed {normalizedPath}" : r.ErrorMessage));
    }
    
    public ValueTask<ListToolsResult> HandleListTools(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HandleListTools called");
        var result = ListTools();
        _logger.LogInformation($"Returning {result.Tools.Count} tools");
        return ValueTask.FromResult(result);
    }
    
    public ValueTask<CallToolResult> Handle(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        return request.Params?.Name switch
        {
            OpenDumpfile => HandleOpen(request),
            RunWinDbgCommand => HandleRun(request),
            CloseDumpFile => HandleClose(request),
            _ => throw new McpException($"Unknown tool: '{request.Params?.Name}'")
        };
    }
}