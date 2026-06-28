{
  treefmt-nix ?
    import (builtins.fetchGit {
      url = "https://github.com/numtide/treefmt-nix";
      rev = "128222dc911b8e2e18939537bed1762b7f3a04aa";
    }),
  checkDir ? null,
  stdenv,
  lib,
  writeShellScriptBin,
  formats,
  treefmt,
  git,
  git-lfs,
  csharpier,
  alejandra,
  deadnix,
  mdformat,
  statix,
  runCommandLocal,
  yamlfmt,
}: let
  treefmt-pkgs = let
    # Restructure `python3Packages` exactly how treefmt-nix wants it
    python3Packages = {inherit mdformat;};
  in {
    inherit
      stdenv
      lib
      writeShellScriptBin
      treefmt
      formats
      git
      git-lfs
      csharpier
      alejandra
      deadnix
      python3Packages
      statix
      runCommandLocal
      yamlfmt
      ;
  };
  treefmt-settings = {
    settings = {
      allow-missing-formatter = false;
    };
    programs = {
      csharpier.enable = true;
      alejandra.enable = true;
      mdformat.enable = true;
      deadnix.enable = true;
      statix.enable = true;
      yamlfmt = {
        enable = true;
        settings.formatter = {
          indentless_arrays = true;
          retain_line_breaks_single = true;
        };
      };
    };
  };
  output = treefmt-nix.evalModule treefmt-pkgs treefmt-settings;
in
  if checkDir == null
  then output.config.build.wrapper
  else output.config.build.check checkDir
