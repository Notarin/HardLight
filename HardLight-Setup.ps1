# Check for existing SSH key pair
$sshDir = Join-Path $env:USERPROFILE ".ssh"
if (-not (Test-Path $sshDir)) {
    New-Item -ItemType Directory -Path $sshDir -Force | Out-Null
}

$pubKeyFile = Get-ChildItem -Path $sshDir -Filter "*.pub" | Select-Object -First 1

if ($null -eq $pubKeyFile) {
    Write-Host "No SSH key pair found. Generating a default standard key pair automatically..." -ForegroundColor Yellow
    # Create default standard rsa key with an empty passphrase cleanly without interaction
    $keyPath = Join-Path $sshDir "id_rsa"
    & ssh-keygen -t rsa -b 4096 -f $keyPath -N "" | Out-Null
    $pubKeyFile = Get-ChildItem -Path $sshDir -Filter "*.pub" | Select-Object -First 1
}

# Print the public key to the console for the user to copy and paste into Gerrit
$pubKeyContent = Get-Content -Raw -Path $pubKeyFile.FullName
Write-Host "`n======================= YOUR SSH PUBLIC KEY =======================" -ForegroundColor Cyan
Write-Host $pubKeyContent.Trim() -ForegroundColor White
Write-Host "===================================================================`n" -ForegroundColor Cyan

# Print instructions for the user to add the key to Gerrit
Write-Host "INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "1. Highlight and copy the SSH public key above. (Only the key, not the bar above or below it.)"
Write-Host "2. Navigate to your Gerrit account preferences dashboard:"
Write-Host "https://hl.squishcat.net/settings/#SSHKeys" -ForegroundColor Green
Write-Host "3. Paste the key into the field, and hit 'Add New SSH Key'.`n"

Read-Host "Press [ENTER] once you have added your SSH key to the website to continue (or if you've already done so)..."

# Grab username for the SSH URL
Write-Host ""
$ss14Uname = (Read-Host "Enter your SS14 / HardLight Username (Case sensitive)").Trim()

if ([string]::IsNullOrEmpty($ss14Uname)) {
    Write-Error "Username string cannot be empty. Aborting workflow setup."
    exit
}

$targetSshUrl = "ssh://$($ss14Uname)@hl.squishcat.net:29418/HardLight"

# Are we in a HardLight repo already? If so, just update the remote URL. If not, clone the repo fresh.
if ((Split-Path (Get-Location) -Leaf) -eq "HardLight") {
    if (Test-Path ".git") {
        Write-Host "`nDetected existing HardLight repository. Updating origin..." -ForegroundColor Cyan
        & git remote set-url origin $targetSshUrl
        & git switch master 2>$null
        & git pull origin master
    } else {
        & git init -b master
        & git remote add origin $targetSshUrl
        & git pull origin master
    }
} else {
    Write-Host "`nCloning HardLight repository..." -ForegroundColor Green
    & git clone $targetSshUrl

    if (Test-Path "HardLight") {
        Set-Location "HardLight"
    } else {
        Write-Error "Git clone failed. ;-; Please tell us what happened!"
        exit
    }
}

# Add hook
Write-Host "`nDownloading hook..." -ForegroundColor Green
$gitDir = & git rev-parse --git-dir
$hookPath = Join-Path $gitDir "hooks"

if (-not (Test-Path $hookPath)) {
    New-Item -ItemType Directory -Path $hookPath -Force | Out-Null
}

$hookFile = Join-Path $hookPath "commit-msg"
Invoke-WebRequest -Uri "https://hl.squishcat.net/tools/hooks/commit-msg" -OutFile $hookFile

Write-Host "Making it so push works correctly..." -ForegroundColor Green
& git config remote.origin.push HEAD:refs/for/master

Write-Host "`n🎉 Ready!!! You are good to go! Make a branch, changes, and commit like normal, then push! 🎉" -ForegroundColor Green
Read-Host "Press [ENTER] to exit the setup script..."
