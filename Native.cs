using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

[ComImport, Guid("27FE5639-8407-4F47-8364-EE118FB08AC8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDebugClient
{
    int AttachKernel(uint Flags, [MarshalAs(UnmanagedType.LPStr)] string Options);
    int GetKernelConnectionOptions(IntPtr Buffer, uint BufferSize, out uint OptionsSize);
    int SetKernelConnectionOptions([MarshalAs(UnmanagedType.LPStr)] string Options);
    int StartProcessServer(uint Flags, [MarshalAs(UnmanagedType.LPStr)] string Options, IntPtr Reserved);
    int ConnectProcessServer([MarshalAs(UnmanagedType.LPStr)] string RemoteOptions, out ulong Server);
    int DisconnectProcessServer(ulong Server);
    int GetRunningProcessSystemIds(ulong Server, IntPtr Ids, uint Count, out uint ActualCount);
    int GetRunningProcessSystemIdByExecutableName(ulong Server, [MarshalAs(UnmanagedType.LPStr)] string ExeName, uint Flags, out uint Id);
    int GetRunningProcessDescription(ulong Server, uint SystemId, uint Flags, IntPtr ExeName, uint ExeNameSize, out uint ActualExeNameSize, IntPtr Description, uint DescriptionSize, out uint ActualDescriptionSize);
    int AttachProcess(ulong Server, uint ProcessId, uint AttachFlags);
    int CreateProcess(ulong Server, [MarshalAs(UnmanagedType.LPStr)] string CommandLine, uint CreateFlags);
    int CreateProcessAndAttach(ulong Server, [MarshalAs(UnmanagedType.LPStr)] string CommandLine, uint CreateFlags, uint ProcessId, uint AttachFlags);
    int GetProcessOptions(out uint Options);
    int AddProcessOptions(uint Options);
    int RemoveProcessOptions(uint Options);
    int SetProcessOptions(uint Options);
    [PreserveSig] int OpenDumpFile([MarshalAs(UnmanagedType.LPStr)] string DumpFile); // used
    int WriteDumpFile([MarshalAs(UnmanagedType.LPStr)] string DumpFile, uint Qualifier);
    int ConnectSession(uint Flags, uint HistoryLimit);
    int StartServer([MarshalAs(UnmanagedType.LPStr)] string Options);
    int OutputServers(uint OutputControl, [MarshalAs(UnmanagedType.LPStr)] string Machine, uint Flags);
    int TerminateProcesses();
    int DetachProcesses();
    [PreserveSig] int EndSession(uint Flags); // used
    int GetExitCode(out uint Exit);
    [PreserveSig] int DispatchCallbacks(uint Timeout);
    int ExitDispatch([MarshalAs(UnmanagedType.Interface)] IDebugClient Client);
    int CreateClient(out IDebugClient Client);
    int GetInputCallbacks(out IntPtr Callbacks);
    int SetInputCallbacks(IntPtr Callbacks);
    int GetOutputCallbacks(out IntPtr Callbacks);
    [PreserveSig] int SetOutputCallbacks([MarshalAs(UnmanagedType.Interface)] IDebugOutputCallbacks Callbacks); // used
    int GetOutputMask(out uint Mask);
    int SetOutputMask(uint Mask);
    int GetOtherOutputMask([MarshalAs(UnmanagedType.Interface)] IDebugClient Client, out uint Mask);
    int SetOtherOutputMask([MarshalAs(UnmanagedType.Interface)] IDebugClient Client, uint Mask);
    int GetOutputWidth(out uint Columns);
    int SetOutputWidth(uint Columns);
    int GetOutputLinePrefix(IntPtr Buffer, uint BufferSize, out uint PrefixSize);
    int SetOutputLinePrefix([MarshalAs(UnmanagedType.LPStr)] string Prefix);
    int GetIdentity(IntPtr Buffer, uint BufferSize, out uint IdentitySize);
    int OutputIdentity(uint OutputControl, uint Flags, [MarshalAs(UnmanagedType.LPStr)] string Format);
    int GetEventCallbacks(out IntPtr Callbacks);
    int SetEventCallbacks(IntPtr Callbacks);
    int FlushCallbacks();
}

// Minimal, correctly-ordered IDebugControl with placeholders up to Execute / WaitForEvent.
// (Pattern based on community sample showing slot indices; ensures calls hit the right vtable slots.) :contentReference[oaicite:2]{index=2}
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
 Guid("5182e668-105E-416E-AD92-24EF800424BA")]
public interface IDebugControl
{
    // D01..D63 = placeholders for earlier methods (order matters!)
    int D01(); int D02(); int D03(); int D04(); int D05(); int D06(); int D07();
    int D08(); int D09(); int D10(); int D11(); int D12(); int D13(); int D14();
    int D15(); int D16(); int D17(); int D18(); int D19(); int D20(); int D21();
    int D22(); int D23(); int D24(); int D25(); int D26(); int D27(); int D28();
    int D29(); int D30(); int D31(); int D32(); int D33(); int D34(); int D35();
    int D36(); int D37(); int D38(); int D39(); int D40(); int D41(); int D42();
    int D43(); int D44(); int D45(); int D46(); int D47(); int D48(); int D49();
    int D50(); int D51(); int D52(); int D53(); int D54(); int D55(); int D56();
    int D57(); int D58(); int D59(); int D60(); int D61(); int D62(); int D63();

    // Execute
    [PreserveSig]
    int Execute(int OutputControl, [MarshalAs(UnmanagedType.LPStr)] string Command, int Flags); // :contentReference[oaicite:3]{index=3}

    // D65..D90 placeholders
    int D65(); int D66(); int D67(); int D68(); int D69(); int D70(); int D71();
    int D72(); int D73(); int D74(); int D75(); int D76(); int D77(); int D78();
    int D79(); int D80(); int D81(); int D82(); int D83(); int D84(); int D85();
    int D86(); int D87(); int D88(); int D89(); int D90();

    // WaitForEvent
    [PreserveSig]
    int WaitForEvent(int Flags, int Timeout); // same semantics as native. :contentReference[oaicite:4]{index=4}
}

[ComImport, Guid("4BF58045-D654-4C40-B0AF-683090F356DC")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDebugOutputCallbacks
{
    [PreserveSig]
    int Output(uint Mask, [MarshalAs(UnmanagedType.LPStr)] string Text);
}

internal static class Native
{
    internal const int INFINITE = -1;
    internal const uint DEBUG_OUTCTL_ALL_CLIENTS = 0x00000001;
    internal const uint DEBUG_EXECUTE_DEFAULT    = 0x00000000;
    internal const uint DEBUG_END_PASSIVE        = 0x00000000;
    
    [DllImport("dbgeng.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int DebugCreate(ref Guid InterfaceId,
        [MarshalAs(UnmanagedType.IUnknown)] out object Interface);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool SetDllDirectoryW(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr AddDllDirectory(string NewDirectory);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetModuleFileNameW(IntPtr hModule, StringBuilder lpFilename, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int FormatMessageW(uint dwFlags, IntPtr lpSource, uint dwMessageId,
        uint dwLanguageId, out IntPtr lpBuffer, int nSize, IntPtr Arguments);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("ole32.dll")]
    internal static extern int CoInitializeEx(IntPtr pvReserved, COINIT dwCoInit);

    [DllImport("ole32.dll")]
    internal static extern void CoUninitialize();

    // -------- GUIDs --------
    internal static readonly Guid IID_IDebugClient = new Guid("27FE5639-8407-4F47-8364-EE118FB08AC8");
    
    public static bool CheckHr(int hr, string what, ILogger logger, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;
        var result = true;
        if (hr < 0)
        {
            errorMessage = GetLastError();
            logger.LogError("{call} failed, hr={hr}, message={errorMessage}", what, $"0x{hr:X8}", errorMessage);
            result = false;
        }

        return result;
    }
    
    public static string GetLastError()
    {
        var result = string.Empty;
        int e = Marshal.GetLastWin32Error();
        const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
        const uint FORMAT_MESSAGE_FROM_SYSTEM     = 0x00001000;
        const uint FORMAT_MESSAGE_IGNORE_INSERTS  = 0x00000200;

        int n = Native.FormatMessageW(
            FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
            IntPtr.Zero, (uint)e, 0, out IntPtr buf, 0, IntPtr.Zero);

        if (n != 0 && buf != IntPtr.Zero)
        {
            string msg = Marshal.PtrToStringUni(buf) ?? "";
            msg = msg.TrimEnd('\r', '\n');
            result = msg;
            Native.LocalFree(buf);
        }
        return result;
    }

    internal enum COINIT : uint
    {
        COINIT_MULTITHREADED = 0x0,
        COINIT_APARTMENTTHREADED = 0x2,
    }
}