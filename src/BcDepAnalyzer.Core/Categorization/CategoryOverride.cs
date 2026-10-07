using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Categorization;

public sealed record CategoryOverride(int TableId, TableCategory Category, string? Comment = null);
