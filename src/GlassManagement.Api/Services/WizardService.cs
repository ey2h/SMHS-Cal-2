using System.Collections.Concurrent;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Services;

/// <summary>向导会话管理服务（内存存储）</summary>
public class WizardService
{
    /// <summary>会话字典</summary>
    private readonly ConcurrentDictionary<string, WizardSession> _sessions = new();

    /// <summary>创建新向导会话</summary>
    public WizardSession CreateSession(int userId)
    {
        var session = new WizardSession { UserId = userId };
        _sessions[session.SessionId] = session;
        return session;
    }

    /// <summary>获取向导会话</summary>
    public WizardSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>更新步骤数据</summary>
    public void UpdateStep(string sessionId, int step, string key, object? data)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.StepData[key] = data;
            session.CompletedSteps.Add(step);
            session.CurrentStep = step;
            session.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>完成会话</summary>
    public void CompleteSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Status = "Completed";
            session.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>设置项目ID</summary>
    public void SetProjectId(string sessionId, int projectId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ProjectId = projectId;
        }
    }

    /// <summary>转换为响应DTO</summary>
    public static WizardSessionResponse ToResponse(WizardSession session)
    {
        return new WizardSessionResponse
        {
            SessionId = session.SessionId,
            CurrentStep = session.CurrentStep,
            CompletedSteps = session.CompletedSteps.OrderBy(x => x).ToList(),
            Status = session.Status,
            ProjectId = session.ProjectId,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            StepData = session.StepData
        };
    }
}
