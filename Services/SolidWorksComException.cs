using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester.Services
{
    /// <summary>Detects and classifies SOLIDWORKS COM / RPC failures.</summary>
    public static class SolidWorksComException
    {
        private const int RpcServerUnavailable = unchecked((int)0x800706BA);
        private const int RpcServerDisconnected = unchecked((int)0x800706BE);
        private const int RpcCallFailed = unchecked((int)0x800706BF);
        private const int RpcDisconnected = unchecked((int)0x80010108);
        private const int ObjectDisconnected = unchecked((int)0x800401FD);

        public static bool IsConnectionFailure(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is InvalidComObjectException)
                    return true;

                if (current is COMException com && IsConnectionHResult(com.HResult))
                    return true;
            }

            return false;
        }

        public static bool IsConnectionHResult(int hResult) =>
            hResult is RpcServerUnavailable
                or RpcServerDisconnected
                or RpcCallFailed
                or RpcDisconnected
                or ObjectDisconnected;

        public static bool IsAlive(ISldWorks? app)
        {
            if (app == null)
                return false;

            try
            {
                _ = app.RevisionNumber();
                return true;
            }
            catch (Exception ex) when (IsConnectionFailure(ex))
            {
                return false;
            }
        }
    }
}
