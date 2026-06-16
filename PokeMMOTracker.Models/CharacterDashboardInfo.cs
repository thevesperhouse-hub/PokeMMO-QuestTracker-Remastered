using System.Collections.Generic;

namespace PokeMMOTracker.Models;

public class CharacterDashboardInfo
{
	public string Name { get; set; }
	public double GlobalPercent { get; set; }
	public Dictionary<string, double> RegionPercents { get; set; } = new Dictionary<string, double>();
	public string ActiveRegion { get; set; }
	public string ActiveZoneTitle { get; set; }
	public int ActiveZoneStep { get; set; }
	public string CurrentQuest { get; set; }
}
