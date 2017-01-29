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
$ILMergeExe = Join-Path $RepoRoot 'packages\ILRepack.2.0.12\tools\ILRepack.exe'
$zipExe = Join-Path $RepoRoot 'packages\7ZipCLI.9.20.0\tools\7za.exe'
$nugetMirrorExe = Join-Path $ArtifactsDir "NuGetMirror.exe"

# Clear artifacts
Remove-Item -Recurse -Force $ArtifactsDir | Out-Null
mkdir $ArtifactsDir | Out-Null

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

& $dotnetExe clean (Join-Path $RepoRoot "$PackageId.sln") --configuration release /m
& $dotnetExe build (Join-Path $RepoRoot "$PackageId.sln") --configuration release /m

if (-not $?)
{
    Write-Host "Build failed!"
    exit 1
}

# Run tests
if (-not $SkipTests)
{
    Run-Tests $RepoRoot $DotnetExe
}

$json608Lib  = (Join-Path $RepoRoot 'packages\Newtonsoft.Json.6.0.8\lib\net45')
$net46Root = (Join-Path $RepoRoot 'src\NuGetMirror\bin\release\net46')
$ILMergeOpts = , (Join-Path $net46Root 'NuGetMirror.exe')
$ILMergeOpts += Get-ChildItem $net46Root -Exclude @('*.exe', '*compression*', '*System.*', '*.config', '*.pdb', '*.json', '*.xml') | where { ! $_.PSIsContainer } | %{ $_.FullName }
$ILMergeOpts += '/out:' + (Join-Path $ArtifactsDir 'NuGetMirror.exe')
$ILMergeOpts += '/log'

# Newtonsoft.Json 6.0.8 is used by NuGet and is needed only for reference.
$ILMergeOpts += '/lib:' + $json608Lib
$ILMergeOpts += '/ndebug'
$ILMergeOpts += '/parallel'

Write-Host "ILMerging NuGetMirror.exe"
& $ILMergeExe $ILMergeOpts | Out-Null

if (-not $?)
{
    # Get failure message
    Write-Host $ILMergeExe $ILMergeOpts
    & $ILMergeExe $ILMergeOpts
    Write-Host "ILMerge failed!"
    exit 1
}

if (-not $SkipPack)
{
    # Pack
    if ($StableVersion)
    {
        & $dotnetExe pack (Join-Path $RepoRoot "src\$PackageId\$PackageId.csproj") --configuration release --output $ArtifactsDir /p:NoPackageAnalysis=true

        if (-not $?)
        {
            Write-Host "Pack failed!"
            exit 1
        }

        & $dotnetExe pack (Join-Path $RepoRoot "src\NuGetMirror\NuGetMirror.csproj") --configuration release --output $ArtifactsDir /p:NoPackageAnalysis=true

        if (-not $?)
        {
            Write-Host "Pack failed!"
            exit 1
        }
    }
    else
    {
        $buildNumber = Get-BuildNumber $BuildNumberDateBase

        & $dotnetExe pack (Join-Path $RepoRoot "src\$PackageId\$PackageId.csproj") --configuration release --output $ArtifactsDir --version-suffix "beta.$buildNumber" /p:NoPackageAnalysis=true

        if (-not $?)
        {
            Write-Host "Pack failed!"
            exit 1
        }

        & $dotnetExe pack (Join-Path $RepoRoot "src\NuGetMirror\NuGetMirror.csproj") --configuration release --output $ArtifactsDir --version-suffix "beta.$buildNumber" /p:NoPackageAnalysis=true

        if (-not $?)
        {
            Write-Host "Pack failed!"
            exit 1
        }
    }

        # Clear out net46 lib
    & $nupkgWrenchExe files emptyfolder artifacts -p lib/net46 --id NuGetMirror
    & $nupkgWrenchExe nuspec frameworkassemblies clear artifacts
    & $nupkgWrenchExe nuspec dependencies emptygroup artifacts -f net46 --id NuGetMirror

    # Add net46 tools
    & $nupkgWrenchExe files add --path tools/NuGetMirror.exe --file $nugetMirrorExe --id NuGetMirror

    # Get version number
    $nupkgVersion = (& $nupkgWrenchExe version $ArtifactsDir --id $PackageId) | Out-String
    $nupkgVersion = $nupkgVersion.Trim()

    $updatedVersion = $nupkgVersion + "+git." + $commitHash

    & $nupkgWrenchExe nuspec edit --property version --value $updatedVersion $ArtifactsDir
    & $nupkgWrenchExe updatefilename $ArtifactsDir

    # Create xplat tar
    $versionFolderName = "nugetmirror.$nupkgVersion".ToLowerInvariant()
    $publishDir = Join-Path $ArtifactsDir publish
    $versionFolder = Join-Path $publishDir $versionFolderName
    & $dotnetExe publish src\NuGetMirror\NuGetMirror.csproj -o $versionFolder -f netcoreapp1.0 --configuration release

    if (-not $?)
    {
        Write-Host "Publish failed!"
        exit 1
    }

    pushd $publishDir

    # clean up pdbs
    rm $versionFolderName\*.pdb

    # bzip the portable netcore app folder
    & $zipExe "a" "$versionFolderName.tar" $versionFolderName
    & $zipExe "a" "..\$versionFolderName.tar.bz2" "$versionFolderName.tar"

    if (-not $?)
    {
        Write-Host "Zip failed!"
        exit 1
    }

    popd

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