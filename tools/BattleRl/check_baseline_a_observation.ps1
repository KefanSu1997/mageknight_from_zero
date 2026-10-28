param(
    [string]$BinaryDirectory = 'H:/MageKnight_RL_Runs/20261022_A_baseline_spec/observation_gate_bin',
    [string]$Setups = 'H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/family_export_gate_setups.json',
    [string]$Output = 'H:/MageKnight_RL_Runs/20261022_A_baseline_spec/native_observation_solo_gate.json'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
foreach ($name in @('NATIVE_PAYMENTS','NATIVE_CONVERSIONS','NATIVE_ENERGY_TARGETS',
    'SHARED_SKILLS','SHAPESHIFT','ORIGINAL_WORLD','ORIGINAL_OPENINGS')) {
    [Environment]::SetEnvironmentVariable('BATTLE_RL_'+$name,'1')
}
[Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'Newtonsoft.Json.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'BattleRl.dll')) | Out-Null
$adapter = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'SoloConquestRl.dll'))
[MK.Logic.Data.CardJsonLoader]::SetBaseDirectory((Join-Path $root 'resources/text_json'))
$ruins = $adapter.GetType('OriginalRuinCatalog').GetMethod('LoadOptional').Invoke($null,@($root))
$paths = @('tutorial_opening.json','countryside_3_11_transcription.json','core_1_8_transcription.json') |
    ForEach-Object { Join-Path $root ('resources/original_maps/'+$_) }
$catalog = [MK.Logic.Runtime.Map.PrintedTileCatalog]::Load([string[]]$paths)
$reference = [BaselineAObservation]::SevenHeroEntryGate()
$families = Get-Content $Setups -Raw | ConvertFrom-Json
$rows = @()
$worldRows = @()
$featureRows = @()
foreach ($task in $families[0].tasks) {
    $setup = [System.Text.Json.JsonSerializer]::Deserialize(
        ($task.setup | ConvertTo-Json -Depth 20),[MK.Logic.Runtime.Battles.SoloEpisodeSetup])
    $episode = [MK.Logic.Runtime.Battles.SoloEpisodeFactory]::Create($setup,$catalog,$ruins)
    for ($i=0; $i -lt 4; $i++) {
        $before = [MK.Logic.Runtime.Battles.SoloStateDigest]::Compute($episode)
        $world = $episode.Observe().World
        $worldRows += [System.Text.Json.JsonSerializer]::Serialize($world,$world.GetType())
        $counters = [BaselineAObservation+Counters]::new($episode.Steps,
            $episode.Journey.CompletedTurns+1,$world.Round,0,$false)
        $features = [BaselineAObservation]::Capture($episode.Journey,$counters)
        $featureRows += ,@($features)
        $after = [MK.Logic.Runtime.Battles.SoloStateDigest]::Compute($episode)
        if ($features.Length -ne 6314 -or $before -ne $after) { throw 'Projection mutated state or changed width' }
        $rows += @{hero=$setup.Hero; step=$episode.Steps; width=$features.Length; state_unchanged=$true}
        if ($i -lt 3) {
            $support = [MK.Logic.Runtime.Battles.SoloDecisionSupport]::Capture($episode)
            $null = $support.ExecuteRecorded($episode,0,$null,$null)
        }
    }
}
@{passed=$true; reference_battles=$reference; solo_states=$rows;
    scope='Read-only seven-hero entry projection only; world extension, pending selections, full policy and PPO remain unverified'} |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Output
Get-Content -LiteralPath $Output
('['+($worldRows -join ',')+']') | Set-Content -LiteralPath ($Output+'.worlds.json')
ConvertTo-Json -InputObject $featureRows -Depth 5 -Compress | Set-Content -LiteralPath ($Output+'.features.json')
