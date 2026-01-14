param(
    [Parameter(Mandatory = $true)]
    [string]$Json,
    [string]$TargetHost = "127.0.0.1",
    [int]$Port = 6400
)

$client = New-Object System.Net.Sockets.TcpClient
$client.Connect($TargetHost, $Port)
$stream = $client.GetStream()

# Read handshake line (WELCOME ... \n)
while ($true) {
    $b = $stream.ReadByte()
    if ($b -lt 0 -or $b -eq 10) { break }
}

function Read-Exact([System.IO.Stream]$s, [int]$count) {
    $buf = New-Object byte[] $count
    $offset = 0
    while ($offset -lt $count) {
        $read = $s.Read($buf, $offset, $count - $offset)
        if ($read -le 0) { throw "Connection closed" }
        $offset += $read
    }
    return ,$buf
}

$payload = [System.Text.Encoding]::UTF8.GetBytes($Json)
$len = [UInt64]$payload.Length
$header = New-Object byte[] 8
for ($i = 0; $i -lt 8; $i++) {
    $header[7 - $i] = [byte](($len -shr ($i * 8)) -band 0xFF)
}

$stream.Write($header, 0, 8)
$stream.Write($payload, 0, $payload.Length)
$stream.Flush()

$respHeader = Read-Exact $stream 8
$respLen = [UInt64]0
for ($i = 0; $i -lt 8; $i++) {
    $respLen = ($respLen -shl 8) + $respHeader[$i]
}
$respBytes = Read-Exact $stream ([int]$respLen)
$response = [System.Text.Encoding]::UTF8.GetString($respBytes)

$stream.Close()
$client.Close()

$response
