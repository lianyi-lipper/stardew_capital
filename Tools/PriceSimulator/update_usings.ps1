# Update SimulatorConsole namespaces to match new Core library structure
$replacements = @{
    'using StardewCapital.Core.Logging;'              = 'using StardewCapital.Core.Common.Logging;'
    'using StardewCapital.Core.Calculation;'          = 'using StardewCapital.Core.Futures.Calculation;'
    'using StardewCapital.Core.Models;'               = 'using StardewCapital.Core.Futures.Models;'
    'using StardewCapital.Core.Utils;'                = 'using StardewCapital.Core.Common.Utils;'
    'using StardewCapital.Core.Time;'                 = 'using StardewCapital.Core.Common.Time;'
    'using StardewCapital.Config;'                    = 'using StardewCapital.Core.Futures.Config;'
    'using StardewCapital.Domain.Market;'             = 'using StardewCapital.Core.Futures.Domain.Market;'
    'using StardewCapital.Domain.Market.MarketState;' = 'using StardewCapital.Core.Futures.Domain.Market.MarketState;'
    'using StardewCapital.Domain.Instruments;'        = 'using StardewCapital.Core.Futures.Domain.Instruments;'
    'using StardewCapital.Domain;'                    = 'using StardewCapital.Core.Futures.Domain;'
    'using StardewCapital.Data.SaveData;'             = 'using StardewCapital.Core.Futures.Data;'
    'using StardewCapital.Services.News;'             = 'using StardewCapital.Core.Futures.Data;'
}

$count = 0
Get-ChildItem -Path "Tools\PriceSimulator" -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw -Encoding UTF8
    $modified = $false
    
    foreach ($key in $replacements.Keys) {
        $newValue = $replacements[$key]
        if ($content -match [regex]::Escape($key)) {
            $content = $content -replace [regex]::Escape($key), $newValue
            $modified = $true
        }
    }
    
    if ($modified) {
        Set-Content $_.FullName -Value $content -Encoding UTF8
        Write-Host "Updated: $($_.Name)"
        $count++
    }
}

Write-Host "Updated $count files in SimulatorConsole project"
