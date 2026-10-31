using CipherRAG.Domain.Entities;
using CipherRAG.Domain.ValueObjects;
using Xunit;

namespace CipherRAG.Domain.Tests;

public class AccessPolicyDeepSecurityTests
{
    [Theory]
    // Department Exact & Case Variations
    [InlineData("Cardiology", "Cardiology", true)]
    [InlineData("Cardiology", "cardiology", true)]
    [InlineData("Cardiology", "CARDIOLOGY", true)]
    [InlineData("Oncology", "Oncology", true)]
    [InlineData("Oncology", "oncology", true)]
    [InlineData("Pediatrics", "Pediatrics", true)]
    [InlineData("Neurology", "neurology", true)]
    [InlineData("Radiology", "Radiology", true)]
    [InlineData("Emergency", "emergency", true)]
    [InlineData("Surgery", "SURGERY", true)]
    [InlineData("Pathology", "Pathology", true)]
    [InlineData("IntensiveCare", "intensivecare", true)]
    [InlineData("Pharmacy", "pharmacy", true)]
    [InlineData("Administration", "administration", true)]
    [InlineData("IT", "it", true)]
    // Department Mismatches (Denials)
    [InlineData("Cardiology", "Radiology", false)]
    [InlineData("Cardiology", "Oncology", false)]
    [InlineData("Oncology", "Pediatrics", false)]
    [InlineData("Surgery", "Administration", false)]
    [InlineData("Pharmacy", "IT", false)]
    [InlineData("Emergency", "Pathology", false)]
    [InlineData("Neurology", "Billing", false)]
    [InlineData("IntensiveCare", "Guest", false)]
    // Wildcard & Open Departments
    [InlineData("*", "Cardiology", true)]
    [InlineData("*", "Radiology", true)]
    [InlineData("*", "AnyDepartment", true)]
    [InlineData("", "Cardiology", true)]
    [InlineData(null, "Oncology", true)]
    public void DepartmentConstraint_EvaluatesCorrectly(string? policyDept, string userDept, bool shouldAllow)
    {
        var policy = new AccessPolicy
        {
            AllowedDepartment = policyDept ?? string.Empty
        };

        var ctx = new RequestContext
        {
            UserDepartment = userDept,
            IpAddress = "127.0.0.1",
            Country = "EG"
        };

        var result = policy.EvaluateWithReason(ctx);
        Assert.Equal(shouldAllow, result.IsAllowed);
        if (!shouldAllow)
        {
            Assert.Contains("Department constraint mismatch", result.FailureReason);
        }
    }

    [Theory]
    // Authorized Countries (Case Insensitive)
    [InlineData("EG", true)]
    [InlineData("eg", true)]
    [InlineData("US", true)]
    [InlineData("us", true)]
    [InlineData("SA", true)]
    [InlineData("sa", true)]
    [InlineData("AE", true)]
    [InlineData("UK", true)]
    [InlineData("DE", true)]
    [InlineData("FR", true)]
    // Unauthorized Countries (Denials)
    [InlineData("RU", false)]
    [InlineData("CN", false)]
    [InlineData("KP", false)]
    [InlineData("IR", false)]
    [InlineData("BR", false)]
    [InlineData("IN", false)]
    [InlineData("AU", false)]
    [InlineData("JP", false)]
    [InlineData("Unknown", false)]
    [InlineData("XX", false)]
    public void CountryConstraint_RestrictsToAuthorizedGeographies(string incomingCountry, bool shouldAllow)
    {
        var policy = new AccessPolicy
        {
            AllowedCountries = ["EG", "US", "SA", "AE", "UK", "DE", "FR"]
        };

        var ctx = new RequestContext
        {
            UserDepartment = "Any",
            IpAddress = "127.0.0.1",
            Country = incomingCountry
        };

        var result = policy.EvaluateWithReason(ctx);
        Assert.Equal(shouldAllow, result.IsAllowed);
        if (!shouldAllow)
        {
            Assert.Contains("not in authorized countries", result.FailureReason);
        }
    }

    [Theory]
    [InlineData("*", "RU", true)]
    [InlineData("*", "CN", true)]
    [InlineData("*", "EG", true)]
    public void CountryConstraint_WildcardAllowsAllGeographies(string wildcard, string incomingCountry, bool shouldAllow)
    {
        var policy = new AccessPolicy { AllowedCountries = [wildcard] };
        var ctx = new RequestContext { Country = incomingCountry };
        Assert.Equal(shouldAllow, policy.Evaluate(ctx));
    }

