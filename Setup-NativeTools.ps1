# Автор и правообладатель: Леонов Денис <466611@mail.ru>
#Requires -Version 5.1
[CmdletBinding()]
param(
 [ValidateSet('RV32I','MSP430')][string[]]$Architecture=@('RV32I','MSP430'),
 [string]$ToolsRoot,
 [string]$ArchiveDirectory,
 [ValidateSet('ru','en')][string]$Language='ru'
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$catalog=Get-Content -LiteralPath (Join-Path $root ('languages\'+$Language+'.json')) -Raw -Encoding UTF8 | ConvertFrom-Json
function Message([string]$Key,[object[]]$Values=@()) { [string]::Format([string]$catalog.Strings.('NativeSetup.'+$Key),$Values) }
function Assert-PlainPath([string]$Path) {
 $item=[IO.Path]::GetFullPath($Path)
 while ($item) {
  if ((Test-Path -LiteralPath $item) -and ((Get-Item -LiteralPath $item -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw (Message 'PlainPath' @($item)) }
  $parent=[IO.Path]::GetDirectoryName($item); if ($parent -eq $item) { break }; $item=$parent
 }
}
function Save-Json([string]$Path,$Value) { [IO.File]::WriteAllText($Path,($Value | ConvertTo-Json -Depth 8)+"`n",(New-Object Text.UTF8Encoding($false))) }
function Run-Tool([string]$Tool,[string[]]$Arguments,[string]$Folder,[string]$Log) {
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName=$Tool; $start.WorkingDirectory=$Folder; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
 # Аргументы передаются непосредственно процессу по правилам Windows argv.
 $start.Arguments=($Arguments | ForEach-Object { '"'+[regex]::Replace([regex]::Replace([string]$_,'(\\*)"','$1$1\"'),'(\\+)$','$1$1')+'"' }) -join ' '
 $process=[Diagnostics.Process]::Start($start)
 $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
 if (-not $process.WaitForExit(120000)) { $process.Kill(); $process.WaitForExit(); throw (Message 'Budget' @($Tool)) }
 $output=$stdout.GetAwaiter().GetResult(); $errors=$stderr.GetAwaiter().GetResult()
 Save-Json (Join-Path $Folder $Log) ([ordered]@{Tool=[IO.Path]::GetFileName($Tool);Arguments=$Arguments;ExitCode=$process.ExitCode;Output=$output;Errors=$errors})
 if ($process.ExitCode -ne 0) { throw (Message 'Tool' @($Tool,$process.ExitCode,(Join-Path $Folder $Log))) }
 return $output
}
function Test-Tools([string]$Folder,$Spec) {
 $check=Join-Path $Folder ('checks\'+[Guid]::NewGuid().ToString('N'))
 Assert-PlainPath $check
 [IO.Directory]::CreateDirectory($check) | Out-Null
 $bin=Join-Path $Folder 'bin'; $prefix=$Spec.Prefix
 $versions=@{}
 foreach($name in @('as','ld','readelf','objdump')) {
  $text=Run-Tool (Join-Path $bin ($prefix+$name+'.exe')) @('--version') $check ($name+'-version.json')
  $versions[$name]=$text.Split("`n")[0].Trim()
 }
 $risc=$Spec.Architecture -eq 'RV32I'
 $assembly=$(if($risc){".option norvc`n.option norelax`n"}else{".cpu 430`n"})+".text`n.global _start`n_start:`n    nop`n"+$(if($risc){"    j _start`n"}else{"    jmp _start`n"})
 $linker="ENTRY(_start)`nSECTIONS { . = "+$(if($risc){'0x80000000'}else{'0x4000'})+"; .text : { *(.text*) } }`n"
 [IO.File]::WriteAllText((Join-Path $check 'check.S'),$assembly,(New-Object Text.UTF8Encoding($false)))
 [IO.File]::WriteAllText((Join-Path $check 'check.ld'),$linker,(New-Object Text.UTF8Encoding($false)))
 $flags=if($risc){@('-march=rv32i','-mabi=ilp32','-mno-relax')}else{@('-mcpu=430','-mmcu=msp430f1611')}
 Run-Tool (Join-Path $bin ($prefix+'as.exe')) ($flags+@('-o','check.o','check.S')) $check 'assemble.json' | Out-Null
 Run-Tool (Join-Path $bin ($prefix+'ld.exe')) @('-m',$(if($risc){'elf32lriscv'}else{'msp430elf'}),'--no-relax','-T','check.ld','-o','check.elf','check.o') $check 'link.json' | Out-Null
 $inspection=Run-Tool (Join-Path $bin ($prefix+'readelf.exe')) @('-h','-A','check.elf') $check 'inspect.json'
 $disassembly=Run-Tool (Join-Path $bin ($prefix+'objdump.exe')) @('-d','check.elf') $check 'disassemble.json'
 $bytes=[IO.File]::ReadAllBytes((Join-Path $check 'check.elf'))
 if ($bytes.Length -lt 52 -or [BitConverter]::ToString($bytes[0..6]) -ne '7F-45-4C-46-01-01-01' -or
     [BitConverter]::ToUInt16($bytes,16) -ne 2 -or [BitConverter]::ToUInt16($bytes,18) -ne $Spec.Machine -or
     ($risc -and $inspection -notmatch 'rv32i2p1') -or $disassembly -notmatch '<_start>') { throw (Message 'Image' @($Spec.Architecture,$check)) }
 return [ordered]@{Result='PASS';Architecture=$Spec.Architecture;ElfMachine=$Spec.Machine;ImageSha256=(Get-FileHash (Join-Path $check 'check.elf') -Algorithm SHA256).Hash;ToolVersions=$versions}
}
try {
 if (-not [Environment]::Is64BitProcess -or [Environment]::OSVersion.Platform -ne 'Win32NT') { throw (Message 'Windows') }
 if (-not $ToolsRoot) { $ToolsRoot=Join-Path $root '.tools\native' }
 $ToolsRoot=[IO.Path]::GetFullPath($ToolsRoot).TrimEnd('\')
 Assert-PlainPath $ToolsRoot; Assert-PlainPath (Join-Path $root 'settings')
 Assert-PlainPath (Join-Path $root 'settings\native-tools-setup.json')
 $settingsPath=Join-Path $root 'settings\native-tools.json'
 $settings=@{}
 if (Test-Path -LiteralPath $settingsPath) {
  if ((Get-Item $settingsPath).Length -gt 65536) { throw (Message 'Settings' @($settingsPath)) }
  $saved=Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
  if ($saved -isnot [Management.Automation.PSCustomObject]) { throw (Message 'Settings' @($settingsPath)) }
  foreach($property in $saved.PSObject.Properties) {
   if ($property.Value -isnot [string]) { throw (Message 'Settings' @($settingsPath)) }
   $settings[$property.Name]=$property.Value
  }
 }
 $specs=@(
  @{Architecture='RV32I';Version='15.2.0-1';Prefix='riscv-none-elf-';Machine=243;Dlls=@('libzstd.dll');
    File='xpack-riscv-none-elf-gcc-15.2.0-1-win32-x64.zip';Package='xpack-riscv-none-elf-gcc-15.2.0-1';
    Url='https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack/releases/download/v15.2.0-1/xpack-riscv-none-elf-gcc-15.2.0-1-win32-x64.zip';
    Sha256='85EF714DACD273B1DADF4AF4892774520AC01915BFA6DA816A56E7E41591E09E'},
  @{Architecture='MSP430';Version='9.3.1.11';Prefix='msp430-elf-';Machine=105;Dlls=@();
    File='msp430-gcc-9.3.1.11_win32.zip';Package='msp430-gcc-9.3.1.11_win32';
    Url='https://dr-download.ti.com/software-development/ide-configuration-compiler-or-debugger/MD-LlCjWuAbzH/9.3.1.2/msp430-gcc-9.3.1.11_win32.zip';
    Sha256='21EEBB2A4AE18161F4CEEB77582FDF641968FD14D33C33EA458C8F2BF7E19D63'}
 )
 Add-Type -AssemblyName System.IO.Compression
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 [IO.Directory]::CreateDirectory($ToolsRoot) | Out-Null
 $results=New-Object 'Collections.Generic.List[object]'
 foreach($spec in $specs | Where-Object { $_.Architecture -in $Architecture }) {
  $target=Join-Path $ToolsRoot ($spec.Architecture+'-'+$spec.Version)
  Assert-PlainPath $target
  if (Test-Path -LiteralPath $target) {
   $manifestPath=Join-Path $target 'native-tools.install.json'
   if (-not (Test-Path -LiteralPath $manifestPath)) { throw (Message 'NewFolder' @($target)) }
   $manifest=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
   if ($manifest.Format -ne 'DSNCompiler.NativeTools' -or $manifest.Version -ne 1 -or $manifest.Architecture -ne $spec.Architecture -or $manifest.ArchiveSha256 -ne $spec.Sha256) { throw (Message 'NewFolder' @($target)) }
   foreach($file in $manifest.Files) {
    $path=[IO.Path]::GetFullPath((Join-Path $target $file.Path))
    if (-not $path.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)) { throw (Message 'PlainPath' @($path)) }
    Assert-PlainPath $path
    if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) { throw (Message 'NewFolder' @($target)) }
   }
   Write-Host (Message 'Reuse' @($spec.Architecture,$target))
  } else {
   if ($ArchiveDirectory) { $archivePath=Join-Path ([IO.Path]::GetFullPath($ArchiveDirectory)) $spec.File }
   else {
    $cache=Join-Path $root '.cache\native-downloads'; Assert-PlainPath $cache; [IO.Directory]::CreateDirectory($cache) | Out-Null
    $archivePath=Join-Path $cache $spec.File
    if (-not (Test-Path -LiteralPath $archivePath)) {
     Write-Host (Message 'Download' @($spec.Architecture,$spec.Url))
     $partial=$archivePath+'.'+[Guid]::NewGuid().ToString('N')+'.partial'
     $tls=[Net.ServicePointManager]::SecurityProtocol
     try { [Net.ServicePointManager]::SecurityProtocol=$tls -bor [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri $spec.Url -OutFile $partial -UseBasicParsing -TimeoutSec 300 }
     finally { [Net.ServicePointManager]::SecurityProtocol=$tls }
     if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $spec.Sha256) { throw (Message 'Hash' @($partial)) }
     [IO.File]::Move($partial,$archivePath)
    }
   }
   if (-not (Test-Path -LiteralPath $archivePath)) { throw (Message 'Archive' @($spec.File,$ArchiveDirectory)) }
   if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $spec.Sha256) { throw (Message 'Hash' @($archivePath)) }
   $staging=Join-Path $ToolsRoot ('.pending-'+[Guid]::NewGuid().ToString('N'))
   [IO.Directory]::CreateDirectory($staging) | Out-Null
   $required=@('as','ld','readelf','objdump') | ForEach-Object { 'bin/'+$spec.Prefix+$_+'.exe' }
   $required+=@($spec.Dlls | ForEach-Object { 'bin/'+$_ })
   $found=@{}; $zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
   Write-Host (Message 'Extract' @($spec.Architecture))
   try { foreach($entry in $zip.Entries) {
    if ($entry.FullName -notlike ($spec.Package+'/*')) { throw (Message 'Archive' @($spec.File,$archivePath)) }
    $relative=$entry.FullName.Substring($spec.Package.Length+1)
    if (-not $relative -or $entry.FullName.EndsWith('/')) { continue }
    $path=[IO.Path]::GetFullPath((Join-Path $staging $relative))
    if (-not $path.StartsWith($staging+'\',[StringComparison]::OrdinalIgnoreCase) -or (($entry.ExternalAttributes -shr 16) -band 61440) -eq 40960) { throw (Message 'PlainPath' @($entry.FullName)) }
    # Сохраняются рабочие средства, их DLL, исходные лицензии и уведомления поставщика.
    if ($relative -in $required -or $relative -notmatch '/' -or $relative -match '(?i)(copying|copyright|licen[cs]e|notice|(^|/)l?gpl[^/]*\.(txt|html|pdf)$)') {
     if ($found.ContainsKey($relative)) { throw (Message 'Archive' @($spec.File,$archivePath)) }
     $found[$relative]=$true
     [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
     [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$false)
    }
   } } finally { $zip.Dispose() }
   foreach($relative in $required) { if (-not $found.ContainsKey($relative)) { throw (Message 'Archive' @($spec.File,$archivePath)) } }
   $proof=Test-Tools $staging $spec
   $files=@(Get-ChildItem -LiteralPath $staging -File -Recurse | Where-Object { $_.FullName -notlike ($staging+'\checks\*') } | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring($staging.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
   })
   Save-Json (Join-Path $staging 'native-tools.install.json') ([ordered]@{Format='DSNCompiler.NativeTools';Version=1;Architecture=$spec.Architecture;PackageVersion=$spec.Version;SourceUrl=$spec.Url;ArchiveSha256=$spec.Sha256;Files=$files;Verification=$proof})
   # Обе абсолютные границы проверяются непосредственно перед переносом собственной папки.
   if (-not $staging.StartsWith($ToolsRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $target.StartsWith($ToolsRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw (Message 'PlainPath' @($target)) }
   Assert-PlainPath $ToolsRoot; [IO.Directory]::Move($staging,$target)
  }
  $proof=Test-Tools $target $spec
  $settings[$spec.Architecture]=Join-Path $target 'bin'
  $results.Add([pscustomobject]@{Architecture=$spec.Architecture;Path=$settings[$spec.Architecture];Verification=$proof})
  Write-Host (Message 'Ready' @($spec.Architecture,$settings[$spec.Architecture]))
 }
 # Настройки заменяются только после успешной проверки всех выбранных архитектур.
 $settingsFolder=Join-Path $root 'settings'; [IO.Directory]::CreateDirectory($settingsFolder) | Out-Null
 $temporary=Join-Path $settingsFolder ('native-tools-'+[Guid]::NewGuid().ToString('N')+'.tmp')
 Save-Json $temporary $settings
 Assert-PlainPath $settingsPath
 # Явный .NET null сохраняет атомарную замену также в Windows PowerShell 5.1.
 if (Test-Path -LiteralPath $settingsPath) { [IO.File]::Replace($temporary,$settingsPath,[NullString]::Value) } else { [IO.File]::Move($temporary,$settingsPath) }
 Save-Json (Join-Path $settingsFolder 'native-tools-setup.json') ([ordered]@{Format='DSNCompiler.NativeToolsSetup';Version=1;Result='PASS';Checked=(Get-Date).ToString('o');Architectures=$results.ToArray()})
 Write-Host (Message 'Complete' @($settingsPath))
} catch { Write-Error $_; exit 1 }
