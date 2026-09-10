# 定义要读取的文件列表
$files = @(
    "D:\study_and_work\unity-MK-test\MageKnight_from_zero\start_task_conversation.ps1",
    "D:\study_and_work\unity-MK-test\MageKnight_from_zero\run_multiagent.ps1",
    "D:\study_and_work\unity-MK-test\MageKnight_from_zero\tools\orchestrator.py"
)

# 定义输出文件名
$outputFile = "combined_scripts.txt"

# 如果输出文件已存在，先清空
if (Test-Path $outputFile) { Remove-Item $outputFile }

# 遍历文件并写入
foreach ($filePath in $files) {
    if (Test-Path $filePath) {
        # 获取文件名
        $fileName = Split-Path $filePath -Leaf
        
        # 读取内容 (UTF8)
        $content = Get-Content -Path $filePath -Raw -Encoding UTF8
        
        # 构造头部
        $header = "${fileName}："
        
        # 写入到输出文件
        Add-Content -Path $outputFile -Value $header -Encoding UTF8
        Add-Content -Path $outputFile -Value $content -Encoding UTF8
        
        # 加两个换行符作为分隔
        Add-Content -Path $outputFile -Value "`r`n" -Encoding UTF8
        
        Write-Host "✅ 已写入: $fileName" -ForegroundColor Green
    } else {
        Write-Host "❌ 未找到文件: $filePath" -ForegroundColor Red
    }
}

Write-Host "`n🎉 所有内容已合并到: $(Convert-Path $outputFile)" -ForegroundColor Cyan
