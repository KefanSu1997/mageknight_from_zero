param([Parameter(Mandatory=$true)][string]$Dll,
      [string]$Inventory='AutomationConfigs/rl_full_coverage_inventory.json',
      [Parameter(Mandatory=$true)][string]$Out)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Out){throw 'Preserve existing frozen metadata evidence'}
$taskDllPath=(Resolve-Path -LiteralPath $Dll).Path
$taskAssembly=[System.Reflection.Assembly]::LoadFrom($taskDllPath)
$taskChoiceMethod=$taskAssembly.GetType('MK.Logic.Runtime.Battles.BattleCardChoice').GetMethod('For')
$taskInventory=Get-Content -LiteralPath $Inventory -Raw | ConvertFrom-Json
$taskRows=@(foreach($card in $taskInventory.cards){
    if($card.category -notin @('basic_card','advanced_card','magic')){continue}
    $taskChoices=@($taskChoiceMethod.Invoke($null,[object[]]@([string]$card.id)))
    [ordered]@{id=$card.id;choices=@(foreach($choice in $taskChoices){
        [ordered]@{choice=$choice.Id;mode=$(if($choice.Enhanced){'strong'}else{'base'});
                   enhanced=[bool]$choice.Enhanced;option=[int]$choice.Option}
    })}
})
$taskResult=[ordered]@{schema_version=1;rules_sha256=(Get-FileHash -LiteralPath $taskDllPath -Algorithm SHA256).Hash.ToLowerInvariant();
    inventory_sha256=(Get-FileHash -LiteralPath $Inventory -Algorithm SHA256).Hash.ToLowerInvariant();
    source='Frozen BattleCardChoice.For Enhanced flag; metadata only, not rule acceptance';cards=$taskRows}
$taskOutPath=[System.IO.Path]::GetFullPath($Out)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($taskOutPath)) | Out-Null
[System.IO.File]::WriteAllText($taskOutPath,($taskResult | ConvertTo-Json -Depth 12),[System.Text.UTF8Encoding]::new($false))
[ordered]@{cards=$taskRows.Count;out=$taskOutPath} | ConvertTo-Json -Compress
