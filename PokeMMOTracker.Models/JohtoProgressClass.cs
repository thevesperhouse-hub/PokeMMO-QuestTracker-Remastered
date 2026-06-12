namespace PokeMMOTracker.Models;

public class JohtoProgressClass
{
	public int JohtoProgressId { get; set; }

	public int JohtoId { get; set; }

	public string TaskLabel { get; set; }

	public bool IsDone { get; set; }
}
