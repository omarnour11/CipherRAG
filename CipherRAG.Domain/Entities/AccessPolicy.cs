using CipherRAG.Domain.ValueObjects;

namespace CipherRAG.Domain.Entities;



public class AccessPolicy 
{
    public Guid PolicyId{get;set;}

    public Guid RoleId{get;set;}
    public string AllowedDepartment{get;set;} = string.Empty;
    public List<String> AllowedIpRanges{get;set;} =[];
    public List<String> AllowedCountries{get;set;} =[];
    public TimeRange? TimeWindow{get;set;} 

    public bool IsActive {get;set;}

    //nav
    public Role? Role {get;set;}
    /// <summary>
    /// Evaluates attribute-based access control (ABAC) rules for an incoming request context.
    /// Returns a tuple containing whether access is granted and the specific reason if denied.
    /// </summary>
    public (bool IsAllowed, string? FailureReason) EvaluateWithReason(RequestContext ctx)
    {
        if (!string.IsNullOrEmpty(AllowedDepartment) &&
            !string.Equals(AllowedDepartment, "*", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(AllowedDepartment, ctx.UserDepartment, StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Department constraint mismatch (Authorized: '{AllowedDepartment}', Request: '{ctx.UserDepartment}')");
        }

        if (AllowedCountries.Count > 0 &&
            !AllowedCountries.Contains("*") &&
            !AllowedCountries.Contains(ctx.Country, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Country '{ctx.Country}' is not in authorized countries [{string.Join(", ", AllowedCountries)}]");
        }

        if (AllowedIpRanges.Count > 0 && !AllowedIpRanges.Contains("*"))
        {
            bool ipMatched = AllowedIpRanges.Contains(ctx.IpAddress, StringComparer.OrdinalIgnoreCase);

            if (!ipMatched && System.Net.IPAddress.TryParse(ctx.IpAddress, out var incomingIp))
            {
                // Loopback normalization: map IPv6 ::1, IPv4 127.0.0.1, and localhost
                if (System.Net.IPAddress.IsLoopback(incomingIp) &&
                    AllowedIpRanges.Any(ip => ip == "127.0.0.1" || ip == "::1" || ip.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                {
                    ipMatched = true;
                }
                else
                {
                    // Fallback to checking CIDR blocks
                    var cidrRanges = AllowedIpRanges.Where(ip => ip.Contains('/'));
                    foreach (var cidr in cidrRanges)
                    {
                        try
                        {
                            var network = System.Net.IPNetwork.Parse(cidr);
                            if (network.BaseAddress.AddressFamily == incomingIp.AddressFamily && network.Contains(incomingIp))
                            {
                                ipMatched = true;
                                break;
                            }
                        }
                        catch
                        {
                            // Ignore parsing errors for invalid CIDR blocks
                        }
                    }
                }
            }

            if (!ipMatched)
            {
                return (false, $"Incoming IP '{ctx.IpAddress}' is not within authorized IP ranges [{string.Join(", ", AllowedIpRanges)}]");
            }
        }

        if (TimeWindow is not null && !TimeWindow.IsWithinTimeWindow(ctx.RequestTime))
        {
            return (false, $"Request time {ctx.RequestTime:HH:mm} UTC falls outside authorized shift window [{TimeWindow.StartTime:hh\\:mm} - {TimeWindow.EndTime:hh\\:mm} UTC]");
        }

        return (true, null);
    }

    /// <summary>
    /// Evaluates attribute-based access control (ABAC) rules for an incoming request context.
    /// </summary>
    public bool Evaluate(RequestContext ctx) => EvaluateWithReason(ctx).IsAllowed;
}
