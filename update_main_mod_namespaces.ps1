# 批量更新主mod项目的命名空间引用
$replacements = @{
    'using StardewCapital.Core.Calculation;'          = 'using StardewCapital.Core.Futures.Calculation;'
    'using StardewCapital.Core.Models;'               = 'using StardewCapital.Core.Futures.Models;'
    'using StardewCapital.Core.Math;'                 = 'using StardewCapital.Core.Futures.Math;'
    'using StardewCapital.Core.Logging;'              = 'using StardewCapital.Core.Common.Logging;'
    'using StardewCapital.Core.Time;'                 = 'using StardewCapital.Core.Common.Time;'
    'using StardewCapital.Core.Utils;'                = 'using StardewCapital.Core.Common.Utils;'
    'using StardewCapital.Config;'                    = 'using StardewCapital.Core.Futures.Config;'
    'using StardewCapital.Domain.Market.MarketState;' = 'using StardewCapital.Core.Futures.Domain.Market.MarketState;'
    'using StardewCapital.Domain.Market;'             = 'using StardewCapital.Core.Futures.Domain.Market;'
    'using StardewCapital.Domain.Instruments;'        = 'using StardewCapital.Core.Futures.Domain.Instruments;'
    'using StardewCapital.Domain.Account;'            = 'using StardewCapital.Core.Futures.Domain.Account;'
    'using StardewCapital.Domain;'                    = 'using StardewCapital.Core.Futures.Domain;'
    'using StardewCapital.Data.SaveData;'             = 'using StardewCapital.Core.Futures.Data;'
    'using StardewCapital.Services.News;'             = 'using StardewCapital.Core.Futures.Data;'
    'using StardewCapital.Services.Market;'           = 'using StardewCapital.Core.Futures.Services;'
}

$count = 0
$fileCount = 0

Write-Host "开始更新主mod项目的命名空间..."
Write-Host ""

Get-ChildItem -Path "Src" -Filter "*.cs" -Recurse -Exclude "_Archived" | ForEach-Object {
    $filePath = $_.FullName
    $content = Get-Content $filePath -Raw -Encoding UTF8
    $modified = $false
    $changesMade = @()
    
    foreach ($key in $replacements.Keys) {
        $newValue = $replacements[$key]
        if ($content -match [regex]::Escape($key)) {
            $content = $content -replace [regex]::Escape($key), $newValue
            $modified = $true
            $changesMade += "$key -> $newValue"
            $count++
        }
    }
    
    if ($modified) {
        Set-Content $filePath -Value $content -Encoding UTF8
        $relativePath = $filePath.Replace((Get-Location).Path + "\", "")
        Write-Host "✓ $relativePath"
        foreach ($change in $changesMade) {
            Write-Host "    - $change"
        }
        $fileCount++
    }
}

Write-Host ""
Write-Host "========================================="
Write-Host "✅ 更新完成！"
Write-Host "   修改了 $fileCount 个文件"
Write-Host "   总共 $count 处命名空间引用"
Write-Host "========================================="
