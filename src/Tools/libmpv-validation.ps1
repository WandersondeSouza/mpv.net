<#

Shared validation functions for the two libmpv distribution files. They are
used only by build, package and test workflows; the player does not parse PE
files or calculate hashes during normal startup.

#>

function Get-LibMpvBuildContract([string] $ContractFile) {
    if (-not $ContractFile) {
        $ContractFile = Join-Path $PSScriptRoot 'libmpv-build-contract.psd1'
    }

    if (-not (Test-Path -LiteralPath $ContractFile -PathType Leaf)) {
        throw "libmpv build contract was not found: $ContractFile"
    }

    return Import-PowerShellDataFile -LiteralPath $ContractFile
}

function Test-RequiredNativeFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required native dependency not found: $Path"
    }

    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0) {
        throw "Required native dependency is empty: $Path"
    }

    return $file
}

function Assert-PeX64([string] $Path) {
    $file = Test-RequiredNativeFile $Path
    $stream = [System.IO.File]::OpenRead($file.FullName)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -gt ($stream.Length - 6)) {
            throw "Invalid PE header in $($file.FullName)"
        }

        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "Invalid PE signature in $($file.FullName)"
        }

        $machine = $reader.ReadUInt16()
        if ($machine -ne 0x8664) {
            throw "Expected x64 native binary, got machine 0x$($machine.ToString('X4')): $($file.FullName)"
        }
    }
    finally {
        $stream.Dispose()
    }

    return $file
}

function Convert-RvaToFileOffset([uint32] $Rva, [object[]] $Sections, [string] $Path) {
    foreach ($section in $Sections) {
        $size = [Math]::Max([uint64] $section.VirtualSize, [uint64] $section.SizeOfRawData)
        $start = [uint64] $section.VirtualAddress
        $end = $start + $size
        if ([uint64] $Rva -ge $start -and [uint64] $Rva -lt $end) {
            return [int64] $section.PointerToRawData + ([int64] $Rva - [int64] $section.VirtualAddress)
        }
    }

    throw "Could not resolve PE RVA 0x$($Rva.ToString('X8')) in $Path"
}

function Read-PeAsciiString([System.IO.BinaryReader] $Reader, [int64] $Offset, [string] $Path) {
    $Reader.BaseStream.Position = $Offset
    $bytes = [System.Collections.Generic.List[byte]]::new()
    while ($Reader.BaseStream.Position -lt $Reader.BaseStream.Length) {
        $value = $Reader.ReadByte()
        if ($value -eq 0) {
            return [System.Text.Encoding]::ASCII.GetString($bytes.ToArray())
        }

        $bytes.Add($value)
    }

    throw "Unterminated PE export name in $Path"
}

function Get-PeExportNames([string] $Path) {
    $file = Assert-PeX64 $Path
    $stream = [System.IO.File]::OpenRead($file.FullName)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        $reader.BaseStream.Position = $peOffset + 4
        $null = $reader.ReadUInt16()
        $numberOfSections = $reader.ReadUInt16()
        $reader.BaseStream.Position += 12
        $optionalHeaderSize = $reader.ReadUInt16()
        $reader.BaseStream.Position += 2

        $optionalHeaderOffset = $peOffset + 24
        $reader.BaseStream.Position = $optionalHeaderOffset
        $optionalHeaderMagic = $reader.ReadUInt16()
        $dataDirectoryOffset = switch ($optionalHeaderMagic) {
            0x20B { 0x70; break }
            0x10B { 0x60; break }
            default { throw "Unsupported optional PE header in $($file.FullName)" }
        }

        $reader.BaseStream.Position = $optionalHeaderOffset + $dataDirectoryOffset
        $exportDirectoryRva = $reader.ReadUInt32()
        $exportDirectorySize = $reader.ReadUInt32()
        if ($exportDirectoryRva -eq 0 -or $exportDirectorySize -eq 0) {
            throw "PE export directory is missing: $($file.FullName)"
        }

        $reader.BaseStream.Position = $optionalHeaderOffset + $optionalHeaderSize
        $sections = [System.Collections.Generic.List[object]]::new()
        for ($index = 0; $index -lt $numberOfSections; $index++) {
            $reader.BaseStream.Position += 8
            $sections.Add([pscustomobject]@{
                VirtualSize = $reader.ReadUInt32()
                VirtualAddress = $reader.ReadUInt32()
                SizeOfRawData = $reader.ReadUInt32()
                PointerToRawData = $reader.ReadUInt32()
            })
            $reader.BaseStream.Position += 16
        }

        $exportDirectoryOffset = Convert-RvaToFileOffset $exportDirectoryRva $sections.ToArray() $file.FullName
        $reader.BaseStream.Position = $exportDirectoryOffset + 24
        $numberOfNames = $reader.ReadUInt32()
        $reader.BaseStream.Position = $exportDirectoryOffset + 32
        $addressOfNamesRva = $reader.ReadUInt32()
        $addressOfNamesOffset = Convert-RvaToFileOffset $addressOfNamesRva $sections.ToArray() $file.FullName

        $exports = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($index = 0; $index -lt $numberOfNames; $index++) {
            $reader.BaseStream.Position = $addressOfNamesOffset + (4 * $index)
            $nameRva = $reader.ReadUInt32()
            $nameOffset = Convert-RvaToFileOffset $nameRva $sections.ToArray() $file.FullName
            $exports.Add((Read-PeAsciiString $reader $nameOffset $file.FullName)) | Out-Null
        }

        return $exports
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-LibMpvBuilds([string] $Root, [string] $ContractFile) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "libmpv validation root was not found: $Root"
    }

    $contract = Get-LibMpvBuildContract $ContractFile
    $result = [ordered]@{}

    foreach ($variantName in @('Normal', 'X86_64V3')) {
        $variant = $contract[$variantName]
        $path = Join-Path $Root $variant.FileName
        $file = Assert-PeX64 $path
        $exports = Get-PeExportNames $file.FullName
        foreach ($requiredExport in $contract.RequiredExports) {
            if (-not $exports.Contains($requiredExport)) {
                throw "Required libmpv export '$requiredExport' is missing from $($file.FullName)"
            }
        }

        $result[$variantName] = [pscustomobject]@{
            File = $file.FullName
            Length = $file.Length
            Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
            Exports = $exports
        }
    }

    if ($result.Normal.Sha256 -eq $result.X86_64V3.Sha256) {
        throw "libmpv normal and x86-64-v3 files must not be byte-for-byte identical under $Root"
    }

    return [pscustomobject]$result
}
