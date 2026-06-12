namespace PokeMMOTracker.Models;

public class KantoProgressClass
{
	public int KantoProgressId { get; set; }

	public int KantoId { get; set; }

	public string TaskLabel { get; set; }

	public bool IsDone { get; set; }
}
