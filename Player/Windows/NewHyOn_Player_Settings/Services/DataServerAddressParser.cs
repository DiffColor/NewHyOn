using System;
using System.Net;

namespace NewHyOn.Player.Settings.Services;

internal static class DataServerAddressParser
{
    public const int DefaultRethinkPort = 28015;

    public static bool TryParse(string? rawValue, out DataServerAddressEndpoint endpoint)
    {
        endpoint = default!;

        string input = Normalize(rawValue);
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (TryParseAbsoluteUri(input, out endpoint))
        {
            return true;
        }

        if (IPAddress.TryParse(input, out _))
        {
            endpoint = new DataServerAddressEndpoint(input, DefaultRethinkPort, input);
            return true;
        }

        if (Uri.CheckHostName(input) != UriHostNameType.Unknown)
        {
            endpoint = new DataServerAddressEndpoint(input, DefaultRethinkPort, input);
            return true;
        }

        if (TryParseHostAndPort(input, out endpoint))
        {
            return true;
        }

        return false;
    }

    public static string NormalizeForStorage(string? rawValue)
    {
        return TryParse(rawValue, out DataServerAddressEndpoint endpoint)
            ? endpoint.DisplayAddress
            : Normalize(rawValue);
    }

    private static bool TryParseAbsoluteUri(string input, out DataServerAddressEndpoint endpoint)
    {
        endpoint = default!;
        if (!input.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        if (!IsValidHost(uri.Host))
        {
            return false;
        }

        int port = uri.IsDefaultPort ? DefaultRethinkPort : uri.Port;
        if (!IsValidPort(port))
        {
            return false;
        }

        endpoint = new DataServerAddressEndpoint(
            uri.Host.Trim(),
            port,
            BuildDisplayAddress(uri.Host.Trim(), port));
        return true;
    }

    private static bool TryParseHostAndPort(string input, out DataServerAddressEndpoint endpoint)
    {
        endpoint = default!;
        if (!Uri.TryCreate($"tcp://{input}", UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        if (!IsValidHost(uri.Host))
        {
            return false;
        }

        int port = uri.IsDefaultPort ? DefaultRethinkPort : uri.Port;
        if (!IsValidPort(port))
        {
            return false;
        }

        endpoint = new DataServerAddressEndpoint(
            uri.Host.Trim(),
            port,
            BuildDisplayAddress(uri.Host.Trim(), port));
        return true;
    }

    private static bool IsValidHost(string value)
    {
        return IPAddress.TryParse(value, out _) || Uri.CheckHostName(value) != UriHostNameType.Unknown;
    }

    private static bool IsValidPort(int port)
    {
        return port > 0 && port <= 65535;
    }

    private static string BuildDisplayAddress(string host, int port)
    {
        return port == DefaultRethinkPort
            ? host
            : $"{host}:{port}";
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

internal sealed class DataServerAddressEndpoint
{
    public DataServerAddressEndpoint(string host, int port, string displayAddress)
    {
        Host = host;
        Port = port;
        DisplayAddress = displayAddress;
    }

    public string Host { get; }

    public int Port { get; }

    public string DisplayAddress { get; }
}
