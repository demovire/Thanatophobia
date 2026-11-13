using System;
using System.Runtime.InteropServices;
using MelonLoader;

namespace Thanatophobia;

// Stubs the game's anti-mod checks in GameAssembly.dll so they return "clean" instead of quitting or
// crashing.
internal static class AntiTamper {
    // Check-function RVAs (IDA VA - 0x180000000), found by reversing this build.
    private static readonly int[] Checks = {
        0xF9C0C0, 0xF9C320,                                     // loaded-assembly / scene-object scanners (quit)
        0xD4B9B0, 0xD4BE30, 0xD4BFE0, 0xD4C4A0,                 // integrity checks (hard-crash), cluster A
        0x123DB10, 0x123DFB0, 0x123E160, 0x123E6C0, 0x123ED30,  // cluster B
    };

    private const long ExpectedSize = 46089216; // GameAssembly.dll of the build the RVAs were taken from

    private static readonly byte[] RetZero = { 0x31, 0xC0, 0xC3 }; // xor eax,eax ; ret

    public static void Apply() {
        var module = GetModuleHandle("GameAssembly.dll");
        if (module == IntPtr.Zero) {
            MelonLogger.Error("[AntiTamper] GameAssembly.dll not found; anti-tamper NOT disabled.");
            return;
        }

        // The RVAs only fit this exact build; on any other one they'd land in unrelated code and corrupt it.
        var size = new System.IO.FileInfo(System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.GameRootDirectory, "GameAssembly.dll")).Length;
        if (size != ExpectedSize) {
            MelonLogger.Error($"[AntiTamper] unsupported game build (GameAssembly.dll is {size} bytes, expected {ExpectedSize}); anti-tamper NOT disabled.");
            return;
        }

        var patched = 0;
        foreach (var rva in Checks)
            if (Write(module + rva, RetZero)) patched++;
            else MelonLogger.Error($"[AntiTamper] failed to patch RVA 0x{rva:X}.");
        MelonLogger.Msg($"[AntiTamper] disabled {patched}/{Checks.Length} check(s).");
    }

    private static bool Write(IntPtr addr, byte[] bytes) {
        var size = (UIntPtr)(uint)bytes.Length;
        if (!VirtualProtect(addr, size, PAGE_EXECUTE_READWRITE, out var old)) return false;
        Marshal.Copy(bytes, 0, addr, bytes.Length);
        VirtualProtect(addr, size, old, out _);
        FlushInstructionCache(GetCurrentProcess(), addr, size);
        return true;
    }

    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string moduleName);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);
    [DllImport("kernel32")]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    [DllImport("kernel32")]
    private static extern IntPtr GetCurrentProcess();
}
