namespace PokeMMOTracker.Models;

public class HoennProgressClass
{
	public int HoennProgressId { get; set; }

	public int HoennId { get; set; }

	public string TaskLabel { get; set; }

	public bool IsDone { get; set; }
}
