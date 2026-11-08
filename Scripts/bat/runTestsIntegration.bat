cd ..\..\

set "DOTNET_gcServer=1"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed

pause
