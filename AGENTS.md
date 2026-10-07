# Asset replacement preferences

## Task continuity

- Read `TASK_PROGRESS.md` at the start of each session before continuing work.
- Record new user tasks, completed milestones, validation results, blockers and the
  concrete next step in `TASK_PROGRESS.md` as work progresses, so an interrupted
  session can be resumed. Preserve unfinished tasks and distinguish verified results
  from assumptions. Do not wait until the end of the session to update it.

## Asset replacement

- When the user replaces an asset pack, remove the superseded pack and unused derived
  meshes/materials/prefabs from Unity's Assets folder as part of that replacement.
- First migrate references in all affected scenes, prefabs and TerrainData, including
  backups. Check for missing references after removal. Do not remove shared assets
  still used by other content. A recovery archive may be kept outside Assets.
- Do not assume similarly named newly imported folders are duplicates. Compare model
  and texture contents and explain the evidence. Preserve newly supplied packs unless
  their removal is authorized.
