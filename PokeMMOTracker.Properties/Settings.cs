using System.CodeDom.Compiler;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PokeMMOTracker.Properties;

[CompilerGenerated]
[GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.12.0.0")]
public sealed class Settings : ApplicationSettingsBase
{
	private static Settings defaultInstance = (Settings)SettingsBase.Synchronized(new Settings());

	public static Settings Default => defaultInstance;

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#225883")]
	public string Background
	{
		get
		{
			return (string)this["Background"];
		}
		set
		{
			this["Background"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#FFFFFF")]
	public string ButtonBackground
	{
		get
		{
			return (string)this["ButtonBackground"];
		}
		set
		{
			this["ButtonBackground"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#225883")]
	public string ButtonText
	{
		get
		{
			return (string)this["ButtonText"];
		}
		set
		{
			this["ButtonText"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#CCCCCC")]
	public string TaskOddBackground
	{
		get
		{
			return (string)this["TaskOddBackground"];
		}
		set
		{
			this["TaskOddBackground"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#225883")]
	public string TaskOddText
	{
		get
		{
			return (string)this["TaskOddText"];
		}
		set
		{
			this["TaskOddText"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#EEEEEE")]
	public string TaskEvenBackground
	{
		get
		{
			return (string)this["TaskEvenBackground"];
		}
		set
		{
			this["TaskEvenBackground"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#225883")]
	public string TaskEvenText
	{
		get
		{
			return (string)this["TaskEvenText"];
		}
		set
		{
			this["TaskEvenText"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("#225883")]
	public string CheckBoxBorder
	{
		get
		{
			return (string)this["CheckBoxBorder"];
		}
		set
		{
			this["CheckBoxBorder"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("12")]
	public int FontSize
	{
		get
		{
			return (int)this["FontSize"];
		}
		set
		{
			this["FontSize"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("")]
	public string LastUser
	{
		get
		{
			return (string)this["LastUser"];
		}
		set
		{
			this["LastUser"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool RememberMe
	{
		get
		{
			return (bool)this["RememberMe"];
		}
		set
		{
			this["RememberMe"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("")]
	public string LastRegion
	{
		get
		{
			return (string)this["LastRegion"];
		}
		set
		{
			this["LastRegion"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool RememberScroll
	{
		get
		{
			return (bool)this["RememberScroll"];
		}
		set
		{
			this["RememberScroll"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("Down")]
	public string CheckKey
	{
		get
		{
			return (string)this["CheckKey"];
		}
		set
		{
			this["CheckKey"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("Control")]
	public string CheckModifiers
	{
		get
		{
			return (string)this["CheckModifiers"];
		}
		set
		{
			this["CheckModifiers"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("Up")]
	public string UncheckKey
	{
		get
		{
			return (string)this["UncheckKey"];
		}
		set
		{
			this["UncheckKey"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("Control")]
	public string UncheckModifiers
	{
		get
		{
			return (string)this["UncheckModifiers"];
		}
		set
		{
			this["UncheckModifiers"] = value;
		}
	}

	// SDL_GameControllerButton int values: RIGHTSHOULDER = 10, LEFTSHOULDER = 9.
	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("10")]
	public int CheckButton
	{
		get
		{
			return (int)this["CheckButton"];
		}
		set
		{
			this["CheckButton"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("9")]
	public int UncheckButton
	{
		get
		{
			return (int)this["UncheckButton"];
		}
		set
		{
			this["UncheckButton"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("EN")]
	public string Language
	{
		get
		{
			return (string)this["Language"];
		}
		set
		{
			this["Language"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("-1")]
	public double OverlayLeft
	{
		get
		{
			return (double)this["OverlayLeft"];
		}
		set
		{
			this["OverlayLeft"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("-1")]
	public double OverlayTop
	{
		get
		{
			return (double)this["OverlayTop"];
		}
		set
		{
			this["OverlayTop"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("450")]
	public double OverlayWidth
	{
		get
		{
			return (double)this["OverlayWidth"];
		}
		set
		{
			this["OverlayWidth"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("600")]
	public double OverlayHeight
	{
		get
		{
			return (double)this["OverlayHeight"];
		}
		set
		{
			this["OverlayHeight"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool CompactMode
	{
		get
		{
			return (bool)this["CompactMode"];
		}
		set
		{
			this["CompactMode"] = value;
		}
	}

	// 0 = full, 1 = compact (single quest + footer), 2 = minimal (single quest, no footer).
	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("0")]
	public int ViewMode
	{
		get
		{
			return (int)this["ViewMode"];
		}
		set
		{
			this["ViewMode"] = value;
		}
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool NarratorEnabled
	{
		get => (bool)this["NarratorEnabled"];
		set => this["NarratorEnabled"] = value;
	}

	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("False")]
	public bool NarratorAutoRead
	{
		get => (bool)this["NarratorAutoRead"];
		set => this["NarratorAutoRead"] = value;
	}

	// Neural Edge voices (online, much more natural than legacy Windows SAPI).
	[UserScopedSetting]
	[DebuggerNonUserCode]
	[DefaultSettingValue("True")]
	public bool NarratorNeural
	{
		get => (bool)this["NarratorNeural"];
		set => this["NarratorNeural"] = value;
	}
}
