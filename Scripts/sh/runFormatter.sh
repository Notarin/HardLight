#!/usr/bin/env sh
cd ../../

dotnet format --verify-no-changes --severity warn --exclude RobustToolbox

echo "Formatter complete. Press enter to continue."
read
