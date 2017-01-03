param (
    [switch]$SkipTests,
    [switch]$SkipPack,
    [switch]$Push,
    [switch]$StableVersion
)

$BuildNumberDateBase = "2017-01-02"
$RepoRoot = $PSScriptRoot
$PackageId = "NuGet.CatalogReader"
$SleetFeedId = "packages"

# Load common build script helper methods
. "$PSScriptRoot\build\common.ps1"

# Ensure dotnet.exe exists in .cli
Install-DotnetCLI $RepoRoot

# Ensure packages.config packages
Install-PackagesConfig $RepoRoot

$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$nugetExe = Join-Path $RepoRoot '.nuget\nuget.exe'
$dotnetExe = Get-DotnetCLIExe $RepoRoot
$nupkgWrenchExe = Join-Path $RepoRoot "packages\NupkgWrench.1.1.0\tools\NupkgWrench.exe"
$sleetExe = Join-Path $RepoRoot "packages\Sleet.1.0.1\tools\Sleet.exe"

# Clear artifacts
Remove-Item -Recurse -Force $ArtifactsDir | Out-Null

# Git commit
$commitHash = git rev-parse HEAD | Out-String
$commitHash = $commitHash.Trim()
$gitBranch = git rev-parse --abbrev-ref HEAD | Out-String
$gitBranch = $gitBranch.Trim()

# Restore project.json files
& $dotnetExe restore (Join-Path $RepoRoot "$PackageId.sln")

if (-not $?)
{
    Write-Host "Restore failed!"
    exit 1
}

# Run tests
if (-not $SkipTests)
{
    & $dotnetExe test (Join-Path $RepoRoot "test\$PackageId.Tests\$PackageId.Tests.csproj")

    if (-not $?)
    {
        Write-Host "tests failed!!!"
        exit 1
    }
}

if (-not $SkipPack)
{
    # Pack
    if ($StableVersion)
    {
        & $dotnetExe pack (Join-Path $RepoRoot "src\$PackageId\$PackageId.csproj") --configuration release --output $ArtifactsDir /p:NoPackageAnalysis=true
    }
    else
    {
        $buildNumber = Get-BuildNumber $BuildNumberDateBase

        & $dotnetExe pack (Join-Path $RepoRoot "src\$PackageId\$PackageId.csproj") --configuration release --output $ArtifactsDir --version-suffix "beta.$buildNumber" /p:NoPackageAnalysis=true
    }

    if (-not $?)
    {
       Write-Host "Pack failed!"
       exit 1
    }

    # Get version number
    $nupkgVersion = (& $nupkgWrenchExe version $ArtifactsDir --exclude-symbols -id $PackageId) | Out-String
    $nupkgVersion = $nupkgVersion.Trim()

    $updatedVersion = $nupkgVersion + "+git." + $commitHash

    & $nupkgWrenchExe nuspec edit --property version --value $updatedVersion $ArtifactsDir --exclude-symbols -id $PackageId
    & $nupkgWrenchExe updatefilename $ArtifactsDir --exclude-symbols -id $PackageId

    Write-Host "-----------------------------"
    Write-Host "Version: $updatedVersion"
    Write-Host "-----------------------------"
}

$SleetConfig = Get-SleetConfig $RepoRoot

if ($Push -and (Test-Path $SleetConfig) -and ($gitBranch -eq "master"))
{
    & $sleetExe push --source $SleetFeedId --config $SleetConfig $ArtifactsDir

    if (-not $?)
    {
       Write-Host "Push failed!"
       exit 1
    }

    & $sleetExe validate --source $SleetFeedId --config $SleetConfig

    if (-not $?)
    {
       Write-Host "Feed corrupt!"
       exit 1
    }
}

Write-Host "Success!"