# X-RIM sound guide

How to find, record, prepare, name and add the game's sounds, step by step. You need no audio experience.

The game already works without any sounds: every sound that has no file is simply silent, and nothing breaks. So you can add sounds a few at a time, in any order, and listen to the result after each one.

**Contents**

1. [The quick version](#1-the-quick-version)
2. [The shopping list: every sound the game plays](#2-the-shopping-list-every-sound-the-game-plays)
3. [Where to find sounds, and which ones you may use](#3-where-to-find-sounds-and-which-ones-you-may-use)
4. [Recording your own grunts with your phone](#4-recording-your-own-grunts-with-your-phone)
5. [Preparing a file: trim, volume, format](#5-preparing-a-file-trim-volume-format)
6. [Naming the files](#6-naming-the-files)
7. [Putting the sounds into the game, click by click](#7-putting-the-sounds-into-the-game-click-by-click)
8. [Checking what is still missing](#8-checking-what-is-still-missing)
9. [Writing down where each sound came from](#9-writing-down-where-each-sound-came-from)
10. [If something goes wrong](#10-if-something-goes-wrong)

---

## 1. The quick version

1. **Find or record** a sound (parts 3 and 4).
2. **Prepare it:** a `.wav` or `.ogg` file that starts the instant the sound starts, with no silence before it (part 5).
3. **Rename it** to the game's name for it, for example `hit_light.wav` (parts 2 and 6).
4. In Unity, click the menu **XRim → Setup → Open Sound Folder** and copy the file into the folder that opens.
5. In Unity, click **XRim → Setup → Build Audio Sets From Incoming**.
6. Press **Play** and listen. Then write down where the sound came from in `Docs/CREDITS.md` (part 9).

The rest of this guide explains each step in detail.

---

## 2. The shopping list: every sound the game plays

The game knows 21 sounds plus the music. Each has a fixed **name**. Your file's name must start with it (part 6 explains the details).

The list is in the order to find them: the first 8 make the biggest difference, so start there. **Versions** means how many different files of the same sound are worth having: the game picks one at random each time, so a hit does not sound identical every time. One file is enough to start.

The game's style (GDD §2): crash-test dummies made of plastic and metal, so **metal and plastic impacts, never fleshy or bloody sounds**, and **comic, cartoonish grunts**.

| # | Name | When the game plays it | What to look for | Versions | Length |
|---|---|---|---|---|---|
| 1 | `hit_light` | A light hit lands (under 15 damage) | A dull metal-and-plastic thump or clank, like a wrench tapping a car door | 3–4 | 0.1–0.4 s |
| 2 | `hit_heavy` | A heavy hit lands (15 damage or more) | A bigger, crunchier metal impact with some bass, like a sledgehammer on sheet metal | 2–3 | 0.2–0.6 s |
| 3 | `clash_rebound` | Two weapons hit each other and bounce off | A bright metallic clang that rings a little: sword on sword | 3 | 0.3–1.0 s |
| 4 | `grunt_hurt` | A dummy gets hit (its voice) | A short comic "oof!", "ugh!", "ow!" | 4–6 | 0.2–0.6 s |
| 5 | `joint_snap` | A limb is cut off | A sharp crack with a metal snap: a bolt shearing, a plastic joint popping | 2 | 0.2–0.5 s |
| 6 | `bolt_scatter` | Bolts and nuts spill out (a cut limb, a KO) | Small metal parts bouncing on a hard floor: a handful of screws dropped on concrete | 2 | 0.5–1.5 s |
| 7 | `swing_whoosh` | A weapon starts its swing | A quick whoosh of air | 3 | 0.2–0.5 s |
| 8 | `grunt_attack` | A dummy swings its weapon (its voice) | A tennis-player effort grunt: "hah!", "hyah!" | 4–6 | 0.2–0.5 s |
| 9 | `hit_head` | A hit lands on the head | A hollow "bonk" or clang, like a helmet being struck | 2–3 | 0.2–0.5 s |
| 10 | `clash_crush` | A heavy weapon crushes through the other weapon | A heavy crunch of bending metal | 2 | 0.3–0.8 s |
| 11 | `block_full` | A shield blocks a hit completely | A solid, deep "dong" on a thick metal plate | 2 | 0.2–0.6 s |
| 12 | `torso_split` | A dummy is knocked out and its torso splits in two | A big crack with tearing metal | 1–2 | 0.4–1.2 s |
| 13 | `grunt_knock_out` | A dummy is knocked out (its voice) | A long, over-the-top comic cry: "noooo!", "aaargh!" | 2–3 | 0.5–1.5 s |
| 14 | `grunt_big_hurt` | A dummy takes a heavy hit, loses a limb, or is zapped (its voice) | A bigger, more dramatic "AAH!" | 3–4 | 0.3–0.8 s |
| 15 | `wall_zap` | The electric wall zaps a dummy | An electric zap or buzz: a bug zapper, a stun-gun crackle | 2 | 0.3–0.8 s |
| 16 | `clash_slide` | Two weapons scrape past each other | Metal scraping on metal, like a knife being sharpened | 2 | 0.2–0.6 s |
| 17 | `block_partial` | A shield blocks only part of a hit (its rim, or a glancing hit) | A lighter, glancing clink on a metal plate | 2 | 0.2–0.5 s |
| 18 | `weapon_drop` | A dropped weapon hits the floor | A metal object falling and rattling on a hard floor | 2 | 0.4–1.0 s |
| 19 | `wall_warning` | An electric wall springs up as a warning | A short rising electric hum or crackle | 1–2 | 0.4–1.0 s |
| 20 | `body_move` | A dummy lunges, steps back, crouches or jumps | A quick scuff of feet, or a short mechanical whirr | 2–3 | 0.1–0.4 s |
| 21 | `grunt_effort` | A dummy lunges or jumps (its voice) | A small "hup!" or "hmph!" | 3–4 | 0.2–0.5 s |
| – | `music` | During the whole match | A tense duel loop that repeats without a gap: ticking, pulsing, building suspense | 1 | 30 s – 2 min |

**About the five `grunt_…` sounds:** together they are the dummy's voice, called a *grunt pack*. Record or pick all five in the same voice, so a dummy sounds like one character. (Later, players may unlock other grunt packs as cosmetics.)

---

## 3. Where to find sounds, and which ones you may use

### Licences, in plain words

Every sound belongs to someone. A **licence** is the owner's written permission saying what you may do with it. Because X-RIM will make money (it will sell cosmetics), **only use sounds whose licence allows use in a commercial game.** Each sound's licence is shown on the page you download it from.

| Licence (as the website shows it) | What it means | OK for X-RIM? |
|---|---|---|
| **CC0**, "Creative Commons 0", "Public Domain" | Free for anything; you do not have to credit anyone | **Yes**, the easiest |
| **CC BY**, "Attribution" | Free for anything, as long as you credit the author | **Yes**, and write the credit in `Docs/CREDITS.md` (part 9) |
| **CC BY-SA**, "Attribution-ShareAlike" | Credit, and anything made from it must be shared under the same licence | **Avoid**: unclear for a game |
| **CC BY-NC**, "NonCommercial" | Not for anything that earns money | **No** |
| "Royalty-free" from a shop or a bundle (Sonniss, the Unity Asset Store) | Use in your games after you get it; read its licence page | **Yes**, keep a copy of the licence |
| Pixabay Content License | Free for commercial use, no credit needed | **Yes** |
| Sounds taken from other games, films, YouTube, TikTok, TV | Someone else's property | **Never** |

If a page does not say clearly which licence a sound has, skip it.

### Good places to look

1. **Kenney** (kenney.nl). Free game sound packs, all CC0. Click *Assets*, then filter by *Audio*. Useful packs include *Impact Sounds* (metal and plastic hits), *RPG Audio* (weapons, metal) and *Sci-Fi Sounds* (electric zaps). Click a pack, then *Download*: you get a zip file. Double-click it in Windows to see the sounds inside, and double-click a sound to hear it.
2. **Freesound** (freesound.org). The biggest free library, recorded by thousands of people. You need a free account to download (*Join* at the top). Type a search word (see the list below), then **on the left side, under *License*, tick *Creative Commons 0*** (or *Attribution*). Never pick *Attribution NonCommercial*. Click a sound, press play to listen, and click *Download*. Write the author's name and the page address into `Docs/CREDITS.md` right away.
3. **Sonniss GDC Game Audio Bundle** (sonniss.com, search the site for "GDC"). Every year Sonniss gives away tens of gigabytes of professional game sound effects. Its licence allows commercial games and needs no credit (keep the licence PDF that comes with it). The download is huge, so download one year's bundle and only unzip the folders you need (look for folder names like *Impacts*, *Metal*, *Whooshes*, *Electricity*).
4. **Pixabay** (pixabay.com, then *Sound Effects* or *Music* at the top). Free for commercial use, no credit needed. Good for the music loop too.
5. **OpenGameArt** (opengameart.org). Free game assets with a different licence per file; the licence is shown on each page. Take only CC0 or CC BY.
6. **Music.** Besides Pixabay Music, Kevin MacLeod's music at incompetech.com is free with a credit (CC BY). Search for words like *tense*, *suspense*, *battle*, *duel*, and prefer tracks whose title or description says **loop** or **seamless** (they repeat without a gap).

### Search words that work

`metal hit`, `metal impact`, `armor hit`, `clang`, `sword clash`, `metal scrape`, `plastic hit`, `bonk`, `crack`, `snap`, `screws drop`, `bolts drop`, `debris`, `whoosh`, `swish`, `swing`, `zap`, `electric spark`, `taser`, `buzz`, `servo`, `robot movement`, `footstep scuff`, `grunt`, `effort`, `pain`, `ouch`.

Tip: crash-test dummies are plastic and metal, so a *metal or plastic* impact fits better than a punch on a body.

---

## 4. Recording your own grunts with your phone

Grunts are the funniest sounds in the game and the easiest to make yourself. Your own voice also makes the game unique, and there is no licence to worry about: you own it.

1. **Find a quiet room.** A wardrobe full of clothes, or a parked car, is ideal: soft things stop the echo. Turn off fans, the TV and anything humming.
2. **Open your phone's voice recorder app** (on a Samsung it is called *Voice Recorder*). If its settings have a *recording quality*, choose the highest.
3. **Hold the phone a hand's width (15–20 cm) from your mouth**, a little to the side, so your breath does not blow into the microphone.
4. **Record each kind of grunt 5–10 times in a row**, with about a second of silence between them. Make them comic and exaggerated, like a cartoon:
   - attack: "hah!", "hyah!", "hup-HAH!"
   - effort: "hup!", "hmph!"
   - hurt: "oof!", "ugh!", "ow!"
   - big hurt: "AAAH!", "OW-OW-OW!"
   - knocked out: "noooooo!", "aaaargh…"
5. **Do not shout so loud that it crackles.** For the loud ones (the knockout cry), move the phone further away.
6. **Send the recording to your computer:** a USB cable, Google Drive, or email it to yourself.
7. **Cut it into one file per grunt** and keep the best takes (part 5 shows how). Name them `grunt_hurt_01.wav`, `grunt_hurt_02.wav` and so on (part 6).

Record all five kinds in the same voice, so the dummy sounds like one character.

---

## 5. Preparing a file: trim, volume, format

The game can read **`.wav`** and **`.ogg`** files (also `.mp3` and `.aif`). The free program **Audacity** (audacityteam.org) does everything below. Download it, install it and open it. Menu names are given for recent Audacity versions; older versions are noted in brackets.

### Trimming: the most important step

A sound must start at the very beginning of its file. If a hit sound has 0.2 seconds of silence before the bang, the hit on screen and the sound will not match, and the game will feel laggy.

1. In Audacity: **File → Open…**, then pick the sound file. You see its waveform: flat where it is silent, wide where it is loud.
2. Press the **space bar** to listen. Press it again to stop.
3. **Click and drag over the flat part before the sound starts**, then press **Delete** on your keyboard.
4. Do the same with any long silence at the end. Keep a short, natural fade-out.

### Cutting one recording into several files (your grunts)

1. Click and drag to select one grunt, from just before it starts to just after it ends.
2. **File → Export Audio…**, and in the window that opens choose **Current selection** as the range. (Older Audacity: **File → Export → Export Selected Audio…**)
3. Name it (part 6) and save it. Repeat for each grunt you want to keep.

### Evening out the volume

1. Press **Ctrl + A** to select the whole sound.
2. **Effect → Volume and Compression → Normalize…** (older Audacity: **Effect → Normalize…**).
3. Tick *Normalize peak amplitude to* and type **-1** (dB). Click **Apply** (or **OK**).

Do this for every file, so no sound is much louder or quieter than the others. Fine adjustments can be made later inside the game (part 10).

### Saving in the right format

1. **File → Export Audio…** (older: **File → Export → Export as WAV**).
2. Format: **WAV**, encoding **16-bit PCM** (the default). Mono or stereo does not matter: the game turns effects into mono by itself.
3. Name the file (part 6) and save it.

### Converting a file

Phones often record **`.m4a`** files, which the game cannot read. Open them in Audacity and export them as WAV as shown above. If Audacity says it cannot open the file, it needs its free FFmpeg add-on: go to **Edit → Preferences → Libraries**, follow the FFmpeg download link, install it, restart Audacity and try again.

### The music loop

A loop plays its end straight into its beginning. If the end and the start do not match, you hear a click or a jump every time it repeats. The easiest way to avoid this is to download music labelled **loop** or **seamless**. Do not trim the music like the effects: its silence and length are part of the loop.

---

## 6. Naming the files

The game decides which sound a file is from its name. **The name must start with one of the 21 names in part 2** (or with `music`).

- **Several versions of the same sound:** add an underscore and a number (or a word) after the name: `hit_light_01.wav`, `hit_light_02.wav`, `hit_light_crunchy.wav`. The game picks one at random each time.
- **Forgiving:** capital letters, spaces and dashes are understood: `Hit Light 2.wav`, `hit-light.wav` and `HitLight.wav` all work as `hit_light`. Lower case with underscores is still the safest.
- **Music:** one file whose name starts with `music`, for example `music_duel_loop.ogg`. If there are several, only the first by name is used.
- **A wrong name is never a disaster:** the file is ignored, and the game tells you which file and what it probably should be called ("Did you mean hit_light?").

| ✓ Works | ✗ Does not work, and why |
|---|---|
| `hit_light.wav` | `light_hit.wav`: the name must *start* with `hit_light` |
| `hit_light_03.wav` | `hitlight.wav`: missing underscore (the report suggests `hit_light`) |
| `Hit Light 3.wav` | `hit_ligth.wav`: typo (the report suggests `hit_light`) |
| `grunt_hurt_ouch.ogg` | `ouch.wav`: not a name the game knows |
| `music_duel_loop.ogg` | `scream.m4a`: `.m4a` cannot be read (convert it, part 5) |

---

## 7. Putting the sounds into the game, click by click

**The first time only:**

1. Open the X-RIM project in Unity and wait until it has finished loading.
2. Click the menu **XRim → Setup → Create Default Tuning Assets**. This creates the game's *Audio* settings file if it does not exist yet. (Your project does not have it yet. The menu never overwrites anything you have tuned, so it is always safe.)

**Every time you add, rename or remove sounds:**

3. Click **XRim → Setup → Open Sound Folder**. A Windows Explorer window opens on the folder `Assets/XRim/Audio/Incoming`. You can recognise it by the `README.md` file inside.
4. **Copy your sound files into that window**: drag them in, or copy them (Ctrl + C) and paste them there (Ctrl + V). To remove a sound, delete its file from this folder.
5. **Click back into the Unity window.** Unity notices the new files and imports them; a small progress bar may appear for a moment. It gives each file the right settings by itself (effects ready to play instantly, the music streamed), so there is nothing to set.
6. Click **XRim → Setup → Build Audio Sets From Incoming.** This puts every correctly named file into the game.
7. **Read the report.** Open the *Console* window if it is not open (menu **Window → General → Console**, or **Ctrl + Shift + C**). The newest message starts with `[XRim] Audio sets built from…` and lists:
   - how many sounds were found,
   - which sounds are still silent,
   - any file that could not be used, with the reason and a suggested name.
8. **Listen.** Press the **Play** button (▶) at the top of Unity, start a match against the bot, and play a few turns. The music starts with the match.
9. **Adjust the volumes**, if needed: while playing, click the **Debug** button (top left of the game), open the **Tuning** tab, and click **Audio**. You see sliders for the master, effects, voices (grunts) and music volume. You can also change them outside Play mode: in Unity's *Project* window, open `Assets/XRim/Data/Tuning/`, click `Audio`, and use the *Inspector* on the right.

---

## 8. Checking what is still missing

Click **XRim → Reports → Audio Coverage** at any time. It changes nothing; it only writes a report in the Console:

- **Sounds that play:** which sounds have files, and how many versions each has.
- **Still silent:** which sounds have no file yet, most important first. That is your shopping list.
- **Music:** the music file used, if any.
- **Files the game cannot use:** each one with the reason and how to fix it (a typo in the name, an unreadable `.m4a`, a second music file).
- **New since the sound sets were last built:** files you added but have not put into the game yet. Run **XRim → Setup → Build Audio Sets From Incoming** to add them.

---

## 9. Writing down where each sound came from

Open `Docs/CREDITS.md` (in the project folder, next to this guide) in any text editor and **add one line per sound you did not make yourself**: the file name, where you got it, the author, the licence and the web address. Do it right when you download, while the page is still open. It takes a minute and saves a lot of trouble later:

- a CC BY sound *must* be credited, and these notes become the game's credits screen;
- for every other sound, it is your proof that you were allowed to use it.

Your own recordings go in too, with "recorded by me" as the source.

---

## 10. If something goes wrong

| What you notice | What to check |
|---|---|
| **No sound at all** | 1. In Unity's *Game* view, the speaker button **Mute Audio** in the toolbar must be off. 2. Did you run **XRim → Setup → Build Audio Sets From Incoming** after copying the files? 3. Does the Console say the tuning profile has no Audio asset? Then run **XRim → Setup → Create Default Tuning Assets** (part 7, step 2) and build the sets again. 4. Are the volumes in the Audio settings above zero? 5. Are your computer's speakers on? |
| **One sound never plays** | Run **XRim → Reports → Audio Coverage**. It shows whether the name is wrong (with the right name) or the format unreadable. |
| **A sound starts late** | There is silence at the start of the file: trim it (part 5). |
| **One sound is much louder or quieter than the rest** | Normalize it to -1 dB (part 5). To fine-tune a single sound inside the game: in the *Project* window open `Assets/XRim/Data/Audio/`, click `Effects` (or `Grunts` for voices), and in the *Inspector* open *Cues*, find the sound, and lower or raise its *Volume*. Building the sets again keeps that volume. |
| **The music clicks or jumps when it repeats** | The file is not a seamless loop. Pick a track labelled "loop" or "seamless". |
| **"Unity cannot read .m4a files"** | Convert the file to WAV with Audacity (part 5, *Converting a file*). |
| **A sound plays at the wrong moment** | Its name is probably that of a different sound: check part 2 and rename it. |
