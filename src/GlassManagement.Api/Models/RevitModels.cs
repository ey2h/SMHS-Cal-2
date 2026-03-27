namespace GlassManagement.Api.Models;

/// <summary>Revit 模型数据批量提交请求</summary>
public class RevitBatchSubmitRequest
{
    public string ProjectName { get; set; } = "";
    public string RevitVersion { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime ExtractedAt { get; set; }
    public List<RevitPanelData> Panels { get; set; } = new();
    public List<RevitMullionData> Mullions { get; set; } = new();
    public List<RevitConnectionData> Connections { get; set; } = new();
    public List<RevitEmbedData> Embeds { get; set; } = new();
}

/// <summary>面板数据（玻璃/铝板/石材）</summary>
public class RevitPanelData
{
    public string ElementId { get; set; } = "";
    public string PanelType { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; }
    public double Area { get; set; }
    public string? GlassConfig { get; set; }
    public string? Material { get; set; }
    public double LocationX { get; set; }
    public double LocationY { get; set; }
    public double LocationZ { get; set; }
    public string? Level { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>龙骨数据（立柱/横梁）</summary>
public class RevitMullionData
{
    public string ElementId { get; set; } = "";
    public string MullionType { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public double Length { get; set; }
    public double SpanDistance { get; set; }
    public string? Material { get; set; }
    public string? Level { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>连接件数据</summary>
public class RevitConnectionData
{
    public string ElementId { get; set; } = "";
    public string ConnectionType { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string? ConnectedMullionId { get; set; }
    public string? Material { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>预埋件数据</summary>
public class RevitEmbedData
{
    public string ElementId { get; set; } = "";
    public string EmbedType { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string? Material { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>批量提交响应</summary>
public class RevitBatchSubmitResponse
{
    public int SubmissionId { get; set; }
    public int PanelCount { get; set; }
    public int MullionCount { get; set; }
    public int ConnectionCount { get; set; }
    public int EmbedCount { get; set; }
    public int TotalElements { get; set; }
    public string Status { get; set; } = "received";
    public DateTime ReceivedAt { get; set; }
}

/// <summary>Revit 提交记录（数据库存储）</summary>
public class RevitSubmission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ProjectName { get; set; } = "";
    public string RevitVersion { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime ExtractedAt { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "received";
    public int PanelCount { get; set; }
    public int MullionCount { get; set; }
    public int ConnectionCount { get; set; }
    public int EmbedCount { get; set; }
    public string DataJson { get; set; } = "{}";
}
