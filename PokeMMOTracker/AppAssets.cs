using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokeMMOTracker;

public static class AppAssets
{
	private static readonly Uri Base = new Uri("pack://application:,,,/");

	public static ImageSource GetAvatarFull(string avatarId)
	{
		AvatarCatalog.Entry entry = AvatarCatalog.Find(avatarId) ?? AvatarCatalog.Find(AvatarCatalog.DefaultId)!;
		return LoadPng("Assets/" + entry.FullFile);
	}

	public static ImageSource GetAvatarCropped(string avatarId)
	{
		AvatarCatalog.Entry entry = AvatarCatalog.Find(avatarId) ?? AvatarCatalog.Find(AvatarCatalog.DefaultId)!;
		return LoadPng("Assets/" + entry.CroppedFile);
	}

	public static ImageSource AppIcon
	{
		get
		{
			var decoder = new IconBitmapDecoder(
				new Uri(Base, "app.ico"),
				BitmapCreateOptions.None,
				BitmapCacheOption.OnLoad);

			BitmapFrame best = decoder.Frames[0];
			foreach (BitmapFrame frame in decoder.Frames)
				if (frame.PixelWidth > best.PixelWidth)
					best = frame;

			best.Freeze();
			return best;
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
