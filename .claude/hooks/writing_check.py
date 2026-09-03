import json, os, re, subprocess, sys

CODE_EXT = {".cs", ".shader", ".hlsl", ".cginc", ".py"}
PROSE_EXT = {".md", ".txt"}

BANNED = r"\b(actually|additionally|moreover|furthermore|delve|leverage|robust|seamless|comprehensive|crucial|essential|vital|notably|showcasing|testament|landscape|holistic|myriad|nuanced|pivotal|underscore)\b"
FILLER = r"(in order to|due to the fact|it is important to note|it'?s worth noting|plays a (crucial|key|vital) role|serves as|boasts)"
QUALIFIER = r"\b(could potentially|may possibly|might potentially|can potentially)\b"
NOT_BUT = r"(not (just|only) .{1,60}? but|isn'?t about .{1,60}? it'?s)"
DEAD_CODE = r"^\s*//\s*(if|for|while|foreach|var|public|private|protected|return|Debug\.|using |\{|\})"
SEPARATOR = r"^\s*(//\s*[-=*_]{3,}|#region|#endregion)"
TODO = r"\b(TODO|FIXME|XXX|HACK)\b"


def git(args, cwd):
    try:
        return subprocess.run(["git"] + args, cwd=cwd, capture_output=True,
                              text=True, timeout=10)
    except Exception:
        return None


def added_lines(path):
    """Return (lines_to_check, is_new_file). A tracked file with no pending
    diff contributed nothing, so it is never re-scanned for pre-existing text."""
    d = os.path.dirname(path) or "."
    tracked = git(["ls-files", "--error-unmatch", "--", path], d)
    if tracked is not None and tracked.returncode == 0:
        r = git(["diff", "-U0", "--no-color", "--", path], d)
        out = r.stdout if r else ""
        return ([l[1:] for l in out.splitlines()
                 if l.startswith("+") and not l.startswith("+++")], False)
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return (f.read().splitlines(), True)
    except OSError:
        return ([], False)


def check(path, lines, is_new):
    ext = os.path.splitext(path)[1].lower()
    hits = []

    def flag(label, pattern, subset=None, flags=re.I):
        n = sum(1 for l in (subset if subset is not None else lines)
                if re.search(pattern, l, flags))
        if n:
            hits.append("%s x%d" % (label, n))

    if ext in CODE_EXT:
        comments = [l for l in lines if re.match(r"^\s*//(?!/)", l)]
        if comments:
            hits.append("inline // comments x%d" % len(comments))
        doc = [l for l in lines if re.match(r"^\s*///", l)]
        if len(doc) > 3:
            hits.append("/// lines x%d (cap 3, class summary only)" % len(doc))
        flag("commented-out code", DEAD_CODE, comments, 0)
        flag("separator/region comment", SEPARATOR, lines, 0)
        flag("TODO marker", TODO, lines, 0)

    if ext in PROSE_EXT or ext in CODE_EXT:
        flag("banned word", BANNED)
        flag("filler phrase", FILLER)
        flag("stacked qualifier", QUALIFIER)
        flag("'not X but Y'", NOT_BUT)

    if ext in PROSE_EXT:
        em = sum(l.count("—") for l in lines)
        if em > 2:
            hits.append("em dashes x%d" % em)
        curly = sum(l.count(c) for l in lines for c in "‘’“”")
        if curly:
            hits.append("curly quotes x%d" % curly)

    if is_new and re.match(r"^(Test|Demo|Example|Sample)[A-Z_]",
                           os.path.basename(path)):
        hits.append("scaffolding-looking filename")

    return hits


def main():
    try:
        data = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
    except Exception:
        return
    ti = data.get("tool_input") or {}
    tr = data.get("tool_response") or {}
    path = tr.get("filePath") or ti.get("file_path")
    if not path or not os.path.isfile(path):
        return

    lines, is_new = added_lines(path)
    hits = check(path, lines, is_new)
    if not hits:
        return

    msg = ("Writing check on %s: %s. Re-read CLAUDE.md style rules. "
           "Remove anything that is not load-bearing, then confirm what you cut. "
           "Keep a comment only if it explains something the code cannot."
           % (os.path.basename(path), "; ".join(hits)))
    out = json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": msg}})
    sys.stdout.buffer.write(out.encode("utf-8"))


main()
