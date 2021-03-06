param (
	[ValidatePattern("^\d\.\d\.(?:\d\.\d$|\d$)")]
	[string]
	$ReleaseVersionNumber,
	[string]
	$PreReleaseName
)

$ReleaseMode = "Release"
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot -ChildPath "build"
$ToolsFolder = Join-Path -Path $BuildFolder -ChildPath "tools"
$TempFolder = Join-Path -Path $BuildFolder -ChildPath "Temp"
$SolutionRoot = Join-Path -Path $RepoRoot -ChildPath "src"
$SolutionInfoPath = Join-Path -Path $SolutionRoot -ChildPath "SolutionInfo.cs"
$CLIOutPutPath = Join-Path -Path $SolutionRoot -ChildPath "LeChiffre" | Join-Path -ChildPath "bin" | Join-Path -ChildPath $ReleaseMode
Write-Host $CLIOutPutPath

# When no parameters have been passed in, set version to current
$currentVersion = Get-Content -Path "$SolutionInfoPath" | %{ [Regex]::Matches($_, "AssemblyInformationalVersion\(`"(.+)?`"\)") } | %{ $_.Captures.Groups[1].Value }
if([string]::IsNullOrEmpty($ReleaseVersionNumber) -eq $true) 
{
    $ReleaseVersionNumber = $currentVersion.Split('-')[0]
    $PreReleaseName = $currentVersion.Split('-')[1]
}

# Create tools folder
New-Item "$ToolsFolder" -type directory -force

# Go get nuget.exe if we don't have it
$NuGet = "$ToolsFolder\nuget.exe"
$FileExists = Test-Path $NuGet 
If ($FileExists -eq $False) {
	Write-Host "Retrieving nuget.exe..."
	$SourceNugetExe = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
	Invoke-WebRequest $SourceNugetExe -OutFile $NuGet
}

# Ensure we have vswhere
$vswhere = "$ToolsFolder\vswhere.exe"
if (-not (Test-Path $vswhere))
{
	Write-Host "Download VsWhere..."
	$path = "$BuildFolder\tmp"
	&$nuget install vswhere -OutputDirectory $path -Verbosity quiet
	$dir = ls "$path\vswhere.*" | sort -property Name -descending | select -first 1
	$file = ls -path "$dir" -name vswhere.exe -recurse
	mv "$dir\$file" $vswhere   
    Remove-Item $path -Recurse
}

$MSBuild = &$vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if ($MSBuild) 
{
	$MSBuild = join-path $MSBuild 'MSBuild\15.0\Bin\MSBuild.exe'
	if (-not (test-path $msbuild)) 
    {
    	throw "MSBuild not found!"
	}
}

$FullVersion = $ReleaseVersionNumber
if([string]::IsNullOrEmpty($PreReleaseName) -eq $false) 
{
    $FullVersion = "$ReleaseVersionNumber-$PreReleaseName"
}

# Returns the full path if $file is relative to $pwd
function Get-FullPath($file)
{
  $path = [System.IO.Path]::Combine($pwd, $file)
  $path = [System.IO.Path]::GetFullPath($path)
  return $path
}

# Regex-replaces content in a file
function Replace-FileText($filename, $source, $replacement)
{
  $filepath = Get-FullPath $filename
  $text = [System.IO.File]::ReadAllText($filepath)
  $text = [System.Text.RegularExpressions.Regex]::Replace($text, $source, $replacement)
  $utf8bom = New-Object System.Text.UTF8Encoding $true
  [System.IO.File]::WriteAllText($filepath, $text, $utf8bom)
}

# Set the version number in SolutionInfo.cs

Replace-FileText "$SolutionInfoPath" `
    "AssemblyFileVersion\(`"(.+)?`"\)" `
    "AssemblyFileVersion(`"$FullVersion`")"
Replace-FileText "$SolutionInfoPath" `
    "AssemblyInformationalVersion\(`"(.+)?`"\)" `
    "AssemblyInformationalVersion(`"$FullVersion`")"
Replace-FileText "$SolutionInfoPath" `
    "AssemblyVersion\(`"(.+)?`"\)" `
    "AssemblyVersion(`"$ReleaseVersionNumber.*`")"

# Build the solution in release mode
$SolutionPath = Join-Path -Path $SolutionRoot -ChildPath "LeChiffre.sln";

# Restore nuget packages
Write-Host "Restoring nuget packages..."
& $NuGet restore $SolutionPath

# Clean sln for all deploys
& $MSBuild "$SolutionPath" /p:Configuration=$ReleaseMode /maxcpucount /t:Clean
if (-not $?)
{
	throw "The MSBuild process returned an error code."
}

# MSBuild
& $MSBuild "$SolutionPath" /p:Configuration=$ReleaseMode /maxcpucount
if (-not $?)
{
	throw "The MSBuild process returned an error code."
}

# nuget build
Remove-Item $BuildFolder\*.nupkg
$nuSpec = Join-Path -Path $BuildFolder -ChildPath "LeChiffre.Core.nuspec";
& $NuGet pack $nuSpec -OutputDirectory $BuildFolder -Version $FullVersion


# CLI build zip
Remove-Item $BuildFolder\*.zip
$DestZIP = "$BuildFolder\LeChiffre.$FullVersion.zip" 
Write-Host "Zipping up CLI tool to $DestZIP"
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($CLIOutPutPath, $DestZIP) 