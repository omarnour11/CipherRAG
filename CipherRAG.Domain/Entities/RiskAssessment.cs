using CipherRAG.Domain.Enums;

namespace CipherRAG.Domain.Entities;

public class RiskAssessment
{
    public Guid AssessmentId { get; set; }
    public decimal PatternScore { get; set; }
    public decimal MlModelScore { get; set; }
    public decimal LlmJudgeScore { get; set; }
    public RiskVerdict Verdict { get; set; }
    public string QueryHash{get;set;} = String.Empty;
    public decimal CompositeRiskScore{get;set;}
    public decimal ComputeComposite()
    {
        return (PatternScore + MlModelScore + LlmJudgeScore) / 3;
    }

    public bool IsHighRisk(decimal threshold)
    {
        return ComputeComposite() >= threshold;
    }
}