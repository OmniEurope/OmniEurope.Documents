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