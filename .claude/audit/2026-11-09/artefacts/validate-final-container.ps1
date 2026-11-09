$ErrorActionPreference = 'Stop'
$repository = (Get-Location).Path
$auditArtifacts = Join-Path $repository '.claude/audit/2026-11-09/artefacts'
$output = Join-Path $auditArtifacts 'metrics/final-suite'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$packages = foreach ($project in @('OmniEurope.Documents.Tests','OmniEurope.Documents.StressTests')) {
    $assets = Get-Content -LiteralPath (Join-Path $repository "tests/$project/obj/project.assets.json") -Raw | ConvertFrom-Json
    $assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { $_.Name.ToLowerInvariant() }
}
[IO.File]::WriteAllText((Join-Path $output 'packages.txt'), ((@($packages | Sort-Object -Unique) -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
$shell = @'
set -eu
mkdir -p /workspace /packages
git -c safe.directory=/repository -C /repository archive 2594b1d7cd6edceedd330e2e72377fe3b7c989af | tar -x -C /workspace
mkdir -p /workspace/scripts
cp /repository/ylaunch.ps1 /workspace/ylaunch.ps1
cp /repository/scripts/ylaunch-core.ps1 /workspace/scripts/ylaunch-core.ps1
if [ -f /repository/.config/crap-exceptions.json ]; then
    mkdir -p /workspace/.config
    cp /repository/.config/crap-exceptions.json /workspace/.config/crap-exceptions.json
fi
while IFS= read -r item; do
    [ -n "$item" ] || continue
    parent=${item%/*}
    if [ -d "/cache/$item" ]; then
        mkdir -p "/packages/$parent"
        cp -a "/cache/$item" "/packages/$parent/"
    fi
done < /evidence/packages.txt
export NUGET_PACKAGES=/packages
export GENERIC_KIT=/kit
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cd /workspace
cp /evidence/run-ci-suite.sh /workspace/run-ci-suite.sh
set +e
timeout 900s sh /workspace/run-ci-suite.sh > /evidence/ci-suite.log 2>&1
result=$?
set -e
printf '%s\n' "$result" > /evidence/exit-code.txt
if [ -d artifacts/tests ]; then cp -a artifacts/tests /evidence/unit; fi
if [ -d artifacts/integration ]; then cp -a artifacts/integration /evidence/stress; fi
exit "$result"
'@
[IO.File]::WriteAllText((Join-Path $output 'run-final-suite.sh'), $shell.Replace("`r`n","`n"), [Text.UTF8Encoding]::new($false))
$ciShell = @'
set -eu
dotnet restore OmniEurope.Documents.slnx > /evidence/restore.log 2>&1
dotnet build OmniEurope.Documents.slnx --configuration Release --no-restore -p:ContinuousIntegrationBuild=true > /evidence/build.log 2>&1
set +e
dotnet test --project tests/OmniEurope.Documents.Tests/OmniEurope.Documents.Tests.csproj --configuration Release --no-build --no-progress --report-trx --report-trx-filename tests.trx --results-directory artifacts/tests --coverlet --coverlet-output-format cobertura --coverlet-include '[OmniEurope.Documents]*' '[OmniEurope.Documents.Fonts]*' --coverlet-exclude-by-attribute GeneratedCodeAttribute ExcludeFromCodeCoverageAttribute > /evidence/unit.log 2>&1 &
unit_pid=$!
dotnet test --project tests/OmniEurope.Documents.StressTests/OmniEurope.Documents.StressTests.csproj --configuration Release --no-build --no-progress --report-trx --report-trx-filename integration.trx --results-directory artifacts/integration > /evidence/stress.log 2>&1 &
stress_pid=$!
wait "$unit_pid"
unit_result=$?
wait "$stress_pid"
stress_result=$?
set -e
printf '%s\n' "$unit_result" > /evidence/unit-exit-code.txt
printf '%s\n' "$stress_result" > /evidence/stress-exit-code.txt
pwsh -NoProfile -File .github/scripts/verify-trx.ps1 -Directory artifacts/tests -Name tests.trx > /evidence/unit-trx.log 2>&1
pwsh -NoProfile -File .github/scripts/verify-trx.ps1 -Directory artifacts/integration -Name integration.trx > /evidence/stress-trx.log 2>&1
if [ "$unit_result" -ne 0 ]; then exit "$unit_result"; fi
if [ "$stress_result" -ne 0 ]; then exit "$stress_result"; fi
pwsh -NoProfile -File scripts/crap-gate.ps1 -CoverageRoot artifacts/tests -ReportPath /evidence/crap-scores.csv > /evidence/crap-gate.log 2>&1
'@
[IO.File]::WriteAllText((Join-Path $output 'run-ci-suite.sh'), $ciShell.Replace("`r`n","`n"), [Text.UTF8Encoding]::new($false))
$arguments = @('run','--rm','--name','oe-documents-audit-20261109','--cpus','4','--memory','4g',
    '--mount',"type=bind,source=$repository,target=/repository,readonly",
    '--mount','type=bind,source=C:/Users/Woluwe/.nuget/packages,target=/cache,readonly',
    '--mount','type=bind,source=C:/Dev/_Generic,target=/kit,readonly',
    '--mount',"type=bind,source=$output,target=/evidence",
    '--entrypoint','sh','sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b','/evidence/run-final-suite.sh')
& docker @arguments
exit $LASTEXITCODE
