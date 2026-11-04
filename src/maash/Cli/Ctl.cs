using System.CommandLine;
using Maash.Core;
using Maash.State;

namespace Maash.Cli;

/// <summary>
/// <c>maash ctl</c> subcommand (alias: maactl): manage loaded modules, primary
/// selection, and persistence. Same subcommands work in script headers (before
/// the ^---$ break) without the "ctl" prefix.
/// </summary>
public static class Ctl {
    /// <summary>Verbs recognized as maactl subcommands (for header/body detection).</summary>
    public static readonly string[] Verbs = { "use", "move", "load", "remove", "clean", "help" };

    /// <summary>
    /// Warnings for body lines that start with a maactl verb: a script-mode
    /// maactl misuse, since body maactl lines are only honored in the
    /// @@/maash eval context, never by the preprocessor.
    /// </summary>
    public static List<string> BodyMaactlWarnings(string body) {
        var warnings = new List<string>();
        foreach (var raw in body.Split('\n')) {
            var t = raw.TrimEnd('\r').TrimStart();
            foreach (var verb in Verbs) {
                if (t == verb || t.StartsWith(verb + " ")) {
                    warnings.Add($"WARN: maactl subcommand '{verb}' in script body; only honored in the @@/maash eval context, not by the preprocessor.");
                    break;
                }
            }
        }
        return warnings;
    }

