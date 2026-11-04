# TODO

## M1: initial implementation (PR #1)

- [x] Core preprocessor + `maash eval` wrapper (M1 process protocol)
- [x] `maactl` (module load/move/remove/clean) with persisted session state
- [x] `maash render` (hotkey-eval form) + bash init snippet (Ctrl+Alt+M)
- [x] Adopt dotnet-repo-template conventions (AGENTS/CONTRIB/.editorconfig/.gitignore/LICENSE)
- [x] Migrate `maash.sln` to `maash.slnx`
- [x] Here-bracketed operators: explicit `hereBracketed` flag in module JSON, closer is `]@@`
- [x] Module `defaultInfix` (echo declares `""`)
- [x] Module bodies in subdirs (only `*.json` in `modules/` root)
- [x] M1 protocol v2: `parseLength` + `render` commands; maash is "dumb" about
  module-scoped operand extents (modules report `parsedLength`; no paren-balancing in maash)

## Future

- [ ] Header directives via System.CommandLine: parse `#use`-style header lines with a sub-RootCommand mirroring the ctl subcommands (deferred from PR#1 third review, item 5).
