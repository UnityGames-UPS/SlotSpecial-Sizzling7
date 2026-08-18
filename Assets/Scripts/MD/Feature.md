# Free Games Feature — Spec

Status: **on hold**, not yet built. `Assets/Scripts/Feature/FreeGameView.cs` exists as an empty MonoBehaviour. Everything below is agreed and ready to build from; the only open item (retriggers) does not block starting.

## Trigger

3 bonus/scatter symbols land → they animate as normal (`SpinWinText` still shows the spin's win, same as any other spin) → after the usual win-line timing, a frame graphic tweens from 0% to 100% size over ~2.5s.

At the same moment the frame starts tweening in, **two images swap simultaneously**: the `SlotObject` image and the `reelBackground` image. Both swaps must stay in sync — implement as one call so they can't drift apart.

## Pick sequence

1. Frame reaches full size.
2. Text fades in: **"FREE GAMES AWARDED"**. Holds 2s. Fades out.
3. Text fades in: **"CHOOSE YOUR FREE GAMES"**, with 4 mystery boxes shown below it.
4. Player picks one box:
   - It fades out, and the win reveal fades in **in the same position**.
   - **The other 3 boxes stay put — they do not fade or reveal anything.** No "here's what was behind the others" moment.
   - Box hover colour-change is wanted eventually but explicitly deferred — not part of the first build.
5. Reveal holds 1.5s.
6. Whole frame tweens back down to scale 0 over ~2s.
7. **"FREE GAMES X OF Y"** text has appeared by this point.

Scatter/bonus symbol animations keep running continuously in the background through this entire sequence (frame in → text beats → pick → reveal → frame out). They only stop once the first free spin actually starts.

## Box → prize mapping

| Box (`boxId`) | Free games awarded |
|---|---|
| `yellow` | 8 |
| `red` | 5 |
| `green` | 15 |
| `blue` | 10 |

Each tier has **3 possible win multipliers** (confirmed: `green` has shown 3 and 5 so far, third value unconfirmed).

The server tells us the outcome (`boxId` + `awardedSpins`) on the trigger spin itself — well before the player can click anything. So at trigger time, once the prize is known, instantiate/configure the correct front (see Art below) behind all 4 backs. Whichever box the player picks reveals the same, correct prize — the pick is cosmetic, not a decision point.

If an unrecognised `boxId` ever arrives, fall back to selecting the front by `awardedSpins` instead, so a surprise value can't null-reference in front of a player.

## Art structure

The mystery-box "back" and all 4 "fronts" are **not flat sprites** — they're Unity object hierarchies the designer already built:

- **Back**: parent object with an Image component + 2 TMP children (the "? MYSTERY PICK" style back).
- **Front** (×4, one per tier): parent object with an Image component + 2 Image children + 2 TMP children.

All 4 fronts already exist as real objects because they're also used to show the tiers statically on the info page. Multipliers are **baked directly into the front's art** — all 3 of a tier's multipliers are shown in one TMP component on the front — so the reveal needs no dynamic multiplier text at all.

Plan: turn the back and each of the 4 fronts into **prefabs**, so the info page and the free-games picker instantiate from the same source rather than maintaining duplicate hand-built objects that can drift apart.

## Between pick and spins

- The spin button becomes a **"Start"** button.
- All buttons except Start, fullscreen, and turbo/quickspin are **disabled and sprite-swapped to a darker/greyed shade**.
- On clicking Start, an image appears containing two **separate** TMP components: **"MULTIPLIER"** (static label) and **"X[number]"** (the value). They're separate specifically because the number needs a continuous pulse animation (scale up/down) throughout the free games — that visual effect is independent of the multiplier's actual value or how often it updates.
- Multiplier value and remaining-games count both update from the backend on every spin.
- The multiplier number is purely a visual/flavour element — it has no further mechanical meaning to apply. (See Backend Findings: it's already baked into the win amount by the server. On a losing spin it doesn't represent anything real; that's fine, it's decorative.)

## The spins themselves

Functionally identical to a normal spin, with one difference: **win lines are skipped**. Just the symbol/win animations play, then the next spin fires after the normal wait. (Already implemented: `PlayTwoPhaseWinLines` sets `skipPhase2` when `isInFreeSpins`.)

`SpinWinText` shows during every free spin too, not just the trigger spin.

QuickSpin/turbo remain available during free spins.

## Big wins during free spins

Big wins can still fire mid-round. Spins must pause while one plays. Mostly already covered: `DelayBeforeNextFreeSpin` already waits on `while (waitingForSpecialWin || uiManager.IsSpecialWinActive)`.

## End sequence

After the last spin's animations and win text:

1. The same frame from the trigger **appears again — no tween this time, it just appears.**
2. Top text (placeholder name in code: `TOTAL WIN(text)` — actual displayed string is still "FREE GAMES AWARDED", pending confirmation from the team lead since it's the same string as the opening beat; build as specified for now).
3. Below it, the **total win accumulated across the whole free-games round counts up**, rendered through the sprite-digit font (`SpriteTextFormatter.ToSpriteDigits`).
4. The Start button has become a **"Take"** button — initially deactivated with a greyed sprite. Once the count-up finishes, it activates and sprite-swaps to its normal state.
5. Player clicks Take → whole screen fades to black → main game fades back in with everything reverted.

**Everything that must revert on the final fade:**
- `SlotObject` and `reelBackground` sprites back to normal
- Multiplier panel hidden
- X-of-Y counter hidden
- All greyed-out buttons restored
- Background music back to the main track

The two swapped background images (`SlotObject`, `reelBackground`) stay swapped for the **entire** feature and only revert after the final fade-out — not at any point in between.

## Backend findings (verified against real captured data)

Two full free-games sessions were captured and analysed (18 spins across two partial sessions, plus one complete round: trigger → 17 free spins → completion, with a spin on each side).

- **`freeSpinsMultiplier` is already applied server-side.** Comparing two spins with an identical reel/line pattern at different multipliers (3 vs 5) showed line payouts scaling exactly with the multiplier ratio, and dividing each by its own multiplier landed on the same base payout. Balance delta equalled `totalWin` exactly on every single spin, never `totalWin × multiplier`. **The client must never multiply the win by this value — it's informational only.**
- The field is rolled **fresh per winning spin**, not fixed per box or session, and not delayed — it can differ between consecutive wins in the same uninterrupted session.
- **No round-total field and no round-over flag exist in the wire format.** The client must accumulate `totalWin` itself across the round, and must treat `freeSpinsRemaining == 0` as the round-over signal (confirmed: the very next spin after that is an ordinary base-game spin with no trailing summary of any kind).
- **Free spins cost nothing** — balance never drops on any free spin regardless of win/loss. The real bet deduction resumes on the very next spin after the round ends.
- `freeSpinsRemaining: 0` can still be present in the payload on the first spin *after* the round ends, even though `isFreeSpin` has already flipped to `false` — don't key logic purely off that field's presence.
- Retriggers **do** happen mid-session (seen in earlier captures) and **can award a different box** than the one that started the session. The one complete round captured for this spec happened not to retrigger, so this remains unverified end-to-end.

**Outstanding backend ask:** get `serverTotalRoundWin` sent by the backend (needed for the closing count-up; client-side accumulation works today but can drift on a missed response or reconnect). A separate ask — sending `freeSpinsMultiplier` on losing spins too — was already sent to the backend team; the client is being built assuming it updates on every free spin regardless of the reply, per an explicit decision not to block on confirmation.

## Model / wire-format gaps to fix (not yet done)

`Assets/Scripts/Core/GameDataModels.cs`:
- `ServerResultFeatures` is missing a `boxId` field entirely (silently dropped today); add it, and add `boxId` to `FreeSpinData`.
- Its `scatter` field is misnamed — the real JSON key is `"bonus"` (with `triggered`/`bonusCount`/`positions`/`award`), so it never populates today.
- `ServerPayload` is missing `freeSpinsRemaining` (int) and `freeSpinsMultiplier` (must be **nullable** — absent must never become 0).
- `ConvertServerResponseToSpinResult` currently hardcodes `serverSpinsRemaining`/`serverSpinsUsed`/`serverTotalSpins`/`serverTotalRoundWin`/`isRoundOver` all to 0/false. This is a live bug: since `GameManager` copies `serverSpinsRemaining` into `freeSpinsRemaining` on every free spin, **free spins currently end after exactly one spin no matter how many were awarded.** Fix: `serverSpinsRemaining` ← `payload.freeSpinsRemaining`; `serverTotalSpins` ← client-summed `awardedSpins`; `serverSpinsUsed` ← total − remaining; `serverTotalRoundWin` ← backend once available, else client accumulation; `isRoundOver` ← `freeSpinsRemaining == 0`.

## Architecture

**Three View scripts total**, one new:

| Script | Role |
|---|---|
| `SlotView` | Reels, spin/win animations — unchanged |
| `UIManager` | HUD, buttons, popups — loses its free-spins portion to the new script |
| **`FreeGameView`** (new) | The entire free-games presentation: frame, boxes, reveal, multiplier panel, X-of-Y counter, end sequence, fade |

`GameManager` stays the sole controller. It already owns `isInFreeSpins`/`freeSpinsRemaining`/`freeSpinsUsed`, and that state is woven through `RequestSpin`, `GetTotalPay`, autoplay pause/resume, and roughly ten checks in `UIManager` — moving it would be a large, risky refactor for no benefit, since the free spins genuinely are the same spins as the base game.

**Control flow is one-way**, matching how `SlotView` already works: `GameManager` commands `FreeGameView` (e.g. `PlayIntroSequence(boxId, awardedSpins, onComplete)`), and `FreeGameView` reports back via callbacks. `FreeGameView` must never call `RequestSpin()` or otherwise drive the game loop itself — an earlier idea to let it do so was explicitly reconsidered and dropped, specifically to avoid bidirectional control flow between two scripts each acting like an authority over game state.

`FreeGameView` takes over `UIManager`'s existing free-spins responsibilities: `OnFreeSpinsStarted`, `OnFreeSpinsEnded`, `UpdateFreeSpinCount`, and the `EndFreeSpinsTransitionSequence` fade (the existing `transitionBackFilm` is reusable for the closing fade-to-black).

`FreeGameView` should live on a GameObject that stays active for the whole session — not on the frame object itself, since deactivating the frame would halt any coroutine running on it mid-sequence.

**Button ownership stays with `UIManager`** — it already owns every button's sprite states and enable/disable logic; `FreeGameView` signals sequence completion and `UIManager` handles the Spin→Start→Take swaps and the grey-out of other buttons, rather than splitting button control across two scripts.

## `SlotView.cs` change required

`AnimateSymbolSingleLoop` always schedules its own `StopAnimation()` after `winSymbolLoopDuration * loopCount`. For the free-games trigger, the scatter animation needs to run **indefinitely** through the whole intro/pick sequence, not stop on a fixed timer. Make `loopCount <= 0` mean "run indefinitely, skip scheduling the stop," and have `AnimateAllScatters` pass that for the trigger case.

This must stay conditional, not a blanket removal — `PlayStopAnimationsForColumn` also calls `AnimateSymbolSingleLoop` (with `loopCount = 1`) for wild-hit animations on reel stop, and would animate forever if the timer were removed unconditionally.

Once that's fixed, scatters stop naturally and correctly on their own: `StartSpin` (the first free spin) → `KillAllTweens` → `KillWinTweens`, which already calls `StopAnimation()` on every icon. No new stop logic is needed beyond removing the fixed timer for this one case.

## Explicitly deferred (not part of the first build)

- **Portrait mode** — deliberately held for a single whole-game pass later rather than done piecemeal here. High priority once picked up.
- Audio for every new beat (frame appear, pick, reveal, count-up, take) — pending final audio assets.
- Box hover colour-change.
- Any skip/fast-forward on the intro or reveal timing.
- The "FREE GAME 0 OF 8" starting-at-zero wording (existing behaviour, not addressed here).
- Handling a disconnect mid-round (today `OnDisconnected` just cleans up state; a free-games session in progress would be lost).

## Open questions

- **Retriggers.** Confirmed to happen mid-session and can award a different box than the one that started it. The spec above doesn't say whether the full frame/pick sequence replays, or whether it's a quieter "+N games" flourish instead. Not resolved yet — deliberately left open until the state machine for this case is actually being built; does not block starting the rest of the feature.
