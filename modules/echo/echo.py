#!/usr/bin/env python3
"""maash echo module (fixture) implementing the M1 wire protocol v2.

Request (stdin):
  M1 <command> <opString> <operandByteLen>\n<operand bytes>
  where command is "parseLength" or "render".

Response (stdout):
  M1 <parsedCharLen> <outputByteLen>\n<output bytes>

Example (empty-infix 'echo' module):
  @@[text]@@     => text     (suffix '[' : here-bracketed echo)
  @@+[text]@@   => TEXT     (suffix '+[' : here-bracketed upper)
  @@text@@      => text     (primary, empty suffix)
  @@+text@@     => TEXT     (suffix '+' : upper)

Dispatch (by trailing character of the operator string):
  '+'/'x'/'+['  -> UPPER (upper-cases the operand)
  otherwise     -> ECHO (returns operand as-is)

Operand extent: for here-bracketed operators (opString ends in '[') maash
has already scoped the operand, so the module consumes all of it. For
non-here-bracketed operators the module owns the extent: it consumes up to
the first unbalanced ')' (the enclosing $( ) closer) or the next unescaped
'@@', whichever comes first.
"""
import sys


def scan_extent(operand):
    """Number of chars of the operand the module consumes: up to the first
    unbalanced ')' (the enclosing $( ) closer) or the next unescaped '@@'."""
    depth = 0
    for i, ch in enumerate(operand):
        if ch == '(':
            depth += 1
        elif ch == ')':
            if depth == 0:
                return i
            depth -= 1
        elif ch == '@' and i + 1 < len(operand) and operand[i + 1] == '@' and (i == 0 or operand[i - 1] != '\\'):
            return i
    return len(operand)


def main():
    # Read exactly one M1 request: header + <operandLen> bytes.
    # We buffer via raw reads to avoid line-buffering edge cases and to ensure
    # all <operandLen> payload bytes arrive even without a trailing newline.
    data = b""
    while b"\n" not in data:
        chunk = sys.stdin.buffer.read(1024)
        if not chunk:
            break
        data += chunk
    header, _, rest = data.partition(b"\n")
    try:
        parts = header.decode("utf-8").split()
        if parts[0] != "M1":
            return 3
        command = parts[1]
        op_str = parts[2]
        blen = int(parts[3])
    except (IndexError, ValueError):
        return 3

    operand_bytes = rest[:blen]
    while len(operand_bytes) < blen:
        chunk = sys.stdin.buffer.read(4096)
        if not chunk:
            break
        operand_bytes += chunk

    operand = operand_bytes.decode("utf-8", "replace")

    # Parsed length: number of chars of the RAW operand the module consumed
    # (leading whitespace included; that's the range maash must replace).
    if op_str.endswith("["):
        # Here-bracketed op: maash already scoped the operand; consume all.
        n = len(operand)
    else:
        n = scan_extent(operand)
    consumed = operand[:n]

    if command == "parseLength":
        sys.stdout.buffer.write(b"M1 %d 0\n" % n)
        sys.stdout.buffer.flush()
        return 0

    # render: dispatch on the operator's trailing character.
    op_text = consumed.lstrip(" ")
    if op_str.endswith("+") or op_str.endswith("x") or op_str.endswith("+["):
        out = op_text.upper()
    else:
        out = op_text
    out_bytes = out.encode("utf-8")
    sys.stdout.buffer.write(b"M1 %d %d\n" % (n, len(out_bytes)))
    sys.stdout.buffer.write(out_bytes)
    sys.stdout.buffer.flush()
    return 0


if __name__ == "__main__":
    sys.exit(main())