    [Theory]
    // Loopback & Localhost Normalization
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    // Subnet CIDR 10.0.0.0/8
    [InlineData("10.0.0.1", true)]
    [InlineData("10.12.34.56", true)]
    [InlineData("10.254.254.254", true)]
    [InlineData("11.0.0.1", false)]
    // Subnet CIDR 192.168.1.0/24
    [InlineData("192.168.1.1", true)]
    [InlineData("192.168.1.150", true)]
    [InlineData("192.168.1.254", true)]
    [InlineData("192.168.2.1", false)]
    [InlineData("192.168.0.254", false)]
    // Subnet CIDR 172.16.0.0/16
    [InlineData("172.16.0.1", true)]
    [InlineData("172.16.100.200", true)]
    [InlineData("172.17.0.1", false)]
    // Blocked public IPs
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("185.220.101.5", false)]
    [InlineData("45.33.32.156", false)]
    [InlineData("10.100.50.25", true)]
    [InlineData("10.0.0.0", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("192.168.1.200", true)]
    [InlineData("172.16.255.255", true)]
    [InlineData("172.16.1.50", true)]
    [InlineData("172.18.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("9.255.255.255", false)]
    [InlineData("192.168.3.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("203.0.113.195", false)]
    [InlineData("198.51.100.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("224.0.0.1", false)]
    public void IpAndCidrConstraint_EvaluatesCorrectly(string incomingIp, bool shouldAllow)
    {
        var policy = new AccessPolicy
        {
            AllowedIpRanges = ["localhost", "10.0.0.0/8", "192.168.1.0/24", "172.16.0.0/16"]
        };

        var ctx = new RequestContext
        {
            UserDepartment = "Any",
            Country = "EG",
            IpAddress = incomingIp
        };

        var result = policy.EvaluateWithReason(ctx);
        Assert.Equal(shouldAllow, result.IsAllowed);
        if (!shouldAllow)
        {
            Assert.Contains("not within authorized IP ranges", result.FailureReason);
        }
    }

    [Theory]
    // Standard Day Shift (08:00 - 17:00 UTC)
    [InlineData(8, 0, 0, true)]    // Start exact
    [InlineData(8, 0, 1, true)]    // Just after start
    [InlineData(10, 30, 0, true)]  // Mid-morning
    [InlineData(12, 0, 0, true)]   // Noon
    [InlineData(14, 15, 0, true)]  // Afternoon
    [InlineData(16, 59, 59, true)] // Just before end
    [InlineData(17, 0, 0, true)]   // End exact
    [InlineData(7, 59, 59, false)] // Just before shift starts
    [InlineData(6, 0, 0, false)]   // Early morning
    [InlineData(17, 0, 1, false)]  // Just after shift ends
    [InlineData(18, 30, 0, false)] // Evening
    [InlineData(23, 0, 0, false)]  // Night
    [InlineData(3, 0, 0, false)]   // Dawn
    public void TimeWindow_StandardDayShift_EnforcesHours(int h, int m, int s, bool shouldAllow)
    {
        var policy = new AccessPolicy
        {
            TimeWindow = new TimeRange
            {
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(17, 0, 0)
            }
        };

        var ctx = new RequestContext
        {
            RequestTime = new DateTimeOffset(2026, 10, 15, h, m, s, TimeSpan.Zero)
        };

        Assert.Equal(shouldAllow, policy.Evaluate(ctx));
    }

    [Theory]
    // Cross-Midnight Night Shift (22:00 - 06:00 UTC)
    [InlineData(22, 0, 0, true)]    // Shift start exact
    [InlineData(23, 0, 0, true)]    // Pre-midnight
    [InlineData(23, 59, 59, true)]  // 1s before midnight
    [InlineData(0, 0, 0, true)]     // Midnight exact
    [InlineData(1, 30, 0, true)]    // Post-midnight
    [InlineData(3, 45, 0, true)]    // Deep night
    [InlineData(5, 59, 59, true)]   // 1s before end
    [InlineData(6, 0, 0, true)]     // Shift end exact
    [InlineData(6, 0, 1, false)]    // 1s after shift ends
    [InlineData(7, 0, 0, false)]    // Morning outside shift
    [InlineData(12, 0, 0, false)]   // Afternoon outside shift
    [InlineData(16, 0, 0, false)]   // Evening outside shift
    [InlineData(21, 59, 59, false)] // 1s before shift starts
    public void TimeWindow_CrossMidnightShift_EnforcesHours(int h, int m, int s, bool shouldAllow)
    {
        var policy = new AccessPolicy
        {
            TimeWindow = new TimeRange
            {
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(6, 0, 0)
            }
        };

        var ctx = new RequestContext
        {
            RequestTime = new DateTimeOffset(2026, 10, 15, h, m, s, TimeSpan.Zero)
        };

        Assert.Equal(shouldAllow, policy.Evaluate(ctx));
    }

    [Theory]
    // Complex Multi-Attribute ABAC Evaluation Scenarios
    // Scenario 1: All attributes valid
    [InlineData("Cardiology", "EG", "10.1.2.3", 10, 0, true)]
    // Scenario 2: Department mismatch
    [InlineData("Radiology", "EG", "10.1.2.3", 10, 0, false)]
    // Scenario 3: Country mismatch
    [InlineData("Cardiology", "RU", "10.1.2.3", 10, 0, false)]
    // Scenario 4: IP mismatch
    [InlineData("Cardiology", "EG", "8.8.8.8", 10, 0, false)]
    // Scenario 5: Outside authorized shift hours
    [InlineData("Cardiology", "EG", "10.1.2.3", 21, 0, false)]
    // Scenario 6: Valid Loopback IP with valid department
    [InlineData("Cardiology", "EG", "127.0.0.1", 12, 0, true)]
    // Scenario 7: Valid Loopback IP with invalid country
    [InlineData("Cardiology", "CN", "127.0.0.1", 12, 0, false)]
    // Scenario 8: Valid second country US with valid IP
    [InlineData("Cardiology", "US", "192.168.1.55", 14, 0, true)]
    public void AbacMatrix_CombinesAllDimensionsStrictly(
        string department, string country, string ip, int hour, int minute, bool expectedAllowed)
    {
        var policy = new AccessPolicy
        {
            AllowedDepartment = "Cardiology",
            AllowedCountries = ["EG", "US", "SA"],
            AllowedIpRanges = ["localhost", "10.0.0.0/8", "192.168.1.0/24"],
            TimeWindow = new TimeRange
            {
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(17, 0, 0)
            }
        };

        var ctx = new RequestContext
        {
            UserDepartment = department,
            Country = country,
            IpAddress = ip,
            RequestTime = new DateTimeOffset(2026, 10, 15, hour, minute, 0, TimeSpan.Zero)
        };

        var result = policy.EvaluateWithReason(ctx);
        Assert.Equal(expectedAllowed, result.IsAllowed);
    }
}
