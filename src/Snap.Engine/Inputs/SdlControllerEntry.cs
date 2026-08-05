namespace Snap.Engine.Inputs;

internal class SdlControllerEntry
{
	public string Guid { get; }
	public string Name { get; }
	public Dictionary<string, SdlBinding> Map { get; }

	public SdlControllerEntry(string guid, string name, Dictionary<string, SdlBinding> map)
	{
		Guid = guid;
		Name = name;
		Map = map;
	}
}