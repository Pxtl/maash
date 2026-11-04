#!/usr/bin/env python3
"""shout module (fixture): uppercases operand (twice for '#' ops).
Implements the M1 wire protocol v2; non-here-bracketed ops are
module-scoped (see scan_extent)."""
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
    data = b""
    while b"\n" not in data:
        chunk = sys.stdin.buffer.read(1024)
        if not chunk: break
        data += chunk
    header, _, rest = data.partition(b"\n")
    parts = header.decode().split()
    if not parts or parts[0] != "M1":
        return 3
    if len(parts) < 4:
        return 3
    command = parts[1]
    op = parts[2]
    blen = int(parts[3])
    operand_bytes = rest[:blen]
    while len(operand_bytes) < blen:
        chunk = sys.stdin.buffer.read(4096)
        if not chunk: break
        operand_bytes += chunk
    operand = operand_bytes.decode("utf-8", "replace")

    # Non-here-bracketed ops: the module owns the operand's extent.
    n = scan_extent(operand)
    consumed = operand[:n].strip()

    if command == "parseLength":
        sys.stdout.buffer.write(b"M1 %d 0\n" % n)
        sys.stdout.buffer.flush()
        return 0

    # render: '#' ops shout twice, others shout once.
    if op.endswith("#"):
        out = consumed.upper() + " " + consumed.upper()
    else:
        out = consumed.upper()
    b = out.encode()
    sys.stdout.buffer.write(b"M1 %d %d\n" % (n, len(b)))
    sys.stdout.buffer.write(b)
    sys.stdout.buffer.flush()
    return 0
if __name__ == "__main__":
    sys.exit(main())
