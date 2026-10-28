$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $taskRoot
$taskOut = 'AutomationOutputs/BattleRl/20261011_dynamic32_batch128_updates100'
$taskLaunch = $taskOut + '_launch'
New-Item -ItemType Directory -Path $taskLaunch -ErrorAction Stop | Out-Null
try {
    # Wait only for the exact known training instance, including its start time.
    $taskPrior = Get-CimInstance Win32_Process -Filter 'ProcessId=295736'
    if ($taskPrior -and $taskPrior.CreationDate.ToString('yyyy-MM-ddTHH:mm:ss') -ne '2026-10-11T19:26:15') {
        throw 'PID was reused; refusing to infer training completion.'
    }
    @{status='waiting_for_static_single_battle_run';previousPid=295736;workers=32;episodesPerUpdate=128;updates=100} |
        ConvertTo-Json | Set-Content -LiteralPath ($taskLaunch+'/queue.json') -Encoding utf8
    while ($taskPrior) {
        Start-Sleep -Seconds 30
        $taskPrior = Get-CimInstance Win32_Process -Filter 'ProcessId=295736'
        if ($taskPrior -and $taskPrior.CreationDate.ToString('yyyy-MM-ddTHH:mm:ss') -ne '2026-10-11T19:26:15') {
            throw 'PID reused while waiting.'
        }
    }
    $taskProgress = Get-Content 'AutomationOutputs/BattleRl/20261011_single32_batch128_updates100/progress.json' -Raw | ConvertFrom-Json
    if ($taskProgress.update -ne 100) { throw 'Previous run exited before update 100; preserve failure, do not automatically restart.' }
    $taskOther = Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^python' -and $_.CommandLine -match 'train_recurrent_ppo.py' }
    if ($taskOther) { throw 'Another recurrent trainer is active; refusing duplicate launch.' }
    $taskArgs = @('-X','utf8','tools/BattleRl/train_recurrent_ppo.py','--dll','tmp/battle_rl_original_world_bound_consumers_v2/BattleRl.dll','--pool','AutomationOutputs/BattleRl/20261011_single_battle_scope/pool.json','--out',$taskOut,'--resume','AutomationOutputs/BattleRl/20261011_single32_batch128_updates100/checkpoint.npz','--updates','100','--episodes-per-update','128','--epochs','4','--native-shared-skills','--native-shapeshift','--native-world','--entity-transformer','--shared-argument-head','--printed-information','--reward-mode','terminal_sequence','--workers','32','--change-collection','--dynamic-single-curriculum')
    $taskProc = Start-Process 'C:/Users/sukefan/AppData/Local/Programs/Python/Python312/python.exe' -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput ($taskLaunch+'/stdout.log') -RedirectStandardError ($taskLaunch+'/stderr.log') -PassThru
    @{status='launched';pid=$taskProc.Id;startTime=$taskProc.StartTime.ToString('o');arguments=$taskArgs;workers=32;episodesPerUpdate=128;updates=100} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($taskLaunch+'/queue.json') -Encoding utf8
} catch {
    @{status='blocked';reason=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath ($taskLaunch+'/queue.json') -Encoding utf8
    throw
}
