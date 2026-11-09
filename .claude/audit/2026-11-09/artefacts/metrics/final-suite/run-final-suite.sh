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