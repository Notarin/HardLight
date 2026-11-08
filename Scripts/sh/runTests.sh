#!/usr/bin/env sh
cd ../../

dotnet test Content.Tests/Content.Tests.csproj -c DebugOpt -- NUnit.ConsoleOut=0

echo "Tests complete. Press enter to continue."
read
