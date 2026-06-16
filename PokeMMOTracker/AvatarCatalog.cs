using System;
using System.Linq;

namespace PokeMMOTracker;

public static class AvatarCatalog
{
	public sealed class Entry
	{
		public string Id { get; }
		public string FullFile { get; }
		public string CroppedFile { get; }

		public Entry(string id, string fullFile, string croppedFile)
		{
			Id = id;
			FullFile = fullFile;
			CroppedFile = croppedFile;
		}
	}

	public const string DefaultId = "fille";

	public static readonly Entry[] All = new[]
	{
		new Entry("fille", "avatar_full.png", "avatar_cropped.png"),
		new Entry("fille_asian", "avatar_full_fille_asian.png", "avatar_cropped_fille_asian.png"),
		new Entry("fille_dark", "avatar_full_fille_dark.png", "avatar_cropped_fille_dark.png"),
		new Entry("fille_metisse", "avatar_full_fille_metisse.png", "avatar_cropped_fille_metisse.png"),
		new Entry("garcon", "avatar_full_male.png", "avatar_cropped_male.png"),
		new Entry("garcon_asian", "avatar_full_garçon_asian.png", "avatar_cropped_garçon_asian.png"),
		new Entry("garcon_dark", "avatar_full_garçon_dark.png", "avatar_cropped_garçon_dark.png"),
		new Entry("garcon_metisse", "avatar_full_garçon_metisse.png", "avatar_cropped_garçon_metisse.png"),
	};

	public static Entry? Find(string? id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		id = MigrateLegacyId(id);
		return All.FirstOrDefault(e => e.Id == id);
	}

	public static bool IsValid(string? id) => Find(id) != null;

	public static string MigrateLegacyId(string id)
	{
		if (id == "female") return "fille";
		if (id == "male") return "garcon";
		return id;
	}

	public static string ResolveId(string? id)
		=> IsValid(id) ? MigrateLegacyId(id!) : DefaultId;
}
