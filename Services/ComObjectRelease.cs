using System;
using System.Runtime.InteropServices;

namespace SolidWorksTester.Services
{
    /// <summary>
    /// COM RCW release helpers. The CLR GC does not free SOLIDWORKS-side memory
    /// until Runtime Callable Wrappers are released.
    /// </summary>
    internal static class ComObjectRelease
    {
        /// <summary>
        /// Releases a COM RCW without closing the SOLIDWORKS process.
        /// Safe for both attached (ROT) and newly started instances — only drops our reference.
        /// </summary>
        public static void Release(object? comObject)
        {
            if (comObject == null || !Marshal.IsComObject(comObject))
                return;

            try
            {
                Marshal.FinalReleaseComObject(comObject);
            }
            catch (ArgumentException)
            {
                // Already released / not a COM object wrapper.
            }
            catch (InvalidComObjectException)
            {
                // RCW already severed.
            }
        }

        /// <summary>Forces CLR finalization of leftover RCWs after batch COM work.</summary>
        public static void CollectRcws()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
