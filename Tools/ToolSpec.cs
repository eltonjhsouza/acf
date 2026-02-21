namespace AgentCore.Tools;

public sealed class ToolSpec
{
  /// <summary>
    /// Especificação de uma ferramenta (tool) que pode ser usada por um agente. 
    /// Contém o nome, descrição e o schema de input da ferramenta.
    /// O schema de input é uma string que descreve o formato dos dados que a ferramenta espera receber.
    /// O agente deve seguir rigorosamente o contrato da ferramenta, ou seja, fornecer os dados
    /// no formato esperado pelo schema.
    /// Exemplo de uso:
    /// var toolSpec = new ToolSpec
    /// {
    ///   Name = "FileSystemTool",
    ///  Description = "Ferramenta para manipulação de arquivos no sistema de arquivos local.",
    ///  JsonSchema = "{ 'type': 'object', 'properties': { 'action': { 'type': 'string' }, 'path': { 'type': 'string' }, 'content': { 'type': 'string' } }, 'required': ['action', 'path'] }"
    /// };
    /// O agente pode então usar essa ferramenta para criar, ler, atualizar ou deletar arquivos no sistema de arquivos local, seguindo o contrato definido pelo schema.
    /// </summary>
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string JsonSchema { get; init; }
}