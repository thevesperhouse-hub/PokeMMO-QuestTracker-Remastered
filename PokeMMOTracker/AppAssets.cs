using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokeMMOTracker;

public static class AppAssets
{
	private static readonly Uri Base = new Uri("pack://application:,,,/");

	// Characters with a custom hub avatar (expand as you add more art per char).
	private static readonly string[] HubAvatarCharacters = { "UltimateFa" };

	public static bool HasHubAvatar(string charName)
	{
		if (string.IsNullOrEmpty(charName)) return false;
		foreach (string n in HubAvatarCharacters)
			if (n == charName) return true;
		return false;
	}

	public static ImageSource AvatarFull => LoadPng("Assets/avatar_full.png");
	public static ImageSource AvatarCropped => LoadPng("Assets/avatar_cropped.png");

	public static ImageSource AppIcon
	{
		get
		{
			var decoder = new IconBitmapDecoder(
				new Uri(Base, "app.ico"),
				BitmapCreateOptions.None,
				BitmapCacheOption.OnLoad);
			var frame = decoder.Frames[0];
			frame.Freeze();
			return frame;
		}
	}

	private static ImageSource LoadPng(string path)
	{
		var img = new BitmapImage();
		img.BeginInit();
		img.UriSource = new Uri(Base, path);
		img.CacheOption = BitmapCacheOption.OnLoad;
		img.EndInit();
		img.Freeze();
		return img;
	}
}
