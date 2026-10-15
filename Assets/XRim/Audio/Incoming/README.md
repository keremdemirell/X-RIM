# Sound files go here

**The full step-by-step guide (where to find sounds, licences, formats, recording your own grunts) is `Docs/SOUND_GUIDE.md`.** This file is the short version. In Unity, **XRim → Setup → Open Sound Folder** opens this folder for you.

Drop `.wav` or `.ogg` files in this folder, then run **XRim → Setup → Build Audio Sets From Incoming**. The menu sorts the files by name, puts them in the default effects set and grunt pack (`Assets/XRim/Data/Audio/`), and tells you in the Console which sounds are still missing. A sound without a file is silent, never an error, so you can add files a few at a time.

If the menu says there is no Audio asset, run **XRim → Setup → Create Default Tuning Assets** once first (it never overwrites anything).

## Naming

A file's name starts with the sound's name below. Anything after it and an underscore (or a number straight after it) is free, so one sound can have several versions and the game picks one at random each time: `hit_heavy_01.wav`, `hit_heavy_02.wav`, `hit_heavy_crunchy.ogg`. Upper or lower case does not matter, and spaces or dashes work in place of underscores (`Hit Heavy 2.wav`, `hit-heavy.wav`). A name the game does not know is reported with a suggestion ("Did you mean hit_light?"); **XRim → Reports → Audio Coverage** lists everything that is missing or misnamed at any time.

| File name starts with | When it plays |
|---|---|
| `hit_light` | A light hit lands |
| `hit_heavy` | A heavy hit lands (15+ damage) |
| `hit_head` | A hit lands on the head |
| `clash_rebound` | Two weapons bounce off each other (or one deflects the other) |
| `clash_crush` | A weapon crushes through the other |
| `clash_slide` | Two weapons scrape past each other |
| `block_full` | A shield fully blocks |
| `block_partial` | A shield partly blocks (rim or glancing) |
| `joint_snap` | A limb is severed |
| `bolt_scatter` | Bolts and nuts spill (a sever, a death) |
| `torso_split` | A dummy dies and its torso splits |
| `weapon_drop` | A dropped weapon clatters |
| `wall_warning` | An electric wall springs up as a warning |
| `wall_zap` | An electric wall zaps a dummy |
| `swing_whoosh` | A weapon starts its swing |
| `body_move` | A dummy lunges, steps back, crouches or jumps |
| `grunt_attack` | A dummy grunts as it swings (like a tennis player) |
| `grunt_effort` | A dummy grunts as it lunges or jumps |
| `grunt_hurt` | A dummy is hit |
| `grunt_big_hurt` | A dummy takes a heavy hit, loses a limb or is zapped |
| `grunt_knock_out` | A dummy is knocked out |
| `music` | The tense duel loop (one file, for example `music_duel_loop.ogg`) |

Sounds starting with `grunt_` form the grunt pack; the others form the effects set. Both are swappable sets (later, cosmetics a player can own).
