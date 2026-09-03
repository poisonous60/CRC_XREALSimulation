# CLAUDE.md - CRC_XREALSimulation (RadVis)

## Docs

Read before working. Located in `e:\260618\HCI개론\3d effect test\docs\`.

- `crc-xreal-current-state.md` : entry point. What is already done.
- `crc-xreal-overview.md` : structure, data flow, user workflow.
- `crc-xreal-authored-assets.md` : what was hand-written vs generated.
- `crc-xreal-commit-history.md` : all 35 commits.
- `crc-xreal-hardware-and-editor-testing.md` : hardware support, editor limits.

## How to answer

Reply in Korean. Write every file, comment, and commit message in English.

- This is a handover for learning. Point at the concept, the official doc, and the
  order to practice in. Do not hand over finished scripts.
- Do not guess. Cite real files and code with `file:line`.
- Verify API and engine behavior against official docs instead of recalling it.
- Separate confirmed fact, inference, and unverifiable. Never call something working
  that you did not run.
- Define a term the first time it appears. Name the concrete thing (class, file,
  type) before describing it.

## Working rules

- No Play mode. Set the scene up and stop. The user presses Play.
- Verify with `unity-cli console --type error` only. On unexpected output, report one
  line and stop.
- The user commits and pushes. Do not do it unasked.
- Move assets with `AssetDatabase.MoveAsset` so the guid survives.

## Code style

Taken from the existing scripts. Match it.

- No `public` fields. Inspector values are `[SerializeField] private`; expose state as
  `public bool IsConnected => ...`.
- Group serialized fields under `[Header]`, explain each with `[Tooltip]`, constrain
  with `[SerializeField, Min(0f)]`.
- Collection fields are `private readonly`. String keys use
  `StringComparer.OrdinalIgnoreCase`.
- Lookups that can fail return `bool TryXxx(out ...)`. Do not drive flow with exceptions.
- Log as `Debug.LogWarning($"[ClassName] ...")`.
- No `var`. Spell out the type. The 11,878 existing lines contain 7.
- Wire components with `static event`, not Inspector references. Subscribe in
  `OnEnable`, unsubscribe in `OnDisable`.
- Input System only. Legacy `Input.GetAxis` does not compile here.
- Not URP. Shaders are `CGPROGRAM` plus `UnityCG.cginc`.
- Comments and tests default to none. The exceptions are a 1 to 3 line `///` summary
  above a component class, and a `//` that records a reason the code cannot show.
  Never add unrequested tests, demos, or scaffolding.

A PostToolUse hook at `.claude/hooks/writing_check.py` scans each write and reports
added comments, scaffolding, and padded prose. Act on what it reports.