    public static Command BuildCommand() {
        var ctl = new Command("ctl", "Manage maash modules and state (alias: maactl).");

        var fileOpt = FileOpt();
        var inlineOpt = InlineOpt();
        var aliasOpt = AliasOpt();
        var infixOpt = InfixOpt();
        var primaryOpt = PrimaryOpt();
        var clobberOpt = ClobberOpt();
        var suppressAlnumOpt = SuppressAlnumOpt();

        // use [module] -f|--file, -i|--inline, -a|--alias, -x|--infix, -p|--primary, --allowclobber, --suppress-alnum-rule
        var useCmd = new Command("use", "Load a module by name (search dirs), file, or inline JSON.");
        var moduleArg = new Argument<string?>("module") {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Module name (relative to a module search dir); or -f/-i. Use '-' for the primary position."
        };
        useCmd.Add((Argument)moduleArg);
        var useOptions = new Option[] {
            fileOpt, inlineOpt, aliasOpt, infixOpt,
            primaryOpt, clobberOpt, suppressAlnumOpt,
            QuietOpt()
        };
        foreach (var option in useOptions) {
            useCmd.Add(option);
        }

        useCmd.SetAction(pr => RunUse(pr,
            pr.GetValue<string?>(moduleArg), fileOpt, inlineOpt, aliasOpt, infixOpt,
            primaryOpt, clobberOpt, suppressAlnumOpt));
        ctl.Add(useCmd);

        // move [module-alias] (accepts use params)
        var moveCmd = new Command("move", "Move an existing module to a new alias/infix/primary.");
        var moveModuleArg = new Argument<string>("module") { Description = "Existing module alias." };
        moveCmd.Add(moveModuleArg);
        foreach (var option in new Option[] { aliasOpt, infixOpt, primaryOpt, suppressAlnumOpt }) {
            moveCmd.Add(option);
        }

        moveCmd.SetAction(pr => RunMove(pr.GetRequiredValue<string>(moveModuleArg), pr,
            aliasOpt, infixOpt, primaryOpt, suppressAlnumOpt));
        ctl.Add(moveCmd);

        // load [file-path] - alias of use -f
        var loadCmd = new Command("load", "Load a module file (alias of 'use -f').");
        var loadFileArg = new Argument<string>("file") { Description = "Path to a module JSON file. Use '-' for the primary position." };
        loadCmd.Add((Argument)loadFileArg);
        foreach (var option in new Option[] { aliasOpt, infixOpt, primaryOpt, clobberOpt, suppressAlnumOpt }) {
            loadCmd.Add(option);
        }

        loadCmd.SetAction(pr => UseRun(
            pr,
            pr.GetRequiredValue<string>(loadFileArg),
            null,
            aliasOpt,
            infixOpt,
            primaryOpt,
            clobberOpt,
            suppressAlnumOpt,
            asFile: true));
        ctl.Add(loadCmd);

        // remove [alias]
        var removeCmd = new Command("remove", "Remove a loaded module by alias.");
        var removeAliasArg = new Argument<string>("alias");
        removeCmd.Add(removeAliasArg);
        var rmQuiet = new Option<bool>("-q", "--quiet");
        removeCmd.Add(rmQuiet);
        removeCmd.SetAction(pr => {
            var alias = pr.GetRequiredValue<string>(removeAliasArg);
            var reg = StateBridge.LoadRegistry();
            if (!reg.TryGetModule(alias, out _)) {
                Console.Error.WriteLine($"maash: no module with alias '{alias}'");
                return 1;
            }
            reg.Remove(alias);
            StateBridge.SaveRegistry(reg);
            if (pr.GetValue(rmQuiet)) {
                return 0;
            }

            Console.WriteLine("removed: " + alias);
            return 0;
        });
        ctl.Add(removeCmd);

        // clean
        var cleanCmd = new Command("clean", "Remove all imported modules.");
        var cleanQuiet = new Option<bool>("-q", "--quiet");
        cleanCmd.Add(cleanQuiet);
        cleanCmd.SetAction(pr => {
            StateBridge.SaveRegistry(new ModuleRegistry());
            if (pr.GetValue(cleanQuiet)) {
                return 0;
            }

            Console.WriteLine("state cleaned");
            return 0;
        });
        ctl.Add(cleanCmd);

        // help [topic] [--file module-file]
        var helpCmd = new Command("help", "Show help for a module or operator (same as maash help).");
        var helpTopicArg = new Argument<string?>("topic") { Arity = ArgumentArity.ZeroOrOne };
        helpCmd.Add(helpTopicArg);
        // -f/--file: show help for a module read directly from its file (no load needed).
        var helpFileOpt = new Option<string?>("-f", "--file") { DefaultValueFactory = _ => (string?)null };
        helpCmd.Add(helpFileOpt);
        helpCmd.SetAction(pr => {
            var helpFilePath = pr.GetValue<string?>(helpFileOpt);
            if (helpFilePath != null) {
                return Program.HelpRunFile(helpFilePath);
            }

            return Program.HelpRun(pr.GetValue<string?>(helpTopicArg));
        });
        ctl.Add(helpCmd);

        return ctl;
    }

    // ---------- option factory (shared across subcommands; one instance added per command) ----------
    static Option<string?> FileOpt() =>
        new("-f", "--file") {
            Description = "Module file path (overrides positional name).",
            DefaultValueFactory = _ => (string?)null
        };
    static Option<string?> InlineOpt() =>
        new("-i", "--inline") {
            Description = "Inline module JSON (overrides positional name).",
            DefaultValueFactory = _ => (string?)null
        };
    static Option<string?> AliasOpt() =>
        new("-a", "--alias") {
            Description = "Module alias (default: use-name or filename).",
            DefaultValueFactory = _ => (string?)null
        };
    static Option<string?> InfixOpt() =>
        new("-x", "--infix") {
            Description = "Custom infix (default: module name).",
            DefaultValueFactory = _ => (string?)null
        };
    static Option<bool> PrimaryOpt() =>
        new("-p", "--primary") {
            Description = "Mark as primary module.",
            DefaultValueFactory = _ => false
        };
    static Option<bool> ClobberOpt() =>
        new("--allowclobber") {
            Description = "Allow clobbering an existing operator registration.",
            DefaultValueFactory = _ => false
        };
    static Option<bool> SuppressAlnumOpt() =>
        new("--suppress-alnum-rule") {
            Description = "Do not force the '@' suffix on alphanumeric infixes.",
            DefaultValueFactory = _ => false
        };
    static Option<bool> QuietOpt() =>
        new("-q", "--quiet") {
            Description = "Suppress stderr echoes.",
            DefaultValueFactory = _ => false
        };

