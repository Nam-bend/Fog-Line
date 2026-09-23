# MotherMoster animations

Open `MotherMoster_Animated.blend` and press Space to preview the revised Attack action. Select `MotherMoster_Rig`, switch the Dope Sheet to Action Editor, and choose a `MotherMoster_*` action to view the other clips. Playback is 30 fps.

| Action | Seconds | Blender frames | Loop |
| --- | ---: | --- | --- |
| Idle | 3.2 | 1–97 | Yes |
| Walk | 1.2 | 1–37 | Yes |
| Run | 0.667 | 1–21 | Yes |
| Attack | 1.2 | 1–37 | No |
| Attack_Sweep | 1.1 | 1–34 | No |
| Attack_Combo | 1.6 | 1–49 | No |
| Attack_Slam | 1.5 | 1–46 | No |
| Stagger | 0.8 | 1–25 | No |
| Death | 2.4 | 1–73 | No |

The edited skeleton, 1,200-triangle mesh and 256×256 texture are preserved. Locomotion is in place. The attack strike occurs at 0.5 seconds. Death holds its final pose.

Revised creature motion uses a crouched hunting posture, a watchful head turn, uneven breathing, weighted steps, and trailing tentacles. Attack pulls both front arms behind the shoulders before a simultaneous forward whip; the distal segments unfurl after the shoulders, then recover more slowly. Both tips reach forward at the 0.5-second strike event.

Attack_Sweep twists the torso into a wide horizontal sweep. Attack_Combo alternates left and right diagonal slashes. Attack_Slam raises both arms overhead before bringing them down with the torso. Run uses a 20% faster cadence, longer strides, a lower forward lean, reaching arms and trailing dorsal tentacles.

Open `AnimationPreviews/preview.html` for flipbooks of all four attacks, Idle, Walk and Run. It supports real-time playback, slow motion and frame scrubbing. `attack_motion_check.json` records the shoulder/tip positions used to verify both arms swing from behind the shoulders to in front of the body.

## Unity

The FBX in `Assets/_Project/Art/Enemies/MotherMosterPSX` contains all nine takes. Assign `Assets/_Project/Animations/Enemies/MotherMoster/MotherMoster.controller` to the model's Animator; disable Apply Root Motion.

- `IsMoving`: false = Idle, true = locomotion.
- `IsWalking`: true = Walk, false = Run when moving.
- `Attack`, `Stagger`, `Die`: triggers. Death is terminal until the Animator is explicitly reset.
- `AttackVariant`: integer 0 = double whip, 1 = sweep, 2 = combo, 3 = overhead slam. `MotherAttackVariation` chooses a different variant when an attack state exits. All attack states carry the `Attack` tag so `EnemyAI` waits for the full move to finish.
- The attack event calls `OnMonsterStrike`; attach the existing `MonsterAttackEvents` component to the Animator object if using `EnemyAI`. `MotherAI` currently has no attack/damage handler or animation movement driver; those gameplay connections are not added by this animation asset.
- Strike events: double whip 0.50 s; sweep 0.533 s; combo 0.50 and 0.933 s; slam 0.75 s. Existing `EnemyAI` resolves damage once per windup, so the combo's second strike is visual unless a future combat handler supports multiple hits.

`Tools > Monster > Build MotherMoster Animations` imports the FBX, extracts standalone `.anim` clips, rebuilds the controller through Unity's API, creates an animated visual prefab, and samples every clip to check its skeleton bindings. This command is provided in `Assets/_Project/Editor/MotherAnimationSetup.cs`.

Validation completed in Blender: all nine FBX actions reimport, skeleton positions remain unchanged, looping endpoints match, and evaluated vertices are finite. Representative renders are in `AnimationPreviews`.

Unity Editor validation and prefab generation could not run because Unity 6000.3.21f1 reported no valid Editor license. The controller and FBX importer metadata are prepared, but their import/runtime behavior still requires validation after license activation. See `Logs/MotherAnimationBuild.log`.
