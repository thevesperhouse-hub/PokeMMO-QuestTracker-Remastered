using System.Collections.Generic;

namespace PokeMMOTracker.Models;

public class ShowUserProgress
{
	public int RegionId { get; set; }

	public string Title { get; set; }

	public List<(string label, int isDone)> labels { get; set; }
}
