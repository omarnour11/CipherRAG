using CipherRAG.Domain.Entities;
using CipherRAG.Domain.ValueObjects;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class AccessPolicyTests
{
    [Fact]
    public void Evaluate_AllowsRequestWhenAllConstraintsMatch()
    {
        var policy = new AccessPolicy
        {
            AllowedDepartment = "Cardiology",
            AllowedCountries = ["EG"],
            AllowedIpRanges = ["10.0.0.0/8"],
            TimeWindow = new TimeRange { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) }
        };

        var result = policy.EvaluateWithReason(new RequestContext
        {
            UserDepartment = "cardiology",
            Country = "eg",
            IpAddress = "10.12.4.8",
            RequestTime = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)
        });

        Assert.True(result.IsAllowed);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Evaluate_DeniesRequestWhenDepartmentDoesNotMatch()
    {
        var policy = new AccessPolicy { AllowedDepartment = "Cardiology" };

        var result = policy.EvaluateWithReason(new RequestContext
        {
            UserDepartment = "Radiology",
            IpAddress = "127.0.0.1",
            Country = "EG"
        });

        Assert.False(result.IsAllowed);
        Assert.Contains("Department constraint mismatch", result.FailureReason);
    }

    [Fact]
    public void Evaluate_AllowsWildcardAndLoopbackIp()
    {
        var policy = new AccessPolicy
        {
            AllowedDepartment = "*",
            AllowedCountries = ["*"],
            AllowedIpRanges = ["localhost"]
        };

        var result = policy.EvaluateWithReason(new RequestContext
        {
            UserDepartment = "Any",
            Country = "US",
            IpAddress = "::1"
        });

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void Evaluate_DeniesRequestOutsideTimeWindow()
    {
        var policy = new AccessPolicy
        {
            TimeWindow = new TimeRange { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) }
        };

        var result = policy.EvaluateWithReason(new RequestContext
        {
            RequestTime = new DateTimeOffset(2026, 10, 6, 18, 0, 0, TimeSpan.Zero)
        });

        Assert.False(result.IsAllowed);
        Assert.Contains("outside authorized shift window", result.FailureReason);
    }

    [Fact]
    public void Evaluate_SupportsCrossMidnightTimeWindow()
    {
        var policy = new AccessPolicy
        {
            TimeWindow = new TimeRange { StartTime = TimeSpan.FromHours(22), EndTime = TimeSpan.FromHours(6) }
        };

        Assert.True(policy.Evaluate(new RequestContext
        {
            RequestTime = new DateTimeOffset(2026, 10, 7, 2, 0, 0, TimeSpan.Zero)
        }));

        Assert.False(policy.Evaluate(new RequestContext
        {
            RequestTime = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero)
        }));
    }
}
