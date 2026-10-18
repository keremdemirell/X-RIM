# PT6 balance proposals (Session 16)

From the first bot batch, `bot-batch-2026-10-18-1902.md`: 500 matches, 100 per run, seeds 1–100, random loadouts, project tuning.
**Everything here is a proposal. Nothing has been changed; you decide at PT6.** "Heuristic" is the new practice bot, "random" the old scribbling one.

## What the batch measured

| Question | Answer |
|---|---|
| Does the heuristic bot beat the random bot? | **Yes: 94 of 100 (94% ± 5%).** |
| Dismemberment (GDD §11 target: severs in 10–20% of matches) | **15%** between two heuristic bots (the middle of the target); 22% when the heuristic bot plays the random one. |
| Match length (GDD §1: TBD) | **12–14 turns** on average (median 11–12). About **1:30–1:50** per match if a person plans for 6 s a turn; 2:50–3:15 if they used the full 12 s every turn. |
| Sudden death | Reached in 8–13% of matches; 5–8% of matches were decided by "more HP" after 3 sudden-death turns without a hit. |
| Electric wall | 2–3 zaps per 100 matches between heuristic bots (they never step back into a standing wall); 53 zaps in 35 matches with the random bot. |
| Simulation cost (Editor) | 18 ms per turn on average, 92 ms at worst. |

## Proposals

### 1. Limb durability (§11): keep arm 40 / leg 50
The sweep, between two heuristic bots:

| Durability | Arm / leg | Matches with a sever |
|---|---|---|
| ×0.6 | 24 / 30 | 57% |
| ×0.8 | 32 / 40 | 31% |
| **×1 (today)** | **40 / 50** | **15%** |
| ×1.25 | 50 / 62.5 | 10% |

Today's values sit exactly in the middle of the target, so a sever stays special: about one match in six or seven.
- **Proposal:** no change.
- **Option:** people see damage and may hunt a damaged arm harder than the bot does. If PT6 shows more severs than that, try ×1.1 (arm 44, leg 55).

### 2. Leg severs never happen: lower leg durability to 35 for a test
In 100 matches at today's values, **0 of 15 severs were legs**, and only 4 of 75 even at ×0.6. In a side view the weapon arm is closest, and the bot aims at the torso, the head or a damaged limb. So the leg-loss states are designed but practically never seen: no jump or lunge on one leg, sitting on the pelvis with none.
- **Proposal:** try **leg durability 50 → 35** in the Damage asset at PT6.
- **Why, for feel:** a dummy hopping on one leg is peak physical comedy (pillar 4).
- **Caveat:** the bots rarely aim low, so judge it by your own matches first. If you keep 50, legs effectively can't be cut.

### 3. Target match length (§1): about 2 minutes, 10–15 turns
- **Proposal:** set the target to **2:00** (`Target Match Length Seconds` = 120 in the Balance Targets asset), meaning 10–15 turns.
- **Why, for feel:** a short duel suits a phone session and keeps the moments coming.
- Today's measurement is just under it with 6 s of planning a turn. The batch report will then say longer or shorter for every run.
- This stays TBD until you decide.

### 4. Spear: lower its base damage 10 → 8
The spear leads in every heuristic run:
- it wins **57–62%** of the matches it is carried into (the others: 43–55%);
- it has the most damage per swing (**8.6–9.1**, rapier about 7.6);
- it lands the most winning blows (40 of about 100);
- it lands more than one hit per swing (its long reach plus the drag-through).

It is not formally flagged "strictly stronger": within each run it is inside the 95% margin, and the runs share seeds and loadouts, so they are not independent. But it trends that way, and the sidegrade rule says no weapon may be strictly stronger. Its stats are placeholders (design TBD).
- **Proposal:** base damage **10 → 8**. It keeps its identity (longest reach, straight thrusts) and its damage per swing comes down near the rapier's.
- **Alternative:** speed 700 → 600.

### 5. Sword: raise its speed 600 → 700
The sword trails: it wins **43–46%** when carried and lands only **0.35–0.52 hits per swing**.
- **Proposal:** speed **600 → 700**. It arrives first more often (§9) while keeping its middle place between the rapier and the mace.
- **Alternative:** length 320 → 360.

### 6. Mace, rapier, shield: no change
- **Mace:** rarely lands (0.15–0.19 hits per swing) but hits hardest (about 18 a hit); it wins 47–52%. That is the intended slow, heavy identity.
- **Rapier:** wins 52–55%.
- **Shield:** wins 45–54%. It almost never hits, by design (it defends).

### 7. Signature moves with the mace almost never land (a question for you, §8)
- Signature turns with a hit: the rapier about 55%, the spear about 75%, the sword about 25%, but **the mace 0–15%** (0–4 hits in 6–27 uses).
- This confirms the Session 15 note: a move plays at the weapon's speed, so the mace (250) only plays the start of a pattern.
- **Your call:** should a signature move always arrive (for example, play at least at some minimum speed), or is "the mace's moves are slow" fine? Weapon speed is Decided per weapon (§9), so this needs a §8 decision.
- **Recommendation:** decide once the real moves are designed; leave it for now.

### 8. Sudden death ending on "more HP" (5–8% of matches): re-run first
- Most of those long matches had both bots holding short weapons (mace, sword, shield) that could not reach at the starting gap.
- The heuristic bot then stood still, because it only lunged when a lunge would reach. **That bot weakness is fixed** (Session 16 batch 6: with nothing in reach it now lunges to close in).
- **Proposal:** run the batch again at PT6 (XRim → Balance → Run Bot Batch…). If more than about 3% of matches still end on "more HP", consider 3 → 5 sudden-death repeats, or turning the electric wall on in sudden death (§14 options).

## How to apply a value at PT6
1. Press Play in the Match scene and open **Debug → Tuning**.
2. Change the value: the weapon asset for damage or speed, the Damage asset for durability, the Balance Targets asset for the target length. Edits made in the Editor persist after Play mode.
3. Run **XRim → Balance → Run Bot Batch…** again, and compare the new report with this one.
4. Tell me what you changed, in the PT6 report, and I record it in `MEMORY.md`.
