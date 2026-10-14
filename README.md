# maash
language-agnostic shell preprocessor

## Naming

Macro At-At Shell (or Maash Aint A Shell)

## Purpose

This is a convenience tool for invoking better scripting languages from within
bash. If a user has a bash script and wants a minimal incremental drop-in use of
another language they know well, this provides a convenient way to mix that in.
The goal of maash is that it will be 100% just bash unless a maash command is
used.

It uses @@ prefixes to invoke other programming languages within bash.  It is
intended to be language agnostic about what those other languages are.

## Interfaces into maash

Maash will have 3 main ways to use it in scripting:
1) The maash program.  A binary with many subcommands that can execute
   shell-scripts with a maash shebang.
2) The maash hotkey.  A standard hotkey ctrl-alt-M that takes the current input
   bash text buffer and runs the mash preprocessor on it, set using the normal
   "bind" command in bash.
3) The maash function "@@" - this is a convenience tool for using maash eval
   without using the maash preprocessor.

## The maash program
When called with no subcommands, the maash program is for running script files.
Interactive support is low-prioirity.  If included, that will be minimum viable
product: Read from stdin, process through the maash processor, then send the
output to an active bash session.  If used as a shebang in a script, the opening
lines of the script will be handled as maactl subcommands without the leading
"maactl" up to a "^---$" break.

### maash subcommands

- `maash ctl [subcommands]` AKA **maactl** - An alias for 'maash ctl'.  It's a
  meta-command full of subcommands to control the maash system.
- `maash preprocess [-f|--file file-path]` Preprocesses the given file and
  returns it as text without executing it in bash.  If run with no argument at
  all it preprocesses stdin.  If run with an argument and not the -f argument,
  it preprocesses the given text body of the argument.
- `maash eval [maash-command] [[argv]]` invokes a maash command without using
  the macro system.
- `maash init` intended to be run in the caller scope ie in their .bashrc, run
  with `eval "$(maash init)"`, handles creating the maactl alias and the @@
  function and any other in-bash customization needed.

## maash modules

A maash module registers one or more maash commands, all of which start with @@,
then an infix (which is overridable when a module is loaded), then a suffix
(which is per-command).  When a module is loaded, an interpreter for the
language is spawned and its state persists until the module is removed.  This
allows maash commands to use variables across the script.  maash commands must
also be able to share the environment variables with the parent bash
context/scope as seamlessly as possible, defining environment variables within
the maash commands script and having them appear in the encapsulating bash, and
vice versa.  These will not be the same as standard native variables, but will
be stored in a language-appropriate collection (for example the `$env:` variable
scope in pwsh).

## maactl

maactl can be used both interactively *and* in script.  However, in script the
maactl subcommands should be in the header of the file (before the `---`),
without the maactl/maash ctl prefix.  Using maactl subcommands within the body
of the script will mean they're only being used for the context of the @@/`maash
eval` function, not the preprocessor.  A warning should be written to stderr
when this happens.

### maactl subcommands

- `maactl use [module-name]` - uses a language profile module from
  /usr/local/share/maash.  Modules are loaded by name, which is filename without
  extension.  This enables an interpreter with an ongoing context and registers
  its maash commands to send things to that interpreter.  The argument is the
  interpreter name which is the extensionless path relative to the module search
  path.  So a module share/maash/custom/fsharp3 would be loaded as `maactl use
  custom/fsharp3`.  The "custom/" is part of module name.
  - arg -f [file-path] --file path to a module file
  - arg -i [jsons-tring] --inline declare a module json right here
  - arg -a [alias-string] --alias sets the module-alias, defaults to full
    use-name *or* filename if loaded from file.  Error on collision.
  - arg -x [infix-char] --infix adds a custom infix to the maash-command to
    avoid collisions.
  - arg -p --primary makes it primary -If have only 1 maactl module loaded it is
    inferred primary -If you load a 2nd module and your existing module is
    inferred primary
      - WARN that primary is ambiguous
      - Remove primary flag
  - Any collisions on module-aliases or fully-resolved maash-commands are errors
    *except* that the primary module can be shadowed.  If a primary command is
    shadowed, the user can still access it through the infix.
    - eg: 2 modules, c (infix c) and c# (infix #), where c is primary, and the c
      module registers # suffix as a command for preprocessor directives, that
      would collide with C#'s primary command which is registered at @@#, but
      the c preprocessor command could be accessed through @@c# so it's okay if
      the @@c# is clobbered by dotnet (should WARN through stderr though unless
      some kind of allowclobber param is provided in the `use`).
- `maactl load [file-path]` - is an alias of `maactl use -f [file-path]`
  - Same params as "use"
- `maactl remove [module-alias]` - removes something imported by maactl use
- `maactl help` [module-alias|module-name] - get help about a module.
  - arg -f [file-path] --file to get help on a module by module file.
- `maactl clean` - remove all imported modules

`/usr/local/share/maash` contains your installed maash plugins.  These are a
universal namespace so users should avoid customizing them.  For userland
plugins they go in `~/.local/share/maash`.

### maash commands

In maash, all commands are prefixed with `@@`.  The maash module can register as
many suffixes to @@ as it likes.  The `@` suffix is special, it's the "primary
command" which also used when no suffix is provided.  When maash sees a @@ in
the line (or in the script, if run in script mode) it checks for any maash
commands greedily (so longest-command-wins).  Maash commands and their argument
are then sent onto their interpreter of the module's choice. It is the module's
responsibility to provide not just output, but also respond with the length of
the parsed argument so that maash can know what range of the bash string to
replace.  If the module does not respond with a length, maash will error unless
run in bracketed mode, in which the maash command parameter ends with `]@@`.

Maash commands will generally accept exactly one argument in script-file mode,
although the `maash eval` and `maash preprocess` subcommands will accept an
array of arguments and concatenate them together as a single argument to the
underlying module.

So, for a C#-based maash-command with the infix `#`