    // ---------- handlers ----------

    /// <summary>
    /// Action for 'ctl use': reads the parsed options and delegates to UseRun
    /// (asFile false).
    /// </summary>
    /// <returns>0 on success, 1 on error.</returns>
    internal static int RunUse(ParseResult parseResult,
        string? moduleArgument,
        Option<string?> fileOpt,
        Option<string?> inlineOpt,
        Option<string?> aliasOpt,
        Option<string?> infixOpt,
        Option<bool> primaryOpt,
        Option<bool> clobberOpt,
        Option<bool> suppressAlnumOpt) {
        return UseRun(
            parseResult,
            moduleArgument,
            parseResult.GetValue(fileOpt),
            aliasOpt,
            infixOpt,
            primaryOpt,
            clobberOpt,
            suppressAlnumOpt,
            asFile: false,
            inline: parseResult.GetValue(inlineOpt));
    }

    /// <summary>
    /// Shared load implementation for 'ctl use' (asFile false) and 'ctl load'
    /// (asFile true): loads a module by name, -f file, or -i inline and
    /// persists the registry.
    /// </summary>
    /// <returns>0 on success, 1 on error.</returns>
    internal static int UseRun(ParseResult parseResult,
        string? nameArg,
        string? fileArg,
        Option<string?> aliasOpt,
        Option<string?> infixOpt,
        Option<bool> primaryOpt,
        Option<bool> clobberOpt,
        Option<bool> suppressAlnumOpt,
        bool asFile = false,
        string? inline = null) {
        try {
            var alias = parseResult.GetValue(aliasOpt);
            var infix = parseResult.GetValue(infixOpt);
            var primary = parseResult.GetValue(primaryOpt);
            var allowClobber = parseResult.GetValue(clobberOpt);
            var suppressAlnum = parseResult.GetValue(suppressAlnumOpt);

            MaashModule module;
            string declaredName;
            if (inline is not null) {
                (module, declaredName) = LoadInline(inline, asFile ? nameArg : alias);
            } else if (fileArg != null) {
                (module, declaredName) = ModuleSource.LoadFromFile(fileArg);
            } else {
                var byName = asFile
                    ? (fileArg ?? nameArg)
                    : (nameArg ?? fileArg);
                if (byName is null) {
                    throw new MaashException("module name, -f file, or -i inline required");
                }

                (module, declaredName) = LoadByNameOrPath(byName);
            }

            var usedAlias = alias ?? declaredName;
            var reg = StateBridge.LoadRegistry();
            var warnings = reg.Load(module, usedAlias, infix, primary,
                allowClobber: allowClobber, suppressAlnumRule: suppressAlnum);
            StateBridge.SaveRegistry(reg);
            foreach (var warning in warnings) {
                Console.Error.WriteLine("maash: " + warning);
            }

            var actualInfix = reg.TryGetModule(usedAlias, out var loadedModule) && loadedModule is not null
                ? loadedModule.Infix
                : (infix ?? declaredName);
            Console.WriteLine("loaded: " + usedAlias + "  (infix '" + actualInfix + "')");
            return 0;
        } catch (MaashException ex) {
            Console.Error.WriteLine("maash: " + ex.Message);
            return 1;
        } catch (InvalidOperationException ex) {
            Console.Error.WriteLine("maash: " + ex.Message);
            return 1;
        }
    }

