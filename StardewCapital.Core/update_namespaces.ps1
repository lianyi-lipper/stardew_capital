# Update namespaces in Core library files
$replacements = @{
    'namespace StardewCapital.Core.Calculation' = 'namespace StardewCapital.Core.Futures.Calculation'
    'namespace StardewCapital.Core.Math' = 'namespace StardewCapital.Core.Futures.Math'
    'namespace StardewCapital.Core.Models' = 'namespace StardewCapital.Core.Futures.Models'
    'namespace StardewCapital.Core.Logging' = 'namespace StardewCapital.Core.Common.Logging'
    'namespace StardewCapital.Core.Time' = 'namespace StardewCapital.Core.Common.Time'
    'namespace StardewCapital.Core.Utils' = 'namespace StardewCapital.Core.Common.Utils'
    'namespace StardewCapital.Config' = 'namespace StardewCapital.Core.Futures.Config'
    'namespace StardewCapital.Domain' = 'namespace StardewCapital.Core.Futures.Domain'
    'namespace StardewCapital.Data.SaveData' = 'namespace StardewCapital.Core.Futures.Data'
    'namespace StardewCapital.Services.News' = 'namespace StardewCapital.Core.Futures.Data'
    'using StardewCapital.Core.Math;' = 'using StardewCapital.Core.Futures.Math;'
    'using StardewCapital.Core.Models;' = 'using StardewCapital.Core.Futures.Models;'
    'using StardewCapital.Core.Calculation;' = 'using StardewCapital.Core.Futures.Calculation;'
    'using StardewCapital.Core.Logging;' = 'using StardewCapital.Core.Common.Logging;'
    'using StardewCapital.Core.Time;' = 'using StardewCapital.Core.Common.Time;'
    'using StardewCapital.Core.Utils;' = 'using StardewCapital.Core.Common.Utils;'
    'using StardewCapital.Config;' = 'using StardewCapital.Core.Futures.Config;'
    'using StardewCapital.Domain' = 'using StardewCapital.Core.Futures.Domain'
    'using StardewCapital.Data.SaveData;' = 'using StardewCapital.Core.Futures.Data;'
    'using StardewCapital.Services.News;' = 'using StardewCapital.Core.Futures.Data;'
}

Get-ChildItem -Path "StardewCapital.Core" -Filter "*.cs" -Recurse | ForEach-Object {
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
        Write-Host "Updated: $($_.FullName)"
    }
}

Write-Host "Namespace update complete!"
