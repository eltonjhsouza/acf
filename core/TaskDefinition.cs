namespace AgentCore.Core;
public class TaskDefinition
{
  public string? Objective { get; set; }
  public Dictionary<string, string>? Constraints { get; set; }
}