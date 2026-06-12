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
}
