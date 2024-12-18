# Define the path to the Function App project
$funcAppPath = Join-Path -Path $PSScriptRoot -ChildPath "../App"

# Define paths for bin and obj folders
$binPath = Join-Path -Path $funcAppPath -ChildPath "bin"
$objPath = Join-Path -Path $funcAppPath -ChildPath "obj"

# Function to safely delete a directory
function SafeDeleteDir($path) {
    if (Test-Path -Path $path) {
        Write-Host "Deleting: $path"
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Write-Host "Path not found: $path"
    }
}

# Check if Azure Functions Core Tools is installed
if (-not (Get-Command "func" -ErrorAction SilentlyContinue)) {
    Write-Error "Azure Functions Core Tools (func) is not installed. Please install it first."
    exit 1
}

# Delete bin and obj directories
Write-Host "Cleaning up 'bin' and 'obj' directories..."
SafeDeleteDir $binPath
SafeDeleteDir $objPath

# Start the Function App without changing the current directory
Write-Host "Starting Function App..."
func start --prefix-output --script-root $funcAppPath
