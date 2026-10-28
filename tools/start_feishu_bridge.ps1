param([switch]$Restart, [switch]$InstallAutoStart)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$runtimePath = Join-Path $env:USERPROFILE '.mageknight-feishu'
$bridgeScript = Join-Path $PSScriptRoot 'feishu_bridge.py'
$serviceScript = Join-Path $PSScriptRoot 'feishu_service.py'
$pythonPath = Join-Path $runtimePath 'venv/Scripts/pythonw.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) { throw 'Stable bridge runtime is missing; run the local migration first.' }
if ($InstallAutoStart) {
    $startup = [Environment]::GetFolderPath('Startup')
    $shortcutPath = Join-Path $startup 'MageKnight Feishu Bridge.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $pythonPath
    $shortcut.Arguments = '"' + $serviceScript + '"'
    $shortcut.WorkingDirectory = $projectPath
    $shortcut.WindowStyle = 7
    $shortcut.Description = 'MageKnight Feishu bridge: restore after logon and restart on exit.'
    $shortcut.Save()
}
if ($Restart) {
    foreach ($script in @($serviceScript, $bridgeScript)) {
        Get-CimInstance Win32_Process | Where-Object {
            $_.Name -eq 'pythonw.exe' -and $_.CommandLine -and $_.CommandLine.Contains($script)
        } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
    }
}
Start-Process -FilePath $pythonPath -ArgumentList ('"' + $serviceScript + '"') -WorkingDirectory $projectPath -WindowStyle Hidden
Write-Output "Bridge supervisor started; status: $runtimePath/status.json"
