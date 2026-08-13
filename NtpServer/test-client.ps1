param(
    [string]$Server = '127.0.0.1',
    [int]$Port = 123,
    [int]$Count = 5,
    [int]$TimeoutMilliseconds = 2000
)

$ErrorActionPreference = 'Stop'
$ntpEpoch = [DateTimeOffset]::FromUnixTimeSeconds(-2208988800)
$client = [Net.Sockets.UdpClient]::new()
try {
    $client.Client.ReceiveTimeout = $TimeoutMilliseconds
    $client.Connect($Server, $Port)
    for ($i = 1; $i -le $Count; $i++) {
        $packet = [byte[]]::new(48)
        $packet[0] = 0x23 # NTPv4, client mode
        $started = [DateTimeOffset]::UtcNow
        [void]$client.Send($packet, $packet.Length)
        $remote = [Net.IPEndPoint]::new([Net.IPAddress]::Any, 0)
        $response = $client.Receive([ref]$remote)
        $finished = [DateTimeOffset]::UtcNow
        if ($response.Length -ne 48 -or (($response[0] -band 7) -ne 4)) {
            throw "잘못된 NTP 응답입니다: length=$($response.Length), mode=$($response[0] -band 7)"
        }
        $secondsBytes = $response[40..43]
        [Array]::Reverse($secondsBytes)
        $seconds = [BitConverter]::ToUInt32($secondsBytes, 0)
        $fractionBytes = $response[44..47]
        [Array]::Reverse($fractionBytes)
        $fraction = [BitConverter]::ToUInt32($fractionBytes, 0)
        $candidate = $ntpEpoch.AddSeconds($seconds).AddTicks([long](($fraction / [math]::Pow(2, 32)) * 10000000))
        $now = [DateTimeOffset]::UtcNow
        $eraSeconds = [math]::Pow(2, 32)
        $era = [math]::Round(($now - $candidate).TotalSeconds / $eraSeconds)
        $serverTime = $candidate.AddSeconds($era * $eraSeconds)
        $midpoint = $started + [TimeSpan]::FromTicks(($finished - $started).Ticks / 2)
        $offsetMs = ($serverTime - $midpoint).TotalMilliseconds
        Write-Host ("{0}/{1} server={2:O} rtt_ms={3:N3} offset_ms={4:N3}" -f $i, $Count, $serverTime, ($finished-$started).TotalMilliseconds, $offsetMs)
    }
}
finally {
    $client.Dispose()
}