    static int RunMove(string alias, ParseResult parseResult, Option<string?> aliasOpt, Option<string?> infixOpt,
        Option<bool> primaryOpt, Option<bool> suppressAlnumOpt) {
        try {
            var reg = StateBridge.LoadRegistry();
            if (!reg.TryGetModule(alias, out var loaded) || loaded is null) {
                Console.Error.WriteLine($"maash: no module with alias '{alias}'");
                return 1;
            }
            var newAlias = parseResult.GetValue(aliasOpt);
            var newInfix = parseResult.GetValue(infixOpt);
            var newPrimary = parseResult.GetValue(primaryOpt);

            if (newAlias != null && newAlias != alias) {
                if (reg.Modules.Any(m => m.Alias == newAlias)) {
                    Console.Error.WriteLine("maash: alias '" + newAlias + "' is already in use");
                    return 1;
                }
                reg.Remove(alias);
                reg.Load(loaded.Module, newAlias, newInfix ?? loaded.Infix, newPrimary,
                    suppressAlnumRule: loaded.SuppressAlnumRule);
            } else {
                reg.Move(alias, newInfix, primary: newPrimary ? (bool?)true : null);
            }

            StateBridge.SaveRegistry(reg);
            Console.WriteLine("moved: " + (newAlias ?? alias));
            return 0;
        } catch (MaashException ex) {
            Console.Error.WriteLine("maash: " + ex.Message);
            return 1;
        } catch (InvalidOperationException ex) {
            Console.Error.WriteLine("maash: " + ex.Message);
            return 1;
        }
    }

    // ---------- module loading ----------

    static (MaashModule, string) LoadByNameOrPath(string nameOrPath)
        => LoadModulePath(nameOrPath, null);

    static (MaashModule, string) LoadModulePath(string nameOrPath, string? baseDir) {
        var path = ResolveModulePath(nameOrPath, baseDir);
        if (path.EndsWith(".json") || File.Exists(path)) {
            return ModuleSource.LoadFromFile(path);
        }

        var found = ModuleSource.FindByName(nameOrPath);
        if (found != null) {
            return ModuleSource.LoadFromFile(found);
        }

        throw new MaashException($"module not found: {nameOrPath} (search dirs: {string.Join(":", ModuleSource.SearchDirs)})");
    }

    static (MaashModule, string) LoadInline(string json, string? declaredName) {
        var module = System.Text.Json.JsonSerializer.Deserialize<MaashModule>(json, MaashJsonContext.Default.MaashModule)
            ?? throw new MaashException("invalid inline module JSON");
        var name = !string.IsNullOrEmpty(declaredName) ? declaredName : module.Name;
        if (string.IsNullOrEmpty(name)) {
            throw new MaashException("inline module must specify a name or an alias");
        }

        return (module, name);
    }

    // ---------- script header support ----------

    /// <summary>
    /// Resolve a module path given with a relative -f/--file: CWD-relative first
    /// (the user's shell convention), falling back to the script's directory
    /// (so scripts can stay portable when the file is not in the CWD).
    /// </summary>
    internal static string ResolveModulePath(string path, string? baseDir) {
        if (path.StartsWith("-")) {
            return path;
        }

        if (Path.IsPathRooted(path)) {
            return path;
        }
        // CWD-relative first.
        if (File.Exists(path)) {
            return path;
        }
        // Then script-dir-relative (portability fallback).
        if (!string.IsNullOrEmpty(baseDir)) {
            var combined = Path.Combine(baseDir, path);
            if (File.Exists(combined)) {
                return combined;
            }
        }
        return path;
    }

    /// <summary>
    /// Parse a <c>#use</c> / <c>#remove</c> header line against the registry.
    /// Verb dispatch is shared with the CLI commands; header semantics are
    /// applied on top (file path resolution, alnum-rule warning).
    /// </summary>
    public static (HeaderDirective Directive, List<string> Warnings) ApplyHeaderLine(ModuleRegistry reg, string line, bool authoritativeUse = false, string? baseDir = null) {
        var warns = new List<string>();
        var d = new HeaderDirective();
        var fields = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0) {
            return (d, warns);
        }

