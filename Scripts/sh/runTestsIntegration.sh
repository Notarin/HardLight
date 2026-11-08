#!/usr/bin/env sh
cd ../../

export DOTNET_gcServer=1
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed

echo "Tests complete. Press enter to continue."
read
