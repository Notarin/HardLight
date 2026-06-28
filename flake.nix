{
  description = "Development environment for Space Station 14";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/release-25.11";
  inputs.flake-utils.url = "github:numtide/flake-utils";

  outputs = {
    self,
    nixpkgs,
    flake-utils,
    ...
  }:
    flake-utils.lib.eachDefaultSystem (system: let
      pkgs = nixpkgs.legacyPackages.${system};
    in {
      devShells.default = import ./shell.nix {inherit pkgs;};
      formatter = pkgs.callPackage ./formatter.nix {};
      checks.formatting = pkgs.callPackage ./formatter.nix {checkDir = self;};
    });
}
