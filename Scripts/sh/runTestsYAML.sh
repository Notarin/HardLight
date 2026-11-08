#!/usr/bin/env sh
cd ../../

dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj -c DebugOpt -- NUnit.ConsoleOut=0

echo "Tests complete. Press enter to continue."
read
