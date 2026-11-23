# build.ps1
#
# PURPOSE:
#   Builds DemaConsulting.CanvasPilot.slnx in Release configuration and runs all tests
#   (xunit.v3 3.2.2 + Avalonia.Headless.XUnit): TRX results are written to artifacts/tests.
#   The WiX installer project is not part of the solution and is built by the CI pipeline.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run (for example
#   -Project <name> in a multi-library repository). Slow or environment-dependent test tiers
#   (GUI automation, integration) may be opt-in switches.
#   Run with no arguments it must still build and run the standard test suite (agents rely
#   on that), and never remove or weaken a test step to make it pass.
#
# LAUNCH SMOKE TEST:
#   This script does not publish. To check that the published program starts (a crash at the start is
#   invisible to the headless tests), publish win-x64 as the CI job does and run ./smoke.ps1 (see its header).

$buildError = $false

Write-Host "Restoring dependencies..."
dotnet restore
if ($LASTEXITCODE -ne 0) { $buildError = $true }

Write-Host "Building..."
dotnet build --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { $buildError = $true }

Write-Host "Running tests..."
dotnet test --no-build --configuration Release --logger trx --results-directory artifacts/tests
if ($LASTEXITCODE -ne 0) { $buildError = $true }

# [PROJECT-SPECIFIC] Add additional build steps here (e.g., packaging, publishing).

exit ($buildError ? 1 : 0)
