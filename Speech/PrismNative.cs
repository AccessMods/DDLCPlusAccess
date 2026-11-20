using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Binding for Prism 0.18.4 (https://github.com/ethindp/prism, MPL-2.0), the library
    /// that routes speech to NVDA, JAWS, Narrator and others, or the Windows voices when no screen
    /// reader runs. Signatures follow lib/prism/prism.h. Only <see cref="SpeechService"/> calls this.
    ///
    /// Notes for Mono / .NET Framework:
    /// - prism.dll is loaded by full path and every function is taken from that module with
    ///   GetProcAddress. [DllImport("prism")] is not used: it makes Mono resolve the bare name
    ///   itself, which on some machines throws DllNotFoundException even though our copy is
    ///   already loaded, and it could bind to another mod's prism.dll of a different version.
    /// - Text goes as a UTF-8 byte array ending with a zero; default string marshalling is ANSI and
    ///   Prism refuses invalid UTF-8 (silently speaking nothing).
    /// - The C bool is one byte: [MarshalAs(UnmanagedType.I1)].
    /// </summary>
    internal static class PrismNative
    {
        public const int PRISM_OK = 0;

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr InitFn(IntPtr config);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr RegistryCreateBestFn(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr BackendNameFn(IntPtr backend);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BackendOutputFn(
            IntPtr backend,
            byte[] utf8Text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt
        );

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ErrorStringFn(int error);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr VersionStringFn();

        // Set by Load; called like the functions they replace.
        public static InitFn prism_init;
        public static RegistryCreateBestFn prism_registry_create_best;
        public static BackendNameFn prism_backend_name;
        public static BackendOutputFn prism_backend_output;
        public static ErrorStringFn prism_error_string;
        public static VersionStringFn prism_version_string;

        /// <summary>
        /// Loads prism.dll from an explicit path and binds every function from it.
        /// Returns false with a readable reason on failure.
        /// </summary>
        public static bool Load(string fullPath, out string error)
        {
            IntPtr handle = LoadLibraryW(fullPath);
            if (handle == IntPtr.Zero)
            {
                error = "Win32 error " + Marshal.GetLastWin32Error();
                return false;
            }

            try
            {
                prism_init = Bind<InitFn>(handle, "prism_init");
                prism_registry_create_best = Bind<RegistryCreateBestFn>(handle, "prism_registry_create_best");
                prism_backend_name = Bind<BackendNameFn>(handle, "prism_backend_name");
                prism_backend_output = Bind<BackendOutputFn>(handle, "prism_backend_output");
                prism_error_string = Bind<ErrorStringFn>(handle, "prism_error_string");
                prism_version_string = Bind<VersionStringFn>(handle, "prism_version_string");
            }
            catch (EntryPointNotFoundException ex)
            {
                error = ex.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static T Bind<T>(IntPtr module, string name)
            where T : class
        {
            IntPtr address = GetProcAddress(module, name);
            if (address == IntPtr.Zero)
                throw new EntryPointNotFoundException(
                    $"prism.dll has no export '{name}' (Win32 error {Marshal.GetLastWin32Error()})"
                );
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
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
