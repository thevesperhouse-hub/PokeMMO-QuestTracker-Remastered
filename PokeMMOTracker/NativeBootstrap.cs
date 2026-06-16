using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SDL2;

namespace PokeMMOTracker;

/// <summary>
/// Loads SDL2.dll for single-file publish (no side-by-side native DLL required).
/// </summary>
internal static class NativeBootstrap
{
	private const string Sdl2ResourceName = "SDL2.dll";
	private static string? _sdl2Path;

	public static void EnsureSdl2()
	{
		if (_sdl2Path != null)
			return;

		string sideBySide = Path.Combine(AppContext.BaseDirectory, "SDL2.dll");
		if (File.Exists(sideBySide))
		{
			_sdl2Path = sideBySide;
		}
		else
		{
			string nativeDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"PokeMMO-QT",
				"native");
			Directory.CreateDirectory(nativeDir);
			string extracted = Path.Combine(nativeDir, "SDL2.dll");

			if (!File.Exists(extracted))
			{
				using Stream? input = Assembly.GetExecutingAssembly().GetManifestResourceStream(Sdl2ResourceName);
				if (input == null)
					throw new InvalidOperationException("Embedded SDL2.dll is missing from the application build.");

				using FileStream output = File.Create(extracted);
				input.CopyTo(output);
			}

			_sdl2Path = extracted;
		}

		NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, ResolveNativeLibrary);
	}

	private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (libraryName is "SDL2" or "SDL2.dll" && _sdl2Path != null)
			return NativeLibrary.Load(_sdl2Path);

		return IntPtr.Zero;
	}
}
