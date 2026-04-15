param(
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [Parameter(Mandatory = $true)]
    [string] $VdprojPath
)

$full = [System.IO.Path]::GetFullPath($VdprojPath)
if (-not (Test-Path -LiteralPath $full)) {
    Write-Error "vdproj not found: $full"
    exit 1
}

function New-VdprojGuid {
    param()
    return '{' + ([guid]::NewGuid().ToString().ToUpperInvariant()) + '}'
}

$content = [System.IO.File]::ReadAllText($full)

$pvPattern = '"ProductVersion"\s*=\s*"8:[^"]*"'
$pvReplacement = '"ProductVersion" = "8:' + $Version + '"'
$m = [regex]::Match($content, $pvPattern)
if (-not $m.Success) {
    Write-Error "Could not find ProductVersion line in: $full"
    exit 1
}
if ($m.Value -eq $pvReplacement) {
    Write-Host "vdproj ProductVersion already matches Version ($Version). ProductCode unchanged."
    exit 0
}

$newContent = [regex]::new($pvPattern).Replace($content, $pvReplacement, 1)

# MSI product line only (exclude prerequisite rows like "8:.NETFramework,Version=v4.8").
$pcPattern = '"ProductCode"\s*=\s*"8:\{[0-9A-Fa-f-]+\}"'
$pcMatch = [regex]::Match($newContent, $pcPattern)
if (-not $pcMatch.Success) {
    Write-Error "Could not find MSI ProductCode (GUID form) in: $full"
    exit 1
}
$newProductCode = New-VdprojGuid
$newContent = [regex]::Replace($newContent, $pcPattern, '"ProductCode" = "8:' + $newProductCode + '"', 1)

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($full, $newContent, $utf8NoBom)
Write-Host "Updated ProductVersion to $Version in $(Split-Path $full -Leaf)"
Write-Host "Updated ProductCode to $newProductCode"
