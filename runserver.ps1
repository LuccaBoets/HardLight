$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$configFile = Join-Path $PSScriptRoot 'server_config.local.toml'

$arguments = @('run', '--project', 'Content.Server')

if (Test-Path $configFile)
{
    $arguments += '--'
    $arguments += '--config-file'
    $arguments += $configFile
}

& $dotnet @arguments
Read-Host 'Press enter to continue'