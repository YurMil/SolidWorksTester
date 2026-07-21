using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.SolidWorks;

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
                swApp = swAppObj as ISldWorks
                    ?? throw new InvalidCastException("ROT object is not ISldWorks.");
                Write("Connected to running SOLIDWORKS.");
            }
            catch (InvalidCastException)
            {
                throw;
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

                object created = Activator.CreateInstance(swType)
                    ?? throw new InvalidOperationException("Failed to create SldWorks.Application.");
                swApp = created as ISldWorks
                    ?? throw new InvalidCastException("Created COM object is not ISldWorks.");
                swApp.Visible = true;
                isNewInstance = true;
                Write("New SOLIDWORKS instance started.");
                WaitForSolidWorksReady(swApp, Write);
            }

            Write($"SOLIDWORKS version: {swApp.RevisionNumber()}");

            try
            {
                string revision = swApp.RevisionNumber();
                // Year comes from the live API revision; match install folder for that year only
                // (do not pass DiscoverBest — that may be a newer install than the one Windows launched).
                SolidWorksVersionContext.UpdateFromRunningInstance(revision);
                Write(SolidWorksVersionContext.Current.Summary);
                Write(SolidWorksCapabilityRouter.GetStrategyNotes());
            }
            catch (Exception ex)
            {
                Write($"Warning: could not update version router: {ex.Message}");
            }

            return new SolidWorksConnectionResult { App = swApp, IsNewInstance = isNewInstance };
        }

        /// <summary>Returns a live <see cref="ISldWorks"/> instance, reconnecting if the previous one died.</summary>
        public static ISldWorks EnsureConnected(ISldWorks? current, Action<string>? log = null)
        {
            if (SolidWorksComException.IsAlive(current))
                return current!;

            if (current != null)
            {
                log?.Invoke("SOLIDWORKS COM connection lost — reconnecting...");
                ComObjectRelease.Release(current);
            }

            return Connect(log).App;
        }

        /// <summary>
        /// Drops our ISldWorks RCW after a batch. Does not call ExitApp — SOLIDWORKS stays open
        /// for the user to inspect drawings (whether we attached or started it).
        /// </summary>
        public static void ReleaseApp(ref ISldWorks? app, Action<string>? log = null)
        {
            if (app == null)
                return;

            try
            {
                ComObjectRelease.Release(app);
                log?.Invoke("Released SOLIDWORKS COM reference (RCW).");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: COM release failed: {ex.Message}");
            }
            finally
            {
                app = null;
                ComObjectRelease.CollectRcws();
            }
        }

        private static void WaitForSolidWorksReady(ISldWorks swApp, Action<string> write)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    _ = swApp.RevisionNumber();
                    return;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }

            write("Warning: SOLIDWORKS started but API may not be fully ready yet.");
        }

        public static void SafeCloseDocument(ISldWorks swApp, IModelDoc2? model, Action<string>? log = null)
        {
            if (model == null)
                return;

            try
            {
                string title = model.GetTitle();
                if (!string.IsNullOrWhiteSpace(title))
                    swApp.CloseDoc(title);
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                log?.Invoke($"Warning: could not close document (COM unavailable): {ex.Message}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: could not close document: {ex.Message}");
            }
        }

        public static void SafeCloseDocumentByPath(ISldWorks swApp, string path, Action<string>? log = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                swApp.CloseDoc(path);
            }
            catch (Exception ex) when (SolidWorksComException.IsConnectionFailure(ex))
            {
                log?.Invoke($"Warning: could not close document (COM unavailable): {ex.Message}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Warning: could not close document: {ex.Message}");
            }
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
                        Thread.Sleep(1500);
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
