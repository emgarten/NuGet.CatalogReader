
# install CLI
Function Install-DotnetCLI {
    param(
        [string]$RepoRoot
    )

    $CLIRoot = Get-DotnetCLIRoot $RepoRoot

    New-Item -ItemType Directory -Force -Path $CLIRoot | Out-Null

    $env:DOTNET_HOME = $CLIRoot
    $installDotnet = Join-Path $CLIRoot "install.ps1"

    $DotnetExe = Get-DotnetCLIExe $RepoRoot

    if (-not (Test-Path $DotnetExe)) {

        New-Item -ItemType Directory -Force -Path $CLIRoot

        Write-Host "Fetching $installDotnet"

        Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installDotnet

        & $installDotnet -Channel 6.0 -i $CLIRoot
        & $installDotnet -Channel 8.0 -i $CLIRoot
        & $installDotnet -Channel 9.0 -i $CLIRoot

        if (-not (Test-Path $DotnetExe)) {
            Write-Log "Missing $DotnetExe"
            exit 1
        }
    }

    & $DotnetExe --info
}

Function Get-DotnetCLIRoot {
    param(
        [string]$RepoRoot
    )
    return Join-Path $RepoRoot ".cli"
}

Function Get-DotnetCLIExe {
    param(
        [string]$RepoRoot
    )

    $CLIRoot = Get-DotnetCLIRoot $RepoRoot

    return Join-Path $CLIRoot "dotnet.exe"
}

Function Get-NuGetExePath {
    param(
        [string]$RepoRoot
    )

    return Join-Path $RepoRoot ".nuget/nuget.exe"
}

# download .nuget\nuget.exe
Function Install-NuGetExe {
    param(
        [string]$RepoRoot
    )

    $nugetExe = Get-NuGetExePath $RepoRoot

    if (-not (Test-Path $nugetExe)) {
        Write-Host "Downloading nuget.exe"
        $nugetDir = Split-Path $nugetExe
        New-Item -ItemType Directory -Force -Path $nugetDir

        Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/v6.12.1/nuget.exe -OutFile $nugetExe
    }
}

# Delete the artifacts directory
Function Remove-Artifacts {
    param(
        [string]$RepoRoot
    )

    $artifactsDir = Join-Path $RepoRoot "artifacts"

    if (Test-Path $artifactsDir) {
        Remove-Item $artifactsDir -Force -Recurse
    }
}

# Invoke dotnet exe
Function Invoke-DotnetExe {
    param(
        [string]$RepoRoot,
        [string[]]$Arguments
    )

    $dotnetExe = Get-DotnetCLIExe $RepoRoot
    $command = "$dotnetExe $Arguments"

    Write-Host "[Exec]" $command -ForegroundColor Cyan

    & $dotnetExe $Arguments

    if (-not $?) {
        Write-Error $command
        exit 1
    }
}

# Invoke dotnet msbuild
Function Invoke-DotnetMSBuild {
    param(
        [string]$RepoRoot,
        [string[]]$Arguments
    )

    $buildArgs = , "msbuild"
    $buildArgs += "/nologo"
    $buildArgs += "/v:m"
    $buildArgs += "/nr:false"
    $buildArgs += "/m:1"
    $buildArgs += $Arguments

    Invoke-DotnetExe $RepoRoot $buildArgs
}

Function Install-CommonBuildTools {
    param(
        [string]$RepoRoot
    )

    Install-DotnetCLI $RepoRoot
    Install-NuGetExe $RepoRoot
}