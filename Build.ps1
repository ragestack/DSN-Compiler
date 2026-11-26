# Автор и правообладатель: Леонов Денис <466611@mail.ru>
#Requires -Version 5.1
param([string]$Version = '',[string]$WorkDirectory = '',[string]$ReleaseDirectory = '',[string]$SdkDirectory = '',[switch]$CompileOnly)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    $arguments=@('-NoProfile','-NonInteractive','-STA','-ExecutionPolicy','Bypass','-File',$PSCommandPath)
    if($Version) { $arguments+=@('-Version',$Version) }
    if($WorkDirectory) { $arguments+=@('-WorkDirectory',$WorkDirectory) }
    if($ReleaseDirectory) { $arguments+=@('-ReleaseDirectory',$ReleaseDirectory) }
    if($SdkDirectory) { $arguments+=@('-SdkDirectory',$SdkDirectory) }
    if($CompileOnly) { $arguments+='-CompileOnly' }
    & (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') @arguments
    exit $LASTEXITCODE
}
if(-not [Environment]::Is64BitProcess) { throw 'Требуется 64-разрядный Windows PowerShell.' }
$root=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$workRoot=if($WorkDirectory){[IO.Path]::GetFullPath($WorkDirectory).TrimEnd('\')}else{$root}
$releaseRoot=if($ReleaseDirectory){[IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\')}else{Join-Path $root 'release'}
$lock=Get-Content -LiteralPath (Join-Path $root 'toolchain.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(-not $Version) { $Version=$lock.product_version }
if($Version -notmatch '^[0-9A-Za-z.-]{1,40}$') { throw 'Неверная версия приложения.' }
foreach($folder in @('.tools','.cache','.state','.tmp','artifacts')) {
 $path=Join-Path $workRoot $folder
 if((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Служебный каталог является ссылкой.' }
}
$sdk=if($SdkDirectory){[IO.Path]::GetFullPath($SdkDirectory).TrimEnd('\')}else{Join-Path $workRoot '.tools\dotnet'}
if(-not (Test-Path -LiteralPath (Join-Path $sdk 'dotnet.exe'))) {
 $payload=Join-Path $root ('dependencies\dotnet-sdk-'+$lock.sdk_version+'-win-x64.zip')
 if(-not (Test-Path -LiteralPath $payload)) { $payload=Join-Path $workRoot ('.cache\downloads\dotnet-sdk-'+$lock.sdk_version+'-win-x64.zip') }
 if(-not (Test-Path -LiteralPath $payload)) { throw 'Поместите закреплённый SDK ZIP в dependencies. Скачивание не выполняется / Supply the pinned SDK ZIP in dependencies. No download is performed.' }
 if((Get-FileHash -LiteralPath $payload -Algorithm SHA512).Hash -ine $lock.sdk_asset.hash) { throw 'Неверный SHA-512 архива SDK.' }
 if(Test-Path -LiteralPath $sdk) { throw 'Каталог SDK незавершён: используйте новую папку исходников.' }
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead($payload)
 try {
  foreach($entry in $archive.Entries) {
   $path=[IO.Path]::GetFullPath((Join-Path $sdk $entry.FullName))
   if(-not $path.StartsWith($sdk+'\',[StringComparison]::OrdinalIgnoreCase) -or $path.Length -gt 245) { throw 'Недопустимый или слишком длинный путь SDK. Используйте короткий каталог.' }
  }
 } finally { $archive.Dispose() }
 Write-Host 'Распаковка SDK / Extracting SDK...'
 [IO.Compression.ZipFile]::ExtractToDirectory($payload,$sdk)
}
if((Get-Item -LiteralPath $sdk).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'SDK является ссылкой.' }
function Invoke-Sdk([string[]]$Arguments) {
$variables = @{
    DOTNET_ROOT = '.tools\dotnet'; DOTNET_ROOT_X64 = '.tools\dotnet'
    DOTNET_CLI_HOME = '.state\dotnet'; NUGET_PACKAGES = '.cache\nuget\packages'
    NUGET_HTTP_CACHE_PATH = '.cache\nuget\http'; NUGET_SCRATCH = '.tmp\nuget'
    NUGET_PLUGINS_CACHE_PATH = '.cache\nuget\plugins'; TEMP = '.tmp'; TMP = '.tmp'
    DOTNET_BUNDLE_EXTRACT_BASE_DIR = '.tmp\dotnet-bundle'; MSBUILDDEBUGPATH = '.state\msbuild'
}
$process = [Diagnostics.ProcessStartInfo]::new()
$process.FileName = Join-Path $sdk 'dotnet.exe'
$process.WorkingDirectory = $root
$process.UseShellExecute = $false
if ($true) {
    $process.RedirectStandardOutput = $true
    $process.RedirectStandardError = $true
    $process.StandardOutputEncoding = [Text.Encoding]::UTF8
    $process.StandardErrorEncoding = [Text.Encoding]::UTF8
}
foreach ($key in $variables.Keys) {
    $path = if($key -in @('DOTNET_ROOT','DOTNET_ROOT_X64')){$sdk}else{Join-Path $workRoot $variables[$key]}
    [IO.Directory]::CreateDirectory($path) | Out-Null
    $process.EnvironmentVariables[$key] = $path
}
$process.EnvironmentVariables['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
$process.EnvironmentVariables['DOTNET_GENERATE_ASPNET_CERTIFICATE'] = 'false'
$process.EnvironmentVariables['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = 'false'
$process.EnvironmentVariables['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = 'true'
$process.EnvironmentVariables['DOTNET_NOLOGO'] = 'true'
$process.EnvironmentVariables['MSBUILDDISABLENODEREUSE'] = '1'
# Совместимо с Windows PowerShell 5.1; кавычки следуют правилам Windows argv.
$quoted = foreach ($argument in $Arguments) {
    $value = [string]$argument
    '"' + [regex]::Replace([regex]::Replace($value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}
$process.Arguments = $quoted -join ' '
$child = [Diagnostics.Process]::Start($process)
if ($true) {
    $stdout = $child.StandardOutput.ReadToEndAsync()
    $stderr = $child.StandardError.ReadToEndAsync()
}
$child.WaitForExit()
if ($true) {
    $outputText = $stdout.GetAwaiter().GetResult()
    $errorText = $stderr.GetAwaiter().GetResult()
    if ($outputText) { Write-Output $outputText.TrimEnd() }
    if ($errorText) { Write-Host $errorText.TrimEnd() }
}
if ($child.ExitCode -ne 0) { throw ('Ошибка локального SDK: '+$child.ExitCode) }
}
function New-Branding {
Add-Type -AssemblyName WindowsBase,PresentationCore,PresentationFramework
$assets = Join-Path $root 'src\DSN.Viewer\Assets'
[IO.Directory]::CreateDirectory($assets) | Out-Null
$settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = 'Prohibit'; $settings.XmlResolver = $null
$reader = [Xml.XmlReader]::Create((Join-Path $root 'AARIS_logo.svg'), $settings)
$svg = [Xml.XmlDocument]::new(); $svg.XmlResolver = $null
try { $svg.Load($reader) } finally { $reader.Dispose() }
$culture = [Globalization.CultureInfo]::InvariantCulture
$box = @($svg.DocumentElement.GetAttribute('viewBox') -split '\s+' | ForEach-Object { [double]::Parse($_,$culture) })
if($box.Count -ne 4 -or $box[0] -ne 0 -or $box[1] -ne 0 -or $box[2] -ne $box[3] -or $box[2] -le 0) { throw 'Требуется квадратный viewBox с началом 0,0' }
function Read-Style([string]$Text) {
    $style=@{}
    foreach($part in $Text.Split(';')) { $pair=$part.Split(':'); if($pair.Count -eq 2) { $style[$pair[0].Trim()]=$pair[1].Trim() } }
    return $style
}
$classes=@{}
foreach($style in $svg.SelectNodes("//*[local-name()='style']")) {
    foreach($match in [regex]::Matches($style.InnerText,'\.([\w-]+)\s*\{([^}]+)\}')) { $classes[$match.Groups[1].Value]=Read-Style $match.Groups[2].Value }
}
$drawing=[Windows.Media.DrawingGroup]::new()
$bounds=[Windows.Rect]::new(0,0,$box[2],$box[3])
$drawing.Children.Add([Windows.Media.GeometryDrawing]::new([Windows.Media.Brushes]::Transparent,$null,[Windows.Media.RectangleGeometry]::new($bounds)))
$brushConverter=[Windows.Media.BrushConverter]::new()
$paths=0
foreach($element in $svg.DocumentElement.ChildNodes) {
    if($element.NodeType -ne 'Element' -or $element.LocalName -in @('defs','title','desc','metadata')) { continue }
    if($element.LocalName -ne 'path' -or $element.HasAttribute('transform')) { throw 'В логотипе появилась неподдерживаемая SVG-конструкция' }
    $style=@{'fill'='black';'fill-rule'=$svg.DocumentElement.GetAttribute('fill-rule')}
    foreach($class in ($element.GetAttribute('class') -split '\s+')) { if($classes.ContainsKey($class)) { foreach($key in $classes[$class].Keys) { $style[$key]=$classes[$class][$key] } } }
    foreach($attribute in @('fill','stroke','stroke-width','fill-rule')) { if($element.HasAttribute($attribute)) { $style[$attribute]=$element.GetAttribute($attribute) } }
    $geometry=[Windows.Media.PathGeometry]::CreateFromGeometry([Windows.Media.Geometry]::Parse($element.GetAttribute('d')))
    $geometry.FillRule=if($style['fill-rule'] -eq 'evenodd'){'EvenOdd'}else{'Nonzero'}
    $fill=if($style['fill'] -eq 'none'){$null}else{$brushConverter.ConvertFromInvariantString($style['fill'])}
    $pen=$null
    if($style.ContainsKey('stroke') -and $style['stroke'] -ne 'none') {
        $thickness=if($style.ContainsKey('stroke-width')){[double]::Parse($style['stroke-width'],$culture)}else{1}
        $pen=[Windows.Media.Pen]::new($brushConverter.ConvertFromInvariantString($style['stroke']),$thickness); $pen.MiterLimit=4
    }
    $drawing.Children.Add([Windows.Media.GeometryDrawing]::new($fill,$pen,$geometry)); $paths++
}
if($paths -eq 0) { throw 'В логотипе нет векторных контуров' }
$drawing.Freeze()
$dictionary=[Windows.ResourceDictionary]::new(); $dictionary.Add('AARISLogo',[Windows.Media.DrawingImage]::new($drawing))
$xaml=[Windows.Markup.XamlWriter]::Save($dictionary)
$author='Автор и правообладатель: Леонов Денис <466611@mail.ru>'
[IO.File]::WriteAllText((Join-Path $assets 'Branding.xaml'),"<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<!-- $author -->`n$xaml`n",[Text.UTF8Encoding]::new($false))
$frames=@()
foreach($size in @(16,24,32,48,64,128,256)) {
    $visual=[Windows.Media.DrawingVisual]::new(); $context=$visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size/$box[2],$size/$box[3]))
    $context.DrawRoundedRectangle([Windows.Media.Brushes]::White,$null,$bounds,($box[2]*0.12),($box[3]*0.12))
    $context.DrawDrawing($drawing); $context.Pop(); $context.Close()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.MemoryStream]::new(); $encoder.Save($stream); $bytes=$stream.ToArray(); $stream.Dispose()
    $frames+=,@{Size=$size;Bytes=$bytes}
}
$icon=[IO.MemoryStream]::new(); $writer=[IO.BinaryWriter]::new($icon)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count
foreach($frame in $frames) {
    $side=if($frame.Size -eq 256){0}else{$frame.Size}
    $writer.Write([byte]$side); $writer.Write([byte]$side); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
    $offset+=$frame.Bytes.Length
}
foreach($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
$writer.Flush(); [IO.File]::WriteAllBytes((Join-Path $assets 'AARIS.ico'),$icon.ToArray()); $writer.Dispose()
Write-Output ('Ресурсы AARIS созданы: '+$paths+' контура, '+$frames.Count+' размеров иконки.')
}

function Copy-Package([string]$Staging,[string]$SourceFingerprint) {
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 [IO.Directory]::CreateDirectory($releaseRoot) | Out-Null
 $target=Join-Path $releaseRoot ('DSNCompiler-'+$Version+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
 [IO.Directory]::CreateDirectory($target) | Out-Null
 $required=@('DSNViewer.exe','DSNViewer.dll','DSNViewer.deps.json','DSNViewer.runtimeconfig.json','DSNRunner.exe','DSNRunner.dll','DSNRunner.deps.json','DSNRunner.runtimeconfig.json','DSN.Core.dll','DSN.Execution.dll')
 foreach($file in $required) { Copy-Item -LiteralPath (Join-Path $Staging $file) -Destination $target }
 $content=[ordered]@{
  'INC'=@('ADSP21','RV32I','MSP430' | Where-Object { (Test-Path -LiteralPath (Join-Path $root ('INC/'+$_+'.INC'))) -and (Test-Path -LiteralPath (Join-Path $root ('implementations/'+$_+'.json'))) } | ForEach-Object { $_+'.INC' })
  'implementations'=@('ADSP21','RV32I','MSP430' | Where-Object { (Test-Path -LiteralPath (Join-Path $root ('INC/'+$_+'.INC'))) -and (Test-Path -LiteralPath (Join-Path $root ('implementations/'+$_+'.json'))) } | ForEach-Object { $_+'.json' })
  'languages'=@('ru.json','en.json')
 }
 if($content.INC.Count -eq 0) { throw 'Подключите хотя бы одну библиотеку INC с описанием реализации.' }
 foreach($folder in $content.Keys) {
  $destination=if($folder -eq 'INC'){'libraries'}else{$folder}
  [IO.Directory]::CreateDirectory((Join-Path $target $destination)) | Out-Null
  foreach($file in $content[$folder]) { Copy-Item -LiteralPath (Join-Path (Join-Path $root $folder) $file) -Destination (Join-Path $target $destination) }
 }
 foreach($file in @('README.md','THIRD_PARTY_NOTICES.txt','LICENSE.txt','Setup-NativeTools.ps1')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $target }
 $runtime=Join-Path $target 'runtime'
 [IO.Directory]::CreateDirectory($runtime) | Out-Null
 foreach($file in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')) { Copy-Item -LiteralPath (Join-Path $sdk $file) -Destination $runtime }
 foreach($part in @('host\fxr','shared\Microsoft.NETCore.App','shared\Microsoft.WindowsDesktop.App')) {
  $from=Join-Path (Join-Path $sdk $part) $lock.runtime_version
  if(-not (Test-Path -LiteralPath $from)) { throw 'В SDK нет закреплённой среды исполнения.' }
  foreach($file in Get-ChildItem -LiteralPath $from -File -Recurse) {
   if($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Файл runtime является ссылкой.' }
   $path=Join-Path (Join-Path (Join-Path $runtime $part) $lock.runtime_version) $file.FullName.Substring($from.Length+1)
   [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
   Copy-Item -LiteralPath $file.FullName -Destination $path
  }
 }
 $files=@(Get-ChildItem -LiteralPath $target -File -Recurse | Sort-Object FullName | ForEach-Object {
  [ordered]@{Path=$_.FullName.Substring($target.Length+1).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
 })
 $manifest=[ordered]@{Product='DSN Compiler';Version=$Version;Stage='technical-release-candidate';PackagePolicy='public-inc-contracts-v1';DistributionLicenseApproved=$true;LicenseFile='LICENSE.txt';LicenseSha256=(Get-FileHash -LiteralPath (Join-Path $root 'LICENSE.txt') -Algorithm SHA256).Hash;SourceFingerprint=$SourceFingerprint;Runtime=$lock.runtime_version;Platform='Windows x64';ContainsDSN=$false;Files=$files}
 $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $target 'release.manifest.json') -Encoding utf8
 $zip=$target+'.zip'; [IO.Compression.ZipFile]::CreateFromDirectory($target,$zip)
 $latest=[ordered]@{Folder=$target;Archive=$zip;Sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash;Version=$Version}
 $latest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot 'latest.json') -Encoding utf8
 $latest | ConvertTo-Json
}
$actual=Invoke-Sdk -Arguments @('--version')
if(($actual | Out-String).Trim() -ne $lock.sdk_version) { throw 'Версия SDK не совпала с закреплённой.' }
New-Branding
$staging=Join-Path $workRoot 'artifacts\prototype'
foreach($project in @('DSN.Viewer','DSN.Runner')) {
 Invoke-Sdk -Arguments @('publish',('src/'+$project+'/'+$project+'.csproj'),'--configuration','Release','--self-contained','false','--output',$staging,'--disable-build-servers','--verbosity','minimal',('-p:ArtifactsPath='+(Join-Path $workRoot 'artifacts/build')),('-p:Version='+$Version),'-p:EnableSourceControlManagerQueries=false','-p:EnableSourceLink=false')
}
if($CompileOnly) { Write-Host ('Скомпилировано / Compiled: '+$staging); exit 0 }
# Отпечаток относится только к входам публичной сборки; внутренние материалы не читаются.
$inputFiles=@('README.md','Build.ps1','Setup-NativeTools.ps1','Directory.Build.props','global.json','NuGet.Config','toolchain.lock.json','AARIS_logo.svg','THIRD_PARTY_NOTICES.txt','LICENSE.txt','languages/ru.json','languages/en.json') | ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) }
foreach($id in @('ADSP21','RV32I','MSP430')) {
 foreach($relative in @(('INC/'+$id+'.INC'),('implementations/'+$id+'.json'))) {
  $path=Join-Path $root $relative
  if(Test-Path -LiteralPath $path) { $inputFiles+=Get-Item -LiteralPath $path }
 }
}
foreach($folder in @('src/DSN.Core','src/DSN.Execution','src/DSN.Viewer','src/DSN.Runner')) {
 $inputFiles+=Get-ChildItem -LiteralPath (Join-Path $root $folder) -File -Recurse | Where-Object {
  $_.FullName -notmatch '[\/](bin|obj)[\/]' -and $_.Extension -in @('.cs','.csproj','.xaml','.INC','.json','.txt','.html') -and
  $_.Name -notin @('AssemblyInfo.cs','Branding.xaml') -and $_.Name -notlike '*.provenance.json'
 }
}
$lines=[string[]]@($inputFiles | ForEach-Object { $_.FullName.Substring($root.Length+1).Replace('\','/')+':'+(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
[Array]::Sort($lines,[StringComparer]::Ordinal)
$sha=[Security.Cryptography.SHA256]::Create()
try { $fingerprint=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))).Replace('-','') } finally { $sha.Dispose() }
Copy-Package $staging $fingerprint
Write-Host ('Готово / Done: '+(Join-Path $releaseRoot 'latest.json'))
