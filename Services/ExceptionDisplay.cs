using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SolidWorksTester.Services
{
    internal static class ExceptionDisplay
    {
        public static string GetUserMessage(Exception ex)
        {
            ex = Unwrap(ex);

            if (!string.IsNullOrWhiteSpace(ex.Message))
                return ex.Message.Trim();

            if (ex is FileNotFoundException fnf &&
                (fnf.FileName?.Contains("SolidWorks.Interop", StringComparison.OrdinalIgnoreCase) == true ||
                 fnf.Message.Contains("SolidWorks.Interop", StringComparison.OrdinalIgnoreCase)))
            {
                return "SOLIDWORKS interop assemblies are missing from the published application.\n\n" +
                       "Rebuild with NuGet interop (SolidWorks.Interop 32.1.0) included in the output, " +
                       "or install SOLIDWORKS 2022–2026 on this PC.";
            }

            if (ex is COMException com)
                return DescribeComHResult(com.HResult);

            return $"{ex.GetType().Name}. See the log for technical details.";
        }

        public static string GetLogSummary(Exception ex)
        {
            ex = Unwrap(ex);
            var sb = new StringBuilder();
            sb.Append(GetUserMessage(ex));

            if (ex is COMException com)
                sb.Append($" [HRESULT: 0x{com.HResult & 0xFFFFFFFF:X8}]");

            return sb.ToString();
        }

        public static string GetFullDetails(Exception ex) => Unwrap(ex).ToString();

        private static Exception Unwrap(Exception ex)
        {
            if (ex is AggregateException aggregate)
            {
                aggregate = aggregate.Flatten();
                if (aggregate.InnerExceptions.Count == 1)
                    return aggregate.InnerExceptions[0];
            }

            return ex;
        }

        private static string DescribeComHResult(int hResult)
        {
            int code = hResult & 0xFFFF;
            return code switch
            {
                unchecked((int)0x06BA) => "SOLIDWORKS COM connection lost (RPC server unavailable). Restart SOLIDWORKS and run the app with the same user privileges.",
                unchecked((int)0x06BE) => "SOLIDWORKS COM call failed (RPC fault).",
                unchecked((int)0x06BF) => "SOLIDWORKS COM connection was disconnected.",
                unchecked((int)0x0108) => "SOLIDWORKS COM object disconnected from its server.",
                unchecked((int)0x01FD) => "SOLIDWORKS COM object is no longer available.",
                _ => $"COM error (HRESULT: 0x{hResult & 0xFFFFFFFF:X8})."
            };
        }
    }
}