```
@@#"hello, world".ToUpper()
```
Or in bracketed mode
```
@@#["hello, world".ToUpper()]@@
```

A module can register multiple commands.  By convention, braced mode can be
provided by a module @@#{var myVar = "hello, world"; return myvar;} - it might
be difficult for maash itself to detect that, but a smart interpreter can offer
that.  A module can define whatever subdialects they like within their commands,
that's just part of the existing command.

#### The Primary Module

The "primary" module gets to use the unprefixed @@.  Every other module must
live on their infix, with their individual command suffixes after that infix.
So if C# defined @@ for expressions that return and @@~ for things that don't,
and it was loaded as non-primary with the # infix, then the commands would
become @@# and @@#~.

#### Alphanumeric infixes

Because alphanumeric infixes are prone to confusion, if in infix is alphanumeric
the primary command does not get to use the empty suffix.  So if python is
loaded on infix "py" and it's not primary, then its primary command goes to @@
to @@py@.  This behavior can be suppressed with an argument in `use` when the
module is loaded.

#### Optional Whitespace

The maash command may be followed by whitespace before its argument.  By
convention the maash commands will *not* be invoked with space, but there are
places where it may be needed for clarity.

#### Maash command argument

The maash command argument can span multiple lines.

#### Maash command types
A maash command can return an array of strings or can return void (not
necessarily literally, there may be a metadata-object returned, but what differs
is the value content).  The point is that it's known by the user whether this
command will return values or not.

## The @@ function (implemented as a normal bash function)

A simple executable wrapper for `maash eval`, if somebody doesn't want to use
the maash plugin they can use `@@ # 'runMyFunction()'`

## C# maash module command ideas
- @@ - eval and return string result (if any)
  - do we need to distinguish between commands that return string and those that
    don't?  Like `@@` and `@@&`?  Probably.
  - with 
- @@? - evals and returns some truthy/falsey output, intended for use with `if
  @@?someFunction(); then`
- @@$ - eval and return the result unescaped so it can be executed as bash
- blocks?(see blocks enhancement)
  - @@if{ @@}
  - @@using{ @@}
  - @@foreach{ @@}

## Implementation

Since C# is my main language, a modern dotnet 10 statically-compiled C# "Native
AOT" exe seems the way to go.

## Future Enhancements

### More languages
pwsh and node are obvious choices.

### Blocks?

As a future enhancement, there will need to be some facility for nesting bash
within maash, possibly repeatedly, for things like the C# razor template
@foreach functionality.

Block operators that take bash content will be tricky.  Probably the block bash
content would get passed to the command as N parameters, each separator would
have its own token (like "@@} else {") and "@@}". And the maash preprocessor
would need to also work on the output of that inner block.

When to escape things would also be tricky, as well as when to recurse, since
if/else would be expected to contain and yield unescaped bash code that will be
re-evaluated by the maash preprocessor.

### Alternate shells?

Support for zsh?
