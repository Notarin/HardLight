# Running this build (Drone trait)

## 1. Get the code

```sh
git clone https://code.hardlight.space/Jayty/HardLightJay.git hardlight
cd hardlight
git checkout drone-trait
git submodule update --init --recursive
```

## 2. Install the .NET 10 SDK

`global.json` pins SDK `10.0.100` (roll-forward to the latest 10.0.x feature band).

```sh
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

On Windows use the .NET 10 SDK installer from https://dotnet.microsoft.com/download/dotnet/10.0.

## 3. Build

```sh
dotnet build Content.Server/Content.Server.csproj
dotnet build Content.Client/Content.Client.csproj
```

## 4. Run

Server (first terminal):

```sh
dotnet run --project Content.Server -- --cvar net.port=1212 --cvar auth.mode=0
```

Client (second terminal):

```sh
dotnet run --project Content.Client
```

In the client, connect to `localhost:1212`. `auth.mode=0` disables account auth so any username works.

## 5. Give yourself admin

In the server console:

```
promotehost localhost@<your username>
```

That unlocks the admin menu (spawning, VV, `addcomp`) for inspecting the trait in-game.

## 6. Try the trait

Pick **Drone** in character setup under the **Lewd** trait category, then join a round:

- The internals action works with no mask and no gas tank; the internals alert works too.
- The reservoir is filled with oxygen, or nitrogen if you are playing a Vox or Slime.
- With internals switched off the reservoir refills itself (~5 minutes from empty).
- Eating and drinking are blocked.
- Walking occasionally makes you stumble and fall; standing back up takes ~4 seconds, and there
  is a 45 second grace period before you can stumble again.

## Troubleshooting

If the client fails to start with an OpenAL/audio device error (common on headless Linux):

```sh
ALSOFT_DRIVERS=null dotnet run --project Content.Client
```

If the client window renders incorrectly, try:

```sh
dotnet run --project Content.Client -- --cvar display.compat=true
```
