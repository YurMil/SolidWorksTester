using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Services
{
    public sealed class SolidWorksConnectionResult
    {
        public ISldWorks App { get; init; } = null!;
        public bool IsNewInstance { get; init; }
    }

    public static class SolidWorksConnection
    {
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr pvReserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        private static readonly Guid SldWorksGuid = new("96749377-3391-11D2-9EE3-00C04F797396");

        public static SolidWorksConnectionResult Connect(Action<string>? log = null)
        {
            void Write(string message) => log?.Invoke(message);

            CleanupGhostProcesses(Write);

            ISldWorks? swApp = null;
            var isNewInstance = false;

            try
            {
                Guid clsid = SldWorksGuid;
                GetActiveObject(ref clsid, IntPtr.Zero, out object swAppObj);
                swApp = (ISldWorks)swAppObj;
                Write("Connected to running SOLIDWORKS.");
            }
            catch
            {
                Write("Could not connect to running instance (UAC or ROT).");

                if (HasVisibleSolidWorksWindow(Write))
                    throw new InvalidOperationException(
                        "SOLIDWORKS is running but connection failed. Run this app and SOLIDWORKS with the same privileges (both as admin or both as normal user).");

                Write("Starting a new SOLIDWORKS instance...");
                Type? swType = Type.GetTypeFromProgID("SldWorks.Application")
                    ?? throw new InvalidOperationException("COM type SldWorks.Application is not registered.");

                swApp = (ISldWorks)Activator.CreateInstance(swType)!;
                swApp.Visible = true;
                isNewInstance = true;
                Write("New SOLIDWORKS instance started.");
            }

            Write($"SOLIDWORKS version: {swApp.RevisionNumber()}");
            return new SolidWorksConnectionResult { App = swApp, IsNewInstance = isNewInstance };
        }

        private static void CleanupGhostProcesses(Action<string> write)
        {
            try
            {
                foreach (Process proc in Process.GetProcessesByName("SLDWORKS"))
                {
                    if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                        continue;

                    try
                    {
                        write($"Terminating background SOLIDWORKS process (PID: {proc.Id})...");
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch (Exception ex)
                    {
                        write($"Could not terminate PID {proc.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                write($"Warning during process cleanup: {ex.Message}");
            }
        }

        private static bool HasVisibleSolidWorksWindow(Action<string> write)
        {
            try
            {
                foreach (Process proc in Process.GetProcessesByName("SLDWORKS"))
                {
                    if (string.IsNullOrEmpty(proc.MainWindowTitle))
                        continue;

                    write($"Detected SOLIDWORKS window: '{proc.MainWindowTitle}' (PID: {proc.Id}).");
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }
}
