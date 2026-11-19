using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DDLCPlusAccess
{
    /// <summary>
    /// P/Invoke binding for Prism 0.18.4 (https://github.com/ethindp/prism, MPL-2.0), the library
    /// that routes speech to NVDA, JAWS, Narrator and others, or the Windows voices when no screen
    /// reader runs. Signatures follow lib/prism/prism.h. Only <see cref="SpeechService"/> calls this.
    ///
    /// Notes for Mono / .NET Framework:
    /// - prism.dll is loaded by full path first, so [DllImport("prism")] binds to our copy and not
    ///   to another mod's prism.dll of a different version.
    /// - Text goes as a UTF-8 byte array ending with a zero; default string marshalling is ANSI and
    ///   Prism refuses invalid UTF-8 (silently speaking nothing).
    /// - The C bool is one byte: [MarshalAs(UnmanagedType.I1)].
    /// </summary>
    internal static class PrismNative
    {
        public const int PRISM_OK = 0;

        private const string Dll = "prism";

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_init(IntPtr config);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create_best(IntPtr context);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_output(
            IntPtr backend,
            byte[] utf8Text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt
        );

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_error_string(int error);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_version_string();

        /// <summary>Loads prism.dll from an explicit path. Returns false (with the Win32 error) on failure.</summary>
        public static bool Load(string fullPath, out int win32Error)
        {
            IntPtr handle = LoadLibraryW(fullPath);
            win32Error = handle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
            return handle != IntPtr.Zero;
        }

        /// <summary>Text as zero-terminated UTF-8, the only form Prism accepts.</summary>
        public static byte[] ToUtf8(string text)
        {
            return Encoding.UTF8.GetBytes(text + "\0");
        }

        /// <summary>Reads a zero-terminated UTF-8 string (Marshal.PtrToStringUTF8 is missing on .NET Framework).</summary>
        public static string FromUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                return null;

            var bytes = new List<byte>();
            for (int offset = 0; ; offset++)
            {
                byte b = Marshal.ReadByte(pointer, offset);
                if (b == 0)
                    break;
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static string ErrorString(int error)
        {
            try
            {
                return FromUtf8(prism_error_string(error)) ?? ("error " + error);
            }
            catch
            {
                return "error " + error;
            }
        }
    }
}