        d.Verb = fields[0];
        var pos = 1;
        while (pos < fields.Length) {
            var tok = fields[pos];
            if (!tok.StartsWith("-")) {
                // Positional: first one is the module name; a second is an alias
                // (useful with -f where the name is derived from the file).
                if (d.Name == null) {
                    d.Name = tok;
                } else if (d.Alias == null) {
                    d.Alias = tok;
                }

                pos++;
                continue;
            }
            string val = "";
            bool hasVal = pos + 1 < fields.Length && !fields[pos + 1].StartsWith("-");
            if (hasVal) { val = fields[pos + 1]; pos += 2; } else { pos++; }

            switch (tok) {
                case "-p": case "--primary": d.IsPrimary = true; break;
                case "--allowclobber": d.AllowClobber = true; break;
                case "--suppress-alnum-rule": d.SuppressAlnumRule = true; break;
                case "-a": case "--alias": d.Alias = val; break;
                case "-x": case "--infix": d.Infix = val; break;
                case "-q": case "--quiet": break;
                case "-f":
                case "--file":
                    d.IsFile = true;
                    if (d.Name != null && d.Alias == null && d.Name != val && val != "") {
                        d.Alias = d.Name; // 'use <name> -f <file>': name becomes the alias
                    }

                    if (val != "") {
                        d.Name = val;
                    }

                    break;
                case "-i":
                case "--inline":
                    // Inline JSON in a header is not supported (use a file or search-dir module).
                    break;
            }
        }

        switch (d.Verb) {
            case "use":
            case "load":
                if (d.Name == null) {
                    warns.Add("maactl: 'use'/'load' requires a module name or -f/--file value.");
                    break;
                }
                var (mod, _) = LoadModulePath(d.Name!, baseDir);
                var alias = d.Alias ?? d.Name;
                // In a script header a 'use' for an already-loaded alias is re-entrant:
                // the header is the authoritative declaration, so clobber the prior load.
                if (authoritativeUse && reg.TryGetModule(alias, out _)) {
                    reg.Remove(alias);
                }

                if (authoritativeUse && d.IsFile && d.Name is not null && d.Name.Contains("/") &&
                    reg.TryGetModule(System.IO.Path.GetFileNameWithoutExtension(d.Name), out var legacy)) {
                    reg.Remove(legacy!.Alias); // alias derived from the file path in a prior load
                }

                var loadWarns = reg.Load(mod, alias, d.Infix, d.IsPrimary,
                    allowClobber: d.AllowClobber, suppressAlnumRule: d.SuppressAlnumRule);
                warns.AddRange(loadWarns);
                break;
            case "move":
                // 'move <existing-alias> [-a new-alias] [-x infix] [-p]' - mirrors
                // RunMove: positional is the existing alias; -a renames.
                if (d.Name == null) {
                    warns.Add("maactl: 'move' requires an existing module alias.");
                    break;
                }
                if (!reg.TryGetModule(d.Name, out var lm) || lm is null) {
                    warns.Add($"maactl: no module with alias '{d.Name}'.");
                    break;
                }
                if (d.Alias != null && d.Alias != d.Name) {
                    if (reg.TryGetModule(d.Alias, out _)) {
                        warns.Add("maactl: alias '" + d.Alias + "' is already in use.");
                        break;
                    }
                    reg.Remove(d.Name);
                    warns.AddRange(reg.Load(lm.Module, d.Alias, d.Infix ?? lm.Infix, d.IsPrimary,
                        suppressAlnumRule: lm.SuppressAlnumRule));
                } else {
                    warns.AddRange(reg.Move(d.Name, d.Infix, primary: d.IsPrimary ? (bool?)true : null));
                }
                break;
            case "remove":
                reg.Remove(d.Alias ?? "");
                break;
            case "clean":
                reg.Clean();
                break;
            case "help":
                break;
            default:
                warns.Add("maactl: unknown verb '" + d.Verb + "'.");
                break;
        }
        return (d, warns);
    }
}
