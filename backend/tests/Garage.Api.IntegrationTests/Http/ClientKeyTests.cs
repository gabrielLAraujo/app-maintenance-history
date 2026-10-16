using System.Net;
using Garage.Api.Http;
using Microsoft.AspNetCore.Http;

namespace Garage.Api.IntegrationTests.Http;

/// <summary>A chave do rate limit por IP: IPv4 como vem, IPv4 mapeado em IPv6 como IPv4, IPv6 pela rede /64.</summary>
public sealed class ClientKeyTests
{
    [Theory]
    [InlineData(null, "ip:unknown")]
    [InlineData("203.0.113.7", "ip:203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "ip:203.0.113.7")]
    [InlineData("2001:db8:1:2:3:4:5:6", "ip:2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "ip:2001:db8:1:2::/64")]
    public void Client_key_groups_ipv6_by_64(string? address, string expected)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);

        Assert.Equal(expected, RateLimitingSetup.ClientKey(http));
    }
}
