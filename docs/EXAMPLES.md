# maash examples

Concrete input/output examples using the `echo` fixture module
(`modules/echo.json`): it echoes its operand back, and upper-cases the
operand for its `+` and `+[` suffixes.

## maash eval

`maash eval <operator> [args...]` invokes one operator directly; the args
are joined with spaces into a single operand.  The module must already be
loaded into maash state (`maash ctl use`, or a script header):

    $ maash ctl use -f modules/echo.json
    loaded: echo  (infix '')

    $ maash eval '@@' 'hello world'
    hello world
    $ maash eval '@@+' 'shout now'
    SHOUT NOW
    $ maash eval '@@[' 'hi there'
    hi there
    $ maash eval '@@+[' 'bracket shout'
    BRACKET SHOUT

## A maash script

A script's header (before the `---` line) loads modules; the body is plain
bash with maash operators in it:

    # example.maash
    use echo -f modules/echo.json
    ---
    echo "a=$( @@ hello world) b=$( @@+ SHOUT NOW)"
    echo "bracketed=$( @@[hi there]@@) end"

    $ maash example.maash
    a=hello world b=SHOUT NOW
    bracketed=hi there end

## preprocess and render

`maash preprocess` rewrites operators into `maash eval` invocations without
executing them; `maash render` fully evaluates them and splices the results
in as quoted literals:

    $ echo 'x=$( @@ hello world)' | maash preprocess
    x=$( maash eval '@@' 'hello world')
    $ echo 'x=$( @@[hi there]@@)' | maash render
    x='hi there'
