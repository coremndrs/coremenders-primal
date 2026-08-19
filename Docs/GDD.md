# Game Design Document
## [Coremenders — Primal Frost]
**Version:** 0.1 — Initial Draft
**Date:** 2026-05-25
**Status:** Work in Progress
 
---
 
## Table of Contents
1. [Game Overview](#1-game-overview)
2. [World & Maps](#2-world--maps)
3. [Core Game Loop](#3-core-game-loop)
4. [Needs System](#4-needs-system)
5. [Time & Energy System](#5-time--energy-system)
6. [Character Creation](#6-character-creation)
7. [Tribe & NPC System](#7-tribe--npc-system)
8. [Tech Tree & Dreams](#8-tech-tree--dreams)
9. [Skill System](#9-skill-system)
10. [Inventory & Carrying](#10-inventory--carrying)
11. [Durability System](#11-durability-system)
12. [Crafting System](#12-crafting-system)
13. [Water System](#13-water-system)
14. [Structures & Building](#14-structures--building)
15. [Wildlife & Combat](#15-wildlife--combat)
16. [Animal Harvesting](#16-animal-harvesting)
17. [Food System](#17-food-system)
18. [Death, Rescue & The Dream Save System](#18-death-rescue--the-dream-save-system)
19. [Migration](#19-migration)
20. [Cross-Map Play & Communication](#20-cross-map-play--communication)
21. [Multiplayer](#21-multiplayer)
22. [Narrative & Lore](#22-narrative--lore)
23. [Tribe Quests](#23-tribe-quests)
24. [Open Questions](#24-open-questions)
---
 
## 1. Game Overview
 
### Concept
A co-op multiplayer prehistoric survival RPG. Players wake up in a prehistoric world with fading memories of a modern life they can no longer fully recall. Surrounded by dead tribespeople, they must lead the survivors of a broken tribe through an ice-age calamity — gathering resources, managing human needs, and migrating across a harsh and beautiful world to stay alive.
 
The world is not what it seems. As the tribe advances technologically, the truth of their world slowly surfaces through the artifacts they unearth and the dreams they dream.
 
### Genre
Co-op Multiplayer Survival RPG
 
### Platform
PC (Windows) — Initial release
 
### Art Style
Mid-poly 3D with a stylized, cartoonish aesthetic. The visual tone draws inspiration from the *Castlevania* Netflix animated series — expressive, slightly dark, yet vibrant and hand-crafted in feel. Characters and environments are readable and iconic, not photorealistic.
 
### Player Count
- **1–2 players.** There are always exactly **two dreamers** (Section 7); a session supports at most two humans — one per dreamer.
- **Solo:** one player controls both dreamers (switching between them) and manages the NPCs.
> *Future: supporting larger groups would mean additional humans controlling mortal tribespeople (ordinary NPCs without dream/save protection) — out of scope for the two-dreamer launch.*
 
---
 
## 2. World & Maps
 
### Overview
The world consists of **6 distinct maps** arranged along a **south-to-north axis** (Map 1 = southernmost, Map 6 = northernmost). The tribe migrates between maps in response to a temperature cycle that makes zones uninhabitable over time.
 
### Temperature States
Each map has one of three temperature states at any given time:
 
| State | Description |
|---|---|
| 🟡 **Chilly** | Livable without advanced shelter. Mild cold penalties. |
| 🟠 **Cold** | Survivable with proper gear and shelter. Resource penalties. |
| 🔴 **Very Cold** | Unlivable without exceptional gear. Lethal over time. |
 
### Temperature Cycle
The **livable band** (two adjacent Chilly zones) migrates north over ~9 months, then reverses south. A full round trip takes approximately **18 in-game months**.
 
**Rules:**
- There are always exactly **2 adjacent Chilly zones**
- A Chilly zone is always bordered by **at least 1 Cold zone** before reaching a Very Cold zone
| Month | Map 1 | Map 2 | Map 3 | Map 4 | Map 5 | Map 6 |
|---|---|---|---|---|---|---|
| Start | 🟡 Chilly | 🟡 Chilly | 🟠 Cold | 🟠 Cold | 🔴 V.Cold | 🔴 V.Cold |
| M3 | 🟠 Cold | 🟡 Chilly | 🟡 Chilly | 🟠 Cold | 🔴 V.Cold | 🔴 V.Cold |
| M5 | 🔴 V.Cold | 🟠 Cold | 🟡 Chilly | 🟡 Chilly | 🟠 Cold | 🔴 V.Cold |
| M7 | 🔴 V.Cold | 🔴 V.Cold | 🟠 Cold | 🟡 Chilly | 🟡 Chilly | 🟠 Cold |
| M9 | 🔴 V.Cold | 🔴 V.Cold | 🟠 Cold | 🟠 Cold | 🟡 Chilly | 🟡 Chilly |
| M12 | 🔴 V.Cold | 🔴 V.Cold | 🟠 Cold | 🟡 Chilly | 🟡 Chilly | 🟠 Cold |
| M14 | 🔴 V.Cold | 🟠 Cold | 🟡 Chilly | 🟡 Chilly | 🟠 Cold | 🔴 V.Cold |
| M16 | 🟠 Cold | 🟡 Chilly | 🟡 Chilly | 🟠 Cold | 🔴 V.Cold | 🔴 V.Cold |
| M18 | 🟡 Chilly | 🟡 Chilly | 🟠 Cold | 🟠 Cold | 🔴 V.Cold | 🔴 V.Cold |
 
### Map Content
Each map contains:
- **Biome-appropriate resources** (flora, fauna, minerals)
- **Wildlife** that responds to temperature and season
- **Caves** that can serve as natural shelters
- **Artifacts** — strange man-made objects that reveal the world's secret over time
- **Points of interest** for exploration
- **Resource piles** that persist across migrations (non-decaying materials: wood, stone, metals)
### Map Persistence
- Structures built on a map remain when the tribe migrates away
- Structures can **decay or be damaged** during Very Cold seasons (quality of build affects decay chance)
- Food and perishables decay over time or are stolen by wildlife if unsecured
- Non-perishable resources (stone, wood, metals) persist indefinitely in their piles
- Players can return individually to Cold maps if properly equipped
---
 
### Weather Events
 
#### Overview
Weather events occur across all maps and vary in frequency and intensity based on the zone's temperature state. They are **somewhat predictable** — players learn that colder zones bring harsher weather more often — but are never perfectly foreseeable. Attentive players can read **natural signs** to anticipate incoming events.
 
#### Weather Event Types
 
| Event | Description |
|---|---|
| **Watery Snow / Sleet** | Wet precipitation; wets clothing and causes warmth loss |
| **Snow** | Dry snowfall; reduces visibility and covers animal tracks |
| **Blizzard** | Severe snowstorm; greatly reduces visibility, makes outdoor tasks dangerous |
 
#### Frequency by Zone
 
| Zone | Watery Snow / Sleet | Snow | Blizzard |
|---|---|---|---|
| 🟡 **Chilly** | Rare | Rare | Very Rare / Never |
| 🟠 **Cold** | — | Very Common | Common |
| 🔴 **Very Cold** | — | Common | Very Common |
 
> As the temperature cycle shifts a zone colder, weather events become progressively more frequent and intense.
 
#### Effects on Players
 
**Warmth & Clothing:**
- Watery snow / sleet wets clothing, causing accelerated warmth loss
- Wet clothing that freezes causes **hypothermia** and **frostbite** if not dried or changed
- Clothing durability degrades faster during wet weather events
**Tasks:**
- Outdoor construction is affected during active weather events (slowed or restricted)
- Severe blizzards may make most outdoor tasks impossible or dangerous
#### Effects on Wildlife
 
| Weather Event | Wildlife Presence |
|---|---|
| **Blizzard / Heavy Snow** | Extremely rare — animals shelter and hide |
| **Snow** | Hard to find — animals less active |
| **Light Snow / Sleet** | Somewhat harder to find than clear conditions |
| **Clear** | Normal wildlife presence |
 
Animals shelter during weather events, becoming significantly harder to hunt. Predators may be emboldened after a blizzard when prey is weakened and scarce.
 
#### Effects on Hunting & Tracking
- Animal footprints appear in snow and can be followed to track prey
- Active weather events **cover prints over time** — fresh snow obscures recent tracks
- Heavy snow or blizzard conditions can erase tracks entirely, making tracking impossible
- Players must hunt before or after events for reliable tracking
#### Visibility Reduction
 
| Event | Visibility Impact |
|---|---|
| Sleet | Minor reduction |
| Light Snow | Moderate reduction |
| Heavy Snow | Significant reduction |
| Blizzard | Severe — navigating is difficult |
 
#### Predictability & Natural Signs
Weather events are tied to the temperature system — players know the general rule: *colder zones = more frequent and intense events.* However, exact timing is not telegraphed by a UI forecast.
 
**Nature signs that hint at incoming weather changes:**
- Certain **bird species** fly at different altitudes before a storm (high = clear, low = weather incoming)
- Other environmental cues may be discovered through **observation and experience**
- These signs reward players who pay attention to the world rather than relying on a UI indicator
---
 
## 3. Core Game Loop
 
### Macro Loop — The Migration Cycle
The driving loop of the game is **seasonal migration**:
 
1. Tribe starts in the **Chilly zone** (2 maps)
2. Temperature slowly drops — the current zone becomes Cold, then Very Cold
3. Wildlife becomes scarce in cooling zones
4. Tribe must **pack up, prepare, and migrate** to the next Chilly zone
5. In the new zone: establish camp, gather resources, build, craft, survive
6. Repeat — northward for ~9 months, then southward for ~9 months
The pressure to migrate is not sudden — it is gradual and visible. Players who delay too long risk **soft failure**: the zone becomes too hostile to operate in, and the tribe can be trapped in a daily death loop with no forward progress.
 
### Daily Loop
Each in-game day, the tribe:
 
1. **Wakes up** — time pools reset for all tribe members
2. **Assesses needs** — hunger, thirst, warmth, rest must be addressed
3. **Plans the day** — allocate time across gathering, building, crafting, hunting, research, rest
4. **Executes tasks** — collaboratively or individually
5. **Manages the tribe** — NPCs must be fed, assigned, supplied
6. **Rests/sleeps** — saves a checkpoint for the dreamers; NPCs consume sleep time
---
 
## 4. Needs System
 
### Overview
Every tribe member (player and NPC) has a **needs profile** based on a layered hierarchy inspired by human psychology. Meeting needs provides **buffs**; neglecting them causes **debuffs and illness** — and, left critical, inflicts **afflictions that drain Vitality**, routing into the same incapacitation model as combat (Sections 15 & 18). The game has **one death pathway**, not a separate one for needs.
 
The higher a player is on the pyramid, the more powerful their active buffs — but higher needs can only be fulfilled once lower ones are stable.
 
### Tier 1 — Physical Needs (Body)
These are survival-critical. Neglecting one causes escalating debuffs; left **critical**, it inflicts a matching **affliction** that **drains Vitality** over time — the same pathway combat afflictions use (Section 15).
 
| Need | Description |
|---|---|
| **Hunger** | Must eat regularly. Different foods provide different nutrition values. |
| **Thirst** | Must drink water (clean water preferred; contaminated water causes illness). |
| **Sleep** | Must sleep enough hours per day. Sleep deprivation causes cumulative debuffs. |
| **Temperature** | Must maintain body warmth. Clothing, fire, and shelter all contribute. |
 
### Critical Needs, Vitality & Death
When a Tier 1 need hits **critical**, it applies a matching **affliction** that drains **Vitality** for as long as the need goes unmet:
 
| Critical Need | Affliction |
|---|---|
| Hunger | Starving |
| Thirst | Dehydrated |
| Sleep | Exhausted |
| Temperature | Hypothermic |
 
- Multiple critical needs **stack** their Vitality drain.
- While the entity is still conscious these afflictions are **self-resolving** — meeting the need (eat, drink, sleep, warm up) clears the affliction and stops the drain.
- But if Vitality is driven below the **incapacitation threshold** first, the entity goes down (Section 15), and the affliction becomes **intervention-required** — someone else must address the need, since a downed entity can't help itself.
- **Outcome follows the universal model:** a downed **dreamer** enters the Section 18 flow (the intervention is simply feeding/warming/hydrating them; recovered if done in time, otherwise **Wake Up**); a downed **NPC** recovers if cared for in time, otherwise **dies permanently**.
There is no separate "needs death": combat and neglect feed the **same** afflictions → Vitality → incapacitation pipeline.
 
---
 
### Tier 2 — Safety & Mental Needs (Mind)
These require Tier 1 to be consistently met before they become accessible.
 
| Need | Description |
|---|---|
| **Shelter** | Having a stable, enclosed sleeping space. |
| **Safety** | Low perceived threat level (wildlife presence, temperature danger, etc.). |
| **Socialization** | Interacting with other tribe members. Isolation reduces sanity over time. |
| **Sanity** | A combined mental health meter. Low sanity causes hallucinations, poor decisions, stat penalties. |
 
### Tier 3 — Self-Actualization (Thriving)
These unlock only when Tiers 1 and 2 are consistently maintained.
 
| Need | Description |
|---|---|
| **Comfort** | Quality of sleeping conditions, food variety, ambient warmth. |
| **Leisure** | Downtime, rest beyond sleep, recreational activities. |
| **Art & Expression** | Creating art, music, storytelling — tribe-wide morale boosts. |
| **Personal Achievement** | Completing personal goals, mastering skills, contributing meaningfully to the tribe. |
 
### Needs & Buffs
- Each tier fulfilled consistently → unlocks **passive buffs** (stamina, speed, focus, skill gain rate, etc.)
- Missing a lower tier **suspends** buffs from higher tiers
- Tier 3 buffs are **powerful** and meaningfully change gameplay — thriving tribes are significantly more capable than merely surviving ones
### Needs & Energy — Streaks
- Consistently meeting Tier 1 needs over multiple days builds **streaks** (e.g. "well-fed 3 days running", "fully rested")
- Active streaks provide **bonus buffs** on top of standard tier buffs — including increases to maximum Energy capacity
- Breaking a streak (missing a meal, a night's sleep, etc.) removes its bonus immediately
---
 
## 5. Time & Energy System
 
### Overview
Every tribe member has two personal **action resources**: **Time** and **Energy**. Both must be sufficient to perform or contribute to any task. Neither can be shared or pooled between members — each person must independently meet the requirements for their portion of the work.
 
---
 
### Day / Night Cycle
 
#### World Clock
- The world runs on a **real-time clock**: **6 seconds IRL = 1 in-game minute**
- A full in-game day = **~2.4 IRL hours** at the default ratio
- The ratio is **adjustable** — will be tuned in playtesting
- The world clock drives: lighting, sky, weather, wildlife behaviour, and task availability (day-only / night-only restrictions)
- In practice the felt day is far shorter than 2.4 hours, because players **Skip Time** through long, low-engagement stretches (especially night) rather than playing them out in real time
#### Two Time Modes
The game runs in two modes — there is no automatic background stepping; time only fast-forwards when players deliberately initiate a Skip:
 
| Mode | When | Behaviour |
|---|---|---|
| **Real-time** | Active play — moving, scouting, hunting, fighting, short tasks | The clock runs continuously at 6 sec = 1 min |
| **Skip Time** | Passing long, safe stretches — sleeping, waiting out weather, long processes | The clock fast-forwards by an agreed amount (see below) |
 
#### Skip Time
Skip Time is the explicit fast-forward action. The **Skip Time button** is always present, and any task flagged "Can skip time" in its definition also surfaces a Skip Time button directly in its UI (opening the same menu).
 
**Initiating a Skip**
- Any player opens the Skip menu and selects an amount of time.
- **Co-op:** the other player is notified but not interrupted — they open the Skip menu when ready, so a skip request never disrupts active play. Both players must select the same amount; only then does the **Continue** button appear. On Continue, the world fast-forwards by that amount.
- **Solo:** no consensus step — the one human selects an amount and confirms.
This reuses the same both-must-agree selection pattern as the death-revert vote (Section 18), and should share one UI component. The difference is intrusiveness: the revert vote is a forced modal; the Skip request is a soft, non-disruptive invitation.
 
During a Skip, NPCs and any idle (AI-controlled) dreamer continue their assigned routines (Section 7) — their need drain and any resource production are computed across the skipped time.
 
**When a Skip Cannot Be Started**
A player cannot initiate or commit to a Skip while in danger:
- In combat
- Severely injured
- Harmable enemies nearby — proximity counts only if an enemy can actually reach and harm the player. If the player is in a safe location the enemy cannot path into (e.g. a sealed shelter), enemies-nearby is false and the Skip is allowed.
In co-op, both players must be skip-eligible for a Skip to proceed.
 
**Need, Buff & Debuff Calculation**
Before a Skip is confirmed, the menu projects how the chosen duration will affect every tribe member — how much each need drains and how buffs/debuffs change (e.g. *"an 8h skip will cost ~X hunger"*). Needs, buffs, and debuffs can each define a guard threshold ("prevent skip above/below X"):
 
- **Dreamers — hard cap.** If the chosen duration would push any dreamer's need or debuff past its guard, the Skip must be shortened before Continue is allowed. Example: if a dreamer's hunger would cross its guard at hour 4, the player cannot select more than 4 hours. A bleed guard at 50% likewise caps the Skip at the point the dreamer would reach 50%.
- **NPCs — warning, not a cap.** If an NPC would cross a guard, players are informed the NPC is at risk; if the Skip could kill that NPC, a death-risk icon is shown on them. The Skip is still allowed — NPCs are mortal, and proceeding is the players' call.
*All guard percentages are starting values, to be adjusted in playtesting.*
 
**Interrupts**
A Skip is interrupted by significant runtime events — a predator arriving, combat, a blizzard or other major weather, and so on. It runs until the chosen duration elapses or an interrupt fires, whichever comes first, then drops everyone back into real-time at that moment. (Predictable need/debuff guards are handled up front as caps; interrupts cover the things that can't be foreseen.)
 
**Skip Types**
 
| Type | Status | Range | Notes |
|---|---|---|---|
| **Short Skip** | Launch | Up to 12h | The everyday fast-forward — sleeping, waiting out weather, short processes. Usable anywhere the player is safe. |
| **Scheduler** | Future | Up to weeks | A long-horizon skip, usable only at a base location, driven by a pre-set daily task plan. |
 
**Scheduler (future feature):**
- Available only in locations designated as a base.
- Players pre-select the tasks each member performs per day — the mandatory ones (sleep, eat, drink) plus base tasks that aren't weather-restricted. For example, indoor crafting continues during a blizzard, but building outdoors, fishing, and using exterior crafting stations are suspended.
- The same daily plan is applied to the NPCs.
- The Scheduler calculates the resources each NPC consumes and produces per day, so players can judge whether a multi-week stay is sustainable before committing.
- All Skip rules above still apply: the Scheduler cannot advance through a day in which a mandatory need can't be met — it halts at the same guard thresholds.
**Skip Presentation**
While time is skipping, the screen shows a **dark overlay** with a clock animation advancing to the endpoint, so players see exactly how much time is passing. Normal play resumes when the Skip completes or is interrupted.
 
#### Day Reset
- The **time budget** (24h) resets at **midnight** each in-game day
- **Energy does not reset** at midnight — it carries over and must be actively recovered through rest and sleep
---
 
### Time
 
#### Daily Time Pool
- Each member starts the day with exactly **24 hours** of available time
- Time is spent by performing actions (gathering, crafting, building, cooking, sleeping, etc.)
- Time only depletes when a player **actively spends it** on a task — idle time does not drain the budget
- Time resets at **midnight** each in-game day
- **24 hours is the absolute cap** — time cannot be extended through any progression, skill, or buff
#### Daylight vs. Night Time
The 24-hour pool is divided into two sub-pools:
 
| Sub-Pool | Summer | Winter |
|---|---|---|
| **Daylight** | ~15 hours | ~8 hours |
| **Night** | ~9 hours | ~16 hours |
 
- Some tasks **require daylight** (e.g. construction, most gathering)
- Some tasks **require night** (e.g. certain rituals, observation tasks)
- Many tasks can be done **either** day or night
- Some nighttime tasks require an **active nearby light source** (e.g. crafting tools, cooking)
#### Task Time Types
Tasks have up to two time components:
 
| Component | Description | Reducible by more players? |
|---|---|---|
| **Prep Time** | Active labor — chopping, assembling, preparing ingredients | ✅ Yes — divided among collaborators |
| **Process Time** | Passive duration — boiling, drying, curing, setting | ❌ No — clock must run, but shifts can be shared |
 
**Example — Cooking a Soup:**
- 1 hour prep (chop, season) → reducible: 2 players = 30 min prep each
- 30 min boiling → non-reducible: started at 12PM, done at 12:30PM regardless
  - 3 players can each "watch" for 10 minutes in shifts, spending only 10 min each
#### Collaborative Time
- Players must be **physically present** at the location to contribute time to a task
- For construction, one player places a **blueprint**; others interact with it to add their hours
- Each player's contribution is logged with a timestamp
---
 
### Energy
 
#### Overview
Energy is the second action resource. Where time measures *availability*, energy measures *physical capacity*. Running out of energy means a tribe member cannot perform demanding tasks — even if they have hours left in the day.
 
#### Energy Pool
- Each tribe member has a **personal Energy pool** (measured in Energy points)
- Every hour spent on a task consumes a certain amount of energy — the rate varies by task type
- Energy is **individual and non-transferable** between tribe members
- When energy runs out, the member cannot begin or continue energy-consuming tasks for the day
#### Energy vs. Time — The Dual Constraint
Both resources must independently be sufficient for a player to contribute their share of a task:
 
| Situation | Can the task proceed? |
|---|---|
| Enough time ✅ + Enough energy ✅ | ✅ Yes |
| Not enough time ❌ + Enough energy ✅ | ❌ No |
| Enough time ✅ + Not enough energy ❌ | ❌ No |
| Player A: lots of energy, no time + Player B: lots of time, no energy | ❌ No — resources are not pooled |
 
> Two players with complementary shortfalls **cannot compensate for each other**. Each player must independently meet both requirements for their contribution to the task.
 
#### Energy Costs by Task Type
Tasks have a defined **energy cost per hour**. More physically demanding tasks cost more energy per hour.
 
| Task Type | Energy Cost / Hour | Notes |
|---|---|---|
| Heavy labor (mining, logging, hauling) | High | May not be completable solo in one day at low energy capacity |
| Moderate labor (hunting, building) | Medium | Manageable for most members with adequate rest |
| Light labor (cooking, crafting, research) | Low | Accessible even with lower energy capacity |
| Passive supervision (watching a fire, guarding) | Minimal | Mostly a time cost |
 
> *Full task list with exact energy values to be compiled separately.*
 
#### Energy Recovery
Energy is restored by:
- **Full Sleep** — primary recovery source (see Sleep System below)
- **Nap** — partial recovery available any time (see Sleep System below)
- **Rest action** — deliberate time-to-energy conversion (see Sleep System below)
- **Food** — certain foods provide energy restoration beyond basic hunger satisfaction (e.g. high-calorie or high-protein meals)
- **Needs fulfilment buffs** — consistently meeting Tier 1 and Tier 2 needs improves base energy recovery rates
#### Sleep System
 
**Nap**
- Available **any time** of day
- Costs **30 minutes** of time budget
- Provides partial restoration of energy and other depleted stats
- Can be performed at: campfire, bed, makeshift shelter, or any furniture that supports the action
**Full Sleep**
- Reserved for **nighttime**
- The player chooses a duration, which is carried out via **Skip Time** (sleeping is a skippable task)
- Major energy recovery; amount scales with duration and quality
- In co-op the partner is notified; the Skip proceeds once both agree, or the partner can stay in real-time while the other sleeps (per the Skip rules above)
**Rest Action**
- A deliberate action that **converts remaining time budget into energy recovery**
- Can be queued before sleep or performed as a standalone action
- Available at: campfire, bed, makeshift shelter, specific furniture
- **Rest quality scales with:**
  - Furniture / location type (bare ground < makeshift shelter < proper bed)
  - Foods consumed before resting (certain meals boost recovery)
  - Active buffs from needs fulfilment
#### Energy Capacity (Maximum Pool)
Unlike time, the **maximum energy pool can be expanded** through:
- Leveling up **Endurance** and **Strength** skills (learn-by-doing)
- Active buffs from needs fulfilment and need streaks
- Certain foods and consumables
#### Reducing Task Costs
Both the time cost and energy cost of tasks can be reduced through:
 
| Factor | Effect |
|---|---|
| **Skill level** | Higher skill → less time and energy per task |
| **Better tools** | Higher-tier tools reduce both costs |
| **Technology** (tech tree) | Unlocking new tech reduces costs of associated tasks |
| **Active buffs** | Needs fulfilment, streaks, and consumables reduce costs |
 
---
 
### HUD & Menu Display
- A **dedicated needs/resources panel** displays all tribe member stats: Time remaining, Energy remaining, all active needs, Vitality, Stamina, Courage, and buff/debuff status
- Key stats (time, energy, critical needs, Vitality, Stamina, Courage) can be shown on the **action HUD** during gameplay
- The HUD display can be **toggled on/off** by the player at any time
---
 
## 6. Character Creation
 
### Overview
Character creation defines a tribe member's attributes, body type, and appearance. It happens at two points:
 
- **At session start** — the host (solo) or both players (co-op) set up the tribe before the action phase begins.
- **Mid-session** — when a second player joins and takes over the free dreamer, they re-open their creation choices once via the Re-spec Token (see Section 7).
Some cosmetic changes are possible during gameplay. The attributes set here are a **starting allocation** — they grow with use afterward (Section 9). Foundational **appearance** (face preset and gender) is locked once set, with two deliberate exceptions: a host revoking a character, and the one-time Re-spec Token (both in Section 7).
 
Who is created, by whom, and the solo vs. co-op creation flow are defined in Section 7. This section covers what is customised.
 
---
 
### Who Is Created
The tribe of 8 is created as **2 dreamers** (the player-controlled, save-protected characters) and **6 ordinary tribespeople** (mortal NPCs). All eight can be fully customised at creation — attributes, body, and appearance — or left to default generation. Players customise their own dreamer; NPCs can be edited by either player (co-op) or by the host (solo). See Section 7 for the full creation flow and editing rules.
 
---
 
### Attributes
Characters do not pick from preset classes. Instead, players distribute points across **six attributes**, split into two groups.
 
**Physical attributes — these also determine body type:**
 
| Attribute | Governs |
|---|---|
| **Strength** | Carry capacity, melee power, heavy-task efficiency |
| **Endurance** | Energy pool size, stamina, resistance to temperature and fatigue |
| **Agility** | Movement speed, combat reflexes, hunting precision |
 
**Mental attributes — these do not affect physique:**
 
| Attribute | Governs |
|---|---|
| **Intellect** | Research efficiency, dream recall, artifact reading, and complex crafting (Sections 8 & 9) |
| **Charisma** | Socialisation, tribe morale, couple/social bonuses, and future interaction with other tribes (Sections 4 & 7) |
| **Perception** | Tracking, reading natural weather and wildlife signs, foraging yield, and spotting threats and resources (Sections 2 & 9) |
 
Attributes are a starting allocation and **grow with use** as a character performs related actions; all skills likewise begin at baseline and grow through use (see Section 9).
 
**Starting allocation.** Every attribute begins at a **baseline**, so no character is incompetent at anything from the outset. The player then distributes a **shared pool** of points across all six attributes as they see fit — a point spent on one attribute is unavailable to another, so a focused spread is stronger than an even one, but the choice is left to the player. Since attributes grow with use, this allocation is only a starting point, not a permanent identity.
 
> *Exact baseline values, pool size, and attribute tiers are tuning decisions for development.*
 
---
 
### Body Type
Body type is derived from the **ratio of the three physical attributes (STR / END / AGI)** — mental attributes have no effect on physique. Because attributes **grow with use** (Section 9), body type is **dynamic**: as the ratio shifts over a playthrough, the character's build changes to match. Each body type also carries a **body-type buff** describing its bonuses and drawbacks (e.g. a heavy/stocky build trading movement speed for carry capacity and melee power).
 
| Physical Profile (STR / END / AGI ratio) | Body Type |
|---|---|
| High Strength + High Endurance + Low Agility | Heavy / Stocky build |
| High Strength + High Agility + Low Endurance | Lean / Athletic build |
| High Endurance + High Agility + Low Strength | Medium / Wiry build |
| Balanced across all three | Average build |
 
- At launch, a **small set of 3D models** covers the main ratio bands; a character snaps to the nearest model as their ratio changes. Additional models are added progressively.
> *Exact ratio thresholds, body-type buffs, and the model set are to be refined during development.*
 
---
 
### Appearance Customisation
 
**Phase 1 — Available at Launch:**
- Skin tone
- Hair style and colour
- Eye colour
- Preset face selection (a set of distinct facial presets to choose from)
**Future Update:**
- Full facial feature customisation (sliders / detailed editing)
---
 
### In-Game Cosmetic Changes
Some appearance changes can be made **during gameplay** through in-game actions:
- **Hair trimming / restyling** — requires appropriate tools or a skilled tribe member
- **Tattoos** — unlockable through cultural/tech progression
- Other cosmetic unlocks tied to game progression
**Foundational appearance** (face preset, gender) **cannot be changed** once set, except when:
- the host revokes a character and reopens it (Section 7), or
- a joining player uses their one-time Re-spec Token (Section 7).
---
 
### Mid-Session Joining & Reconnecting
 
A second player who joins mid-session takes over the free dreamer and may re-open its appearance and attributes once, via the Re-spec Token. Learned skills, buffs/debuffs, and inventory are all preserved. (Full flow in Section 7.)
 
Reconnecting players are automatically reassigned to their original character in its current state. If their character was revoked while they were offline, they are assigned to an available dreamer.
 
---
 
### Gender
 
Gender is set per character at creation (can be changed using the token received as late joiner), and default generation produces a mix. There is no forced gender balance across the tribe — composition and pairings are entirely free.
 
**Phase 1:** gender is primarily cosmetic, with the exception of the couple mechanic (bonuses apply regardless of pairing gender — see Section 7).
 
**Future update:** gender and Charisma together will influence social interactions with other tribes' NPCs once the multi-tribe system is introduced.
 
---
 
## 7. Tribe & NPC System
 
### Tribe Composition
 
The tribe consists of **8 members** at game start, split into two fundamentally different kinds:
 
| Type | Count | Nature |
|---|---|---|
| **Dreamers** | 2 | The special, time-displaced characters. Controlled by humans or by AI. Carry the dream ability and save protection (Section 18). |
| **Tribespeople** | 6 | Ordinary NPCs. No dream ability, no save protection — their death is permanent. |
 
There are always exactly two dreamers. They cannot be created, replaced, or added to — the dream ability and the entire save/death system (Section 18) belong to them alone.
 
The other six are mortal: neglect, danger, or a bad auto-task can kill them for good, permanently shrinking the tribe.
 
The tribe can grow to a maximum of 10 by recovering up to 2 additional survivors from the world. Found survivors are always ordinary tribespeople — never new dreamers.
 
> The dreamer/mortal split is the backbone of the tribe. Humans always play dreamers; everyone else is a managed NPC whose loss is real and irreversible.
 
---
 
### Control Model
 
**Solo (1 player):**
- The player chooses one dreamer as their main and can switch to the other dreamer at any time with full control.
- The dreamer not currently being controlled runs on AI — following its assigned tasks and routines — but keeps its save protection (a dreamer is protected whether human- or AI-controlled).
- The player also manages the six tribespeople (tasks, routines, needs).
**Co-op (2 players):**
- Each player controls one dreamer.
- The six tribespeople are managed by either player (shared).
**Disconnect / rejoin:** if the second player leaves, their dreamer reverts to AI control and solo rules resume; on rejoin they reclaim that dreamer in its current state (see Section 21).
 
---
 
### Session Start & Character Creation
 
Both dreamers are always created at session creation, so a free dreamer always exists to hand to a second player later.
 
**Starting solo:**
- The host can edit every character — both dreamers and all six NPCs — or accept the default generation and skip straight in.
**Starting in co-op (both players present):**
- Both players enter a shared character-creation screen.
- Each player may edit only their own dreamer, plus any of the six NPCs.
- NPC editing uses a per-character lock: while one player has an NPC open for editing, that NPC is locked to the other player; it frees the moment the first player exits its edit mode.
- The host starts the action phase once both players are ready.
---
 
### Joining an Existing Session
 
When a second player joins a session the host has already started:
 
1. The host opens the game for multiplayer.
2. The host selects which dreamer to keep; the other dreamer is offered to the joining player.
3. The joining player inherits that dreamer's full current state — learned skills, active buffs and debuffs, and inventory all carry over unchanged.
4. The joining player also receives a **Re-spec Token** in their inventory.
#### The Re-spec Token
 
- Has no weight or volume and occupies no meaningful inventory space.
- Is one-time use — consumed when used, and usable at the player's leisure rather than forced at the moment of joining.
- On use, it re-opens the dreamer's creation choices: appearance and core attributes (Strength, Agility, Intellect, Endurance, etc.).
- It does not alter learned skills, buffs/debuffs, or inventory — all earned progress is preserved.
- It is a deliberate exception to the Section 6 rule that foundational appearance is locked once set.
---
 
### The Chieftain
 
The Chieftain is the tribe's administrative leader. By default this is the host (a dreamer); the role can be passed to the other player or to an NPC.
 
**Chieftain powers:**
- Calls tribe migration — only the Chieftain can relocate the whole tribe to a new map.
- Assigns couples — sets and changes couple relationships via the Couple Assignment item in camp.
- Opens the game for multiplayer and assigns the free dreamer to a joining player.
- Tribe administration — overall direction and tribe-wide decisions.
**Rules:**
- Default: the host holds the role at session start.
- The role can be passed to the other player or to an NPC at any time.
- If the Chieftain is an NPC not assigned to a party, any player can issue Chieftain commands through it; if it is an NPC in a specific player's party, only that player can.
- The host can always reclaim the Chieftain role.
---
 
### Couple Mechanic
 
Couples are assigned by the Chieftain and grant passive buffs to their members.
 
| Situation | Bonus |
|---|---|
| **Sleeping next to partner** | Bonus energy recovery + sanity restoration |
| **Fighting near partner** | Combat performance buff |
 
The two dreamers are not a couple by default. Couples are formed freely among any tribe members — dreamers, NPCs, or a mix.
 
The game is aimed mostly at real-life couples, but the system supports two friends equally well: each can pair their dreamer with a mortal tribesperson, giving every player their own in-game couple.
 
Couples may be any gender pairing and may be polyamorous. Bonuses apply on a diminishing-returns scale — a pair of two receives the full bonus; larger groups share a smaller per-person bonus but are never penalised.
 
> *Additional couple mechanics are planned for future updates.*
 
---
 
### NPC Management
 
Each of the six tribespeople has its own time pool, needs profile (hunger, thirst, warmth, sleep), and inventory.
 
**Assignment modes:**
 
| Mode | Description |
|---|---|
| **Party-assigned** | Assigned to a specific player, who manages the NPC's time and needs manually. |
| **Auto-task** | Given a repeating task (chop wood, cook, gather water, etc.) that it performs autonomously, spending time accordingly. |
 
On reassignment, already-spent time is locked and cannot be reclaimed.
 
NPCs on auto-tasks are not micromanaged but still require their needs to be met.
 
**NPC Manager Interface** — a per-NPC screen to configure:
- Time allocation schedule (e.g. 1h eat, 8h sleep, 10min drink, remainder on the assigned task)
- Food preferences (which foods the NPC will eat)
- Ration levels (half / full / double, depending on supply)
**Camp storage:** the tribe shares centralized camp storage. An NPC must be assigned to camp to draw from it; NPCs sent on distant, multi-day tasks must be pre-provisioned, or they will suffer need penalties and can die away from camp.
 
---
 
### NPC Routines
 
Players can give each NPC a daily routine — a pre-set task sequence it runs automatically.
 
> *Example: Sleep 8h → Cook 1h → Gather Wood 2h → Guard Camp 4h → Rest 1h*
 
- Configured in the NPC Manager Interface.
- The NPC works the routine in order each day, spending time and energy.
- Especially useful for NPCs left at camp while players are elsewhere.
- If a routine step can't be performed (missing materials, no energy), the NPC skips it and moves on.
---
 
### Autonomous Behaviour (Survival Override)
 
If a mortal NPC is left unattended for several in-game days without an adequate routine, it begins making its own decisions:
 
1. **Survival first** — if its assigned routine doesn't cover basic needs, it overrides those orders to keep itself alive.
2. **Comfort second** — once survival is handled, it seeks basic comfort tasks.
3. **Idle** — if all needs are met and nothing is assigned, it idles.
Prolonged neglect degrades an NPC's health, sanity, and morale; a sufficiently neglected NPC may desert or die permanently. This is deliberate — the tribe shrinks as a consequence of negligence, not from arbitrary game-over rules.
 
An idle dreamer is different. In solo play the non-controlled dreamer follows the same AI routines and self-preservation logic, but it cannot desert or die permanently — its death routes through the dream system (rescue / Wake Up / Game Over) in Section 18, never NPC permadeath.
 
---
 
## 8. Tech Tree & Dreams
 
### Overview
Technology in the game is discovered organically through **doing** — not from a menu of available recipes. Players do not start knowing what is possible. They discover higher technologies through **dreams** triggered by their in-game actions, then work backward through prerequisites to unlock them.
 
### How Tech Discovery Works
 
1. **Action triggers a dream** — spending significant time performing a task (e.g. chopping wood) triggers a dream showing a more advanced version (e.g. a steel axe)
2. **Dream unlocks a tech node** — the higher tech appears on the tech tree, but its prerequisite nodes remain locked
3. **Prerequisites must be found separately** — players must discover lower-tier techs through their own exploration and crafting before they can reach the dreamed tech
4. **Research action** (unlocked mid-game) — players can spend time actively trying to remember how a dreamed tech works, potentially triggering a new dream that unlocks a prerequisite node
### Tech Tree Structure
- The tree is **not fully visible** at start — only discovered and adjacent nodes are shown
- Techs are grouped by domain: **Tools, Shelter, Food, Clothing, Medicine, Crafting, Knowledge**
- Higher-tier techs require combinations of lower-tier prerequisites
- Some techs require **specific skills** to be at a certain level before they can be attempted
### Research Action
- Unlocked at a certain point in the tech tree
- A player spends time in a **"research" state** at an appropriate location (camp fire, crafting area)
- Has a chance of triggering a **targeted dream** — a memory fragment that fills in a missing prerequisite
- Can be attempted multiple times; success is not guaranteed
### Memory & Modern Knowledge
The dreamers are time-displaced modern humans. Their fading memories of the modern world are the source of their technological intuition. The dream system is the mechanical expression of this — they don't consciously remember how to smelt copper, but part of them *knows* it's possible, and doing enough primitive work unlocks that buried knowledge.
 
---
 
## 9. Skill System
 
### Overview
Every tribe member (dreamer and NPC) progresses through three layers that grow together from the same actions: attributes, skills, and skill trees. Performing an action distributes experience across all three at once, and advanced skill benefits are gated behind attribute levels — so how a character is played shapes who they become, producing distinct builds rather than a single optimal path.
 
---
 
### The Three Layers
 
| Layer | What it is | How its bonuses apply |
|---|---|---|
| **Attributes** (Section 6) | The six core stats — STR, END, AGI, INT, CHA, PER. They grow through use. | Act as requirements that gate advanced skill benefits, and feed their own effects (carry, body type, etc.). |
| **Skills** | Specific proficiencies — e.g. Spear Throwing, Tracking, Traps. Each levels up individually. | Each skill level grants bonuses to that skill alone. |
| **Skill Trees** | The categories that group related skills — e.g. Hunting groups Spear Throwing, Tracking, and Traps. The tree levels up too. | Each tree level grants bonuses shared across every skill in that tree. |
 
*(Exact bonus values for skill and tree levels are defined in playtesting.)*
 
---
 
### How XP Is Earned
 
A single action grants XP to multiple targets at once. Each skill defines, in its data, a set of key–value pairs — what it grants XP to and how much — and each value is a range (min–max), so gains vary slightly per action.
 
A single action typically feeds: the skill performed, its parent tree, sometimes a second related tree, and one or more attributes.
 
**Example — one Spear Throw grants:**
 
| Target | XP (range) |
|---|---|
| Spear Throwing (skill) | 5–10 |
| Hunting (tree) | 1–5 |
| Combat (tree) | 1–3 |
| Strength (attribute) | 2–4 |
| Agility (attribute) | 1–4 |
 
This is the engine of build diversity. Throwing spears builds Spear Throwing, the Hunting and Combat trees, and Strength and Agility — but it does nothing for Tracking or Traps, which feed Perception and Intellect instead. A patient trap-and-stealth hunter and a brute-force spear hunter live in the same Hunting tree but train completely different skills and attributes.
 
---
 
### Bonuses & Attribute Gating
 
- **Skill level** → bonuses to that specific skill.
- **Tree level** → bonuses shared across all skills in the tree.
- **Attribute requirements** → advanced skill benefits (passive effects and unlockable active abilities) require a minimum level in their governing attribute(s). A player can pour XP into Spear Throwing, but the high-end throwing passives won't take effect until their Strength is high enough.
Because each action grows both a skill and its governing attributes together, a focused build naturally raises the attributes its advanced skills require. Branching into a different style means deliberately training different attributes — which is what makes specialised characters meaningfully different from one another.
 
---
 
### Skill Trees & Their Skills
 
The trees and a representative sample of their skills (full lists to be expanded in development):
 
| Tree | Skills (examples) | Primary Attributes |
|---|---|---|
| **Gathering** | Wood chopping, stone gathering, foraging, fishing | STR, PER |
| **Hunting** | Tracking, spear throwing, bow use, trapping | PER, AGI, STR, INT |
| **Crafting** | Tool making, weapon crafting, rope making, pottery | INT, AGI |
| **Building** | Construction, structural quality | STR, INT |
| **Cooking** | Food preparation, recipe discovery, preservation | INT, PER |
| **Medicine** | Wound treatment, herb identification, illness care | INT, PER |
| **Knowledge** | Research, dream recall, artifact reading | INT |
| **Combat** | Melee, ranged, dodging, animal handling | STR, AGI |
 
The "primary attributes" above are indicative; each individual skill defines its own attribute gate(s) in its data — which is why two skills in the same tree (e.g. Spear Throwing vs. Tracking) can require entirely different attributes.
 
> *Charisma currently has no skill tree — one will be added in a future update.*
 
---
 
### Skills, Trees & NPCs
 
- NPCs progress attributes, skills, and trees the same way dreamers do.
- A highly skilled NPC woodcutter gathers more wood per hour than an unskilled one; deciding which NPCs specialise in which trees is a core tribe-strategy decision.
---
 
### Body Type
 
A character's body type is derived from the ratio of their physical attributes (STR / END / AGI). Because those attributes grow with use, body type can shift over a playthrough as the ratio changes, and each body type carries a body-type buff describing its bonuses and drawbacks (for example, a heavy/stocky build trading movement speed for carry capacity and melee power). Defined in full in Section 6; exact ratios and buff values are set in playtesting.
 
At launch, a small set of 3D models covers the main ratio bands, and a character snaps to the nearest one as their ratio changes. Additional body-type models are added progressively.
 
---
 
## 10. Inventory & Carrying
 
### Overview
Every tribe member has a personal inventory. The inventory is **slot-based** in the UI (one item or stack per slot), but each item contributes to three tracked totals: **weight**, **width**, and **height**. Bags and carry methods each have limits on all three dimensions.
 
### Inventory UI
- Simple grid of slots — one item or item stack per slot
- Panel displays running totals: **Total Weight | Total Width | Total Height**
- Totals update dynamically as items are added or removed
- Players cannot carry more than their bag's limits across all three dimensions
### Weight & Carry Capacity
- Each player has a **maximum carry weight** based on their **Strength** stat
- Exceeding carry limits is not possible, but approaching the maximum causes **debuffs**:
  - Reduced movement speed
  - Increased energy drain
  - Stamina penalties
### Bags & Containers
- Players can equip **backpacks and pouches** — each has its own slot grid and weight/dimension limits
- Bags can be **dropped in the world** and picked up later
  - Dropped bags persist in place
  - Risk: attract wildlife; can be damaged by weather
### Carry Methods (Progression-Unlocked)
 
| Method | Capacity | Notes |
|---|---|---|
| **Backpack** | Medium | Default carry. Worn by the player. |
| **Pouches** | Small | Supplementary slots. Worn on body. |
| **Shoulder carry** | 1 log | Single large item carried by hand. |
| **Rope drag** | Multiple logs | Drag items along the ground. Slows movement. |
| **Travois** | Large | A pulled frame. Requires both hands. More capacity. |
| **Sled** | Very Large | High capacity. Slow. Essential for full tribe migration. |
 
> Travois and sled must be crafted and require unlocking through the tech tree.
 
---
 
## 11. Durability System
 
### Overview
Durability is a universal property shared by **tools, weapons, clothing, equipment, and buildings**. Every item degrades with use, time, and environmental exposure. Higher-quality materials last longer and perform better, but all items require maintenance or eventual replacement.
 
---
 
### What Has Durability
 
| Category | Examples |
|---|---|
| **Tools** | Axes, picks, knives, needles, fishing rods |
| **Weapons** | Spears, bows, arrows, clubs |
| **Clothing** | Furs, boots, gloves, insulated wraps |
| **Equipment** | Waterskins, bags, ropes, pouches |
| **Buildings** | Shelters, storage structures, crafting stations |
 
---
 
### Material Tiers & Impact
The material a tool or item is made from affects both its **performance** and its **durability**:
 
| Material Tier | Performance | Durability |
|---|---|---|
| **Stone / Bone / Hide** | Low | Low — degrades quickly |
| **Copper** | Medium | Medium |
| **Iron / Steel** | High | High |
| **Advanced materials** | Very High | Very High |
 
> *Full material tier list to be defined as the tech tree is built out.*
 
Higher-tier materials reduce both the performance cost (time/energy per task) and the rate of durability loss per use.
 
---
 
### Durability Loss
- Durability depletes **during use** at a rate determined by the task type and material tier
- Environmental exposure also causes passive durability loss over time (especially for clothing and buildings in Very Cold zones)
- Buildings degrade during harsh seasons — decay rate is inversely proportional to build quality (see Section 13)
---
 
### Pre-Task Durability Check
At the **start of any task**, the game performs an automatic check:
 
1. Reads the tool's current durability
2. Calculates the **durability loss per minute** for the assigned task
3. Determines **how many minutes the tool can last** before breaking
4. If the tool will break before the task completes, the player is **notified upfront**:
   > *"Your axe will last approximately 2 hours before breaking. The task requires 4 hours. You will need to repair or replace it to complete the task."*
This gives the player full information to decide whether to:
- Proceed with the partial task (2 hours of progress saved)
- Repair the tool first
- Switch to a different tool
- Assign another tribe member with a better tool
### Task Interruption on Break
If a player proceeds despite the warning and the tool breaks mid-task:
- The task **stops at the point of breakage**
- Time invested up to that point is **logged and preserved** — partial progress is kept where applicable
- The player must **resume with a repaired or replacement tool** to continue
- No time is refunded — the time spent was genuinely used
**Example:**
> A 4-hour chopping task. Tool breaks at hour 2. The player spent 2 hours and made 2 hours of progress. To finish, they must repair or replace the tool and invest the remaining 2 hours.
 
---
 
### Repair System
Items can be repaired rather than replaced. Repair actions vary by item type:
 
| Item | Repair Actions |
|---|---|
| **Axe** | Sharpen edge (restores partial durability); replace wood handle; replace axe head |
| **Clothing** | Patch tears with hide or sinew |
| **Bow** | Re-string; reinforce limbs |
| **Waterskin** | Patch with hide and resin |
| **Building** | Reinforce walls; replace structural elements |
 
- Repair costs **time, energy, and materials**
- Repair restores durability — amount depends on repair type and skill level
- Higher repair skill → better durability restored per repair action
- **Broken items** (0 durability) can be **salvaged** for raw materials rather than repaired
---
 
### Salvage
Items at 0 durability that cannot be repaired (or are not worth repairing) can be **broken down**:
- Yields partial raw materials (stone chips, copper fragments, hide scraps, sinew, etc.)
- Salvage is a crafting action that costs a small amount of time and energy
- Recovered materials can be used to craft or repair other items
---
 
## 12. Crafting System
 
### Overview
Crafting is the process of turning raw materials into usable items — tools, weapons, clothing, food, structures, equipment, and more. Every craftable item is defined by a **recipe** that specifies exactly what is needed to make it. Some crafting is done entirely by hand; other recipes require a dedicated **crafting station** to be built first.
 
---
 
### Recipe Format
Every recipe in the game is defined by the following fields:
 
| Field | Description |
|---|---|
| **Name** | The item or output being crafted |
| **Station Required** | The crafting station needed, or "By Hand" if none |
| **Required Materials** | Raw materials and quantities consumed |
| **Output** | What is produced (item, quantity, quality tier) |
| **Time Cost** | Hours of time budget required |
| **Energy Cost** | Energy consumed during crafting |
| **Skill Required** | Minimum skill level (if any) to attempt |
| **Tech Required** | Tech tree node that must be unlocked first |
 
> *The full recipe list will be maintained as a separate reference document, built out alongside the tech tree.*
 
**Example Recipes:**
 
| Name | Station | Materials | Output | Time | Energy |
|---|---|---|---|---|---|
| Flint Knife | By Hand | 1 flint, 1 knapping stone | Flint knife (1) | 30 min | Low |
| Bone Needle | By Hand | 1 bone fragment | Bone needle (1) | 20 min | Low |
| Copper Axe Head | Kiln | 2 copper ore | Copper axe head (1) | 2 hrs | High |
| Waterskin | By Hand | 2 hide, 1 sinew | Waterskin (1) | 1 hr | Low |
 
---
 
### Hand Crafting
Some items can be crafted anywhere, without a station, using only the materials in the player's inventory and an appropriate hand tool:
 
- **No station required** — can be done at any location
- Often requires a **hand tool** as part of the process (e.g. knapping requires a knapping stone; stitching requires a bone needle)
- The hand tool is **not consumed** by the recipe but takes **durability damage** from use
- Suitable for simple, early-game recipes (stone tools, cordage, basic clothing patches)
---
 
### Crafting Stations
More complex recipes require a dedicated station to be built at camp. Stations enable access to higher-tier crafting recipes and unlock further branches of the tech tree.
 
#### Station Properties
Each crafting station has:
- A **build recipe** (materials + time to construct it, like any structure)
- A **recipe list** — the set of crafting recipes it enables
- A **durability** rating (degrades with use and environmental exposure)
- A **tech tree node** that must be unlocked before it can be built
#### Station Examples
 
| Station | Enables | Notes |
|---|---|---|
| **Knapping Station** | Higher-quality stone tools | Improves yield vs. by-hand knapping |
| **Tanning Rack** | Leather and treated hides | Required for advanced clothing |
| **Smoking Rack** | Smoked/preserved foods | Food preservation tech |
| **Kiln** | Smelting copper, firing clay | Unlocks metal age crafting and pottery |
| **Forge** | Iron and steel tools/weapons | High-tier metal crafting |
| **Workbench** | Composite tools and weapons | Assembly of multi-part items |
| **Loom** | Woven textiles | Unlocks cloth-based clothing and bags |
 
> *Full station list will expand alongside the tech tree.*
 
#### Station Persistence Between Migrations
- Crafting stations **remain on the map** when the tribe migrates away (like permanent structures)
- They can be **damaged** if left unprotected:
  - Stations left **outdoors** degrade faster in cold seasons
  - Stations left in a **cave or enclosed building** degrade much slower
- A damaged station must be **repaired** before it can be used again
- Repair uses the same durability repair logic as tools (see Section 11)
---
 
### Tech Tree Integration
Crafting and the tech tree are tightly linked in both directions:
 
- **Tech tree unlocks stations:** A station can only be built once its corresponding tech node is discovered (via dreams or research)
- **Stations unlock tech:** Building and using a station can trigger new dreams, opening higher branches of the tech tree
- **Recipes unlock via tech:** Individual recipes within a station's list may require their own tech nodes before they appear
- This creates a natural progression: *dream about a concept → unlock the station → use the station → dream about higher techniques*
---
 
## 13. Water System
 
### Overview
Water is a critical Tier 1 survival need. All water consumed must be **safe to drink** — raw collection from most sources carries a contamination risk. Processing water (melting, boiling) is required for safety. Water also interacts heavily with the temperature system — it freezes in cold environments and must be stored carefully during transport.
 
---
 
### Water Sources
 
| Source | Available In | Notes |
|---|---|---|
| **Snow** | All maps in winter / cold zones | Gathered by hand; must be melted and boiled before drinking |
| **Ice** | Frozen rivers, cold zones | Gathered as blocks; must be melted and boiled |
| **Rivers (unfrozen)** | Chilly zones, partial thaw | Collected directly; must be boiled |
| **Springs** | Select locations | Cleanest natural source; very low contamination risk (<1%) |
| **Salt water** | Coastal areas (future maps) | Requires distillation — tech tree unlock |
| **Rain / Precipitation** | N/A | Precipitation in this world is always snow — no liquid rain |
 
---
 
### Processing Pipeline
 
Raw water must be processed before it is safe to drink:
 
```
Snow / Ice / River Water
        ↓
   Melt (heat source required)
        ↓
   Boil (fire + container required, costs time)
        ↓
   Safe Drinking Water
        ↓  (optional)
   Freeze in open container → Clean Ice (safe, re-meltable)
```
 
- Each step costs **time and energy**
- Multiple players can collaborate on prep steps (e.g. packing snow) but the boiling process time cannot be reduced
- **Clean ice** = boiled water that has been re-frozen; safe to melt and drink without re-boiling
- Clean ice can be transported in bulk as a reliable water supply
---
 
### Contamination & Illness
 
Drinking unprocessed or partially processed water carries a **contamination chance** that varies by source quality:
 
| Source | Contamination Chance (Base) |
|---|---|
| Spring (direct) | <1% |
| Clean melted snow (unboiled) | ~5% |
| River water (unboiled) | ~10% |
| Stagnant / poor-quality water | Higher (to be tuned) |
 
- Each illness has its **own independent roll** — multiple afflictions can occur simultaneously
- Example illnesses: intestinal worms, dysentery, diphtheria, common cold
- Contamination chance is a **starting value** — will be adjusted based on playtesting
- Illness ties into the broader **Disease & Illness system** (Open Question #9)
**Snow consumed directly (without melting):**
- Drops **body temperature** immediately
- Carries the same contamination risk as unboiled water
- Emergency use only — significant survival penalty
---
 
### Storage Containers
 
| Container | Capacity | Notes |
|---|---|---|
| **Waterskin** | Small | Basic; equippable (see below). Available from start. |
| **Skin barrel** | Large | Camp storage; not wearable. |
| **Wood bowl** | Very small | Open container; freezes quickly. |
| **Clay pot** | Medium | Requires Pottery tech unlock. More durable. |
| **Insulated skin container** | Small–Medium | Keeps water from freezing longer. Tech unlock required. |
 
---
 
### Freezing Mechanic
 
Water freezes if left in containers at or below freezing temperature:
 
- **Open containers** (wood bowls, skin barrels) freeze quickly in cold environments
- **Waterskins** freeze based on where they are worn:
| Worn Position | Freezing Behaviour |
|---|---|
| Under clothing (against body) | Does not freeze — body heat keeps it liquid |
| Over clothing (external) | Freezes slowly in Cold zones; quickly in Very Cold |
 
- **Insulated containers and backpacks** slow freezing significantly — higher-tier tech unlocks
- Frozen water in a waterskin must be melted again before drinking (returns to clean water if originally boiled)
---
 
### Waterskin as Equippable Item
 
The waterskin is both a **storage container** and an **equippable item**:
 
- Comes with a **carrying string** — worn as an equippable slot (body layer)
- Can be worn **under clothing** (freeze-protected, uses an inner layer slot) or **over clothing** (accessible, but exposed to cold)
- Adds to the player's total **carry weight**
- Multiple waterskins can be carried but each occupies weight and a carry slot
---
 
### Bulk Water Transport
 
For migration and long-distance travel, water can be transported in bulk as:
 
| Form | Method | Notes |
|---|---|---|
| **Clean ice blocks** | Freeze boiled water in open containers | Safe — melts to drinkable water. Best bulk option. |
| **Snow** | Gathered loose or packed | Requires melting + boiling before drinking |
| **Dirty ice** | Frozen unprocessed water | Requires melting + boiling + risk of contamination |
 
- Bulk ice/snow is heavy — factors into migration load planning
- Clean ice is the preferred long-term storage format for migrations
---
 
## 14. Structures & Building
 
### Overview
Structures are built using a **blueprint system** and cost time (and materials) to complete. As time is invested they progress through visible construction stages, and they persist on maps after the tribe migrates away.
 
### Blueprint System
- A player places a blueprint at a chosen location (requires materials to be nearby or in inventory).
- Tribe members — dreamers and NPCs alike — interact with the blueprint to contribute time.
- All contributors must be physically present at the site.
- As cumulative time is invested, the structure progresses through visible construction stages.
### Construction Stages
Each structure has milestone states tied to completion percentage:
 
| Completion | Visual State |
|---|---|
| 0% | Blueprint outline on the ground |
| 25% | Foundation / perimeter visible |
| 50% | Half-built structure |
| 75% | Nearly complete |
| 100% | Finished, functional |
 
A structure's current stage is simply part of the world state, captured in save snapshots like everything else. If the dreamers Wake Up (Section 18), the structure reverts to whatever stage it held at the chosen checkpoint, along with the rest of the world — no special per-object tracking is required.
 
### Structure Types
 
| Type | Permanence | Notes |
|---|---|---|
| **Tents / Temporary Shelters** | Temporary | Dismantled before migration. Quick to build. |
| **Permanent Structures** | Persistent | Remain on the map indefinitely. Take longer and more resources. |
| **Cave Shelters** | Natural | Pre-existing; can be improved. |
 
### Structure Decay
- Permanent structures on **Cold or Very Cold** maps during harsh seasons may decay
- Decay chance is inversely proportional to structure quality (better-built = more resilient)
- Decayed structures can be repaired if the tribe returns
- Fully ruined structures must be rebuilt
---
 
## 15. Wildlife & Combat
 
### Wildlife Overview
Animals populate all six maps and respond dynamically to **temperature**, **resource availability**, and **human presence**. Each animal has an **AI Aggression** tendency — how readily it seeks conflict. Its Courage, Vitality, and Stamina are the same universal combat stats every living entity uses (see Combat, below).
 
---
 
### Animal Behavior Types
 
| Type | Behavior |
|---|---|
| **Small lone herbivores** | Low courage. Flee immediately on spotting humans. |
| **Herds of herbivores** | High group courage. Will defend the herd as a pack. |
| **Large herbivores (e.g. mammoths)** | Territorial. Attack if approached too closely. |
| **Small/timid carnivores** | Avoid humans. May flee or stand off depending on courage. |
| **Hunting carnivores** | Actively stalk and hunt humans. |
| **Apex predators** | Always attack within territory. Do not flee. |
 
---
 
### Temperature Effects on Wildlife
- As a map cools and **herbivores migrate** (via spawn changes), carnivores lose their primary food source.
- Carnivores on cooling maps become **more aggressive** and their **courage increases**.
- In **Very Cold maps**, remaining carnivores actively hunt — they stalk from a distance, build tension, and wait for an opportunity.
- In **food-abundant maps**, most carnivores prefer to avoid humans (except apex predators).
---
 
### Fire as a Defense Tool
All animals are afraid of fire, but the **size of the fire** determines which animals are deterred:
 
| Fire Source | Lone Wolf | Wolf Pack | Bear | Mammoth |
|---|---|---|---|---|
| **Torch** | ✅ Flees | 🔶 Keeps distance (tries to flank) | ❌ Unaffected | ❌ Unaffected |
| **Small Campfire** | ✅ Kept at bay | ✅ Kept at bay | ❌ Unaffected | ❌ Unaffected |
| **Large Fire** | ✅ | ✅ | ✅ | ✅ |
| **8 people + big torches** | ✅ | ✅ | ✅ | ✅ |
 
- A pack of wolves will try to **surround and attack from behind** when only a torch is present.
- Group cohesion (staying together with light sources) is a meaningful defensive strategy.
---
 
### Combat System
 
> ⚠️ *Combat is intentionally basic at launch and will be expanded in future updates. All thresholds, chances, and effects below are starting values, tuned in playtesting.*
 
Combat is **real-time**. Weapons include spears, bows, and melee tools. Every living entity — dreamers, NPCs, and wildlife alike — shares the same three combat stats.
 
---
 
#### Universal Combat Stats
 
| Stat | What it does | Driven by |
|---|---|---|
| **Vitality** | The entity's life pool. Damage and certain afflictions reduce it. Once it drops below the incapacitation threshold, there is a chance the entity becomes incapacitated. | — |
| **Courage** | Morale. Applies buffs and debuffs: a frightened entity fights worse (e.g. reduced damage), and below a threshold it attempts to flee. Some injuries raise courage — more damage but less agility (a cornered / berserk effect). Actions, buffs, and injuries all shift it. | — |
| **Stamina** | How long the entity can keep fighting — a short-term meter that depletes with combat exertion (attacking, dodging, sprinting) and regenerates between exchanges. A tired entity tires faster in a fight. | Endurance attribute + current Energy |
 
Stamina is distinct from the daily Energy budget (Section 5): Energy is the day's overall action capacity, while Stamina is the moment-to-moment combat meter whose size scales with Endurance and how much Energy remains.
 
---
 
#### Afflictions (Combat Debuffs)
 
A hit can damage Vitality directly, and/or has a chance to inflict an **affliction** — a debuff that may:
- Reduce stats and skills,
- Slowly drain Vitality over time (e.g. bleeding, infection), and/or
- Be incapacitating (see below).
These are the same afflictions the rescue system treats (bleeding, broken limb, hypothermia, poison — Section 18).
 
---
 
#### Incapacitation & Recovery
 
An entity becomes **incapacitated** when either:
- its Vitality drops below the incapacitation threshold (a chance, not a certainty), or
- it suffers an incapacitating affliction — possible even at full Vitality (e.g. a clean blow that stuns or breaks a limb).
An incapacitated entity can recover, and how depends on the incapacitating affliction:
- **Self-resolving** — passes on its own after a short time (e.g. a stun or knockdown).
- **Intervention-required** — needs external help to recover (e.g. stopping a bleed, resetting a limb, administering an antidote).
The consequence of incapacitation depends on the entity:
 
| Entity | Incapacitated outcome |
|---|---|
| **Dreamer** | Enters the Section 18 flow. A self-resolving incapacitation simply passes; an intervention-required one starts the rescue window — recovered if treated in time, otherwise Wake Up. |
| **NPC** | Recovers if its affliction self-resolves or a tribe member treats it in time; otherwise it dies permanently (Section 7). |
| **Wildlife** | Can be finished or captured while down — a clean finish improves harvest quality (Section 16). If left alone, it may recover and flee. |
 
---
 
#### Combat Performance
 
Effectiveness — accuracy, timing, damage, and defense — is driven by the **Combat skill tree** and its governing attributes (Strength, Agility) per Section 9, then modified by the entity's current Courage and Stamina. A confident, well-rested fighter performs far better than a frightened, exhausted one.
 
---
 
## 16. Animal Harvesting
 
### Overview
Once an animal is killed, players must butcher it to extract usable resources. Harvesting is divided into **distinct actions**, each with its own time and energy cost. The **kill method** directly affects the quality of certain outputs — rewarding deliberate, precise hunting decisions.
 
---
 
### Harvest Actions
 
Each action is performed separately and must be completed by a player at the carcass location:
 
| Action | Output | Notes |
|---|---|---|
| **Skin** | Hide / leather | Quality heavily affected by kill method (see below) |
| **Harvest Meat** | Raw meat | Quality minimally affected by kill method |
| **Harvest Entrails** | Organs, gut | Used in cooking, medicine, crafting. Not affected by kill method. |
| **Harvest Sinew** | Sinew / tendons | Used in crafting (bowstrings, bindings). Not affected by kill method. |
| **Harvest Bones** | Bones | Used in tools, needles, structure reinforcement. Minimally affected by kill method. |
 
- Each action costs **time and energy** proportional to animal size
- Multiple players can work on a carcass simultaneously — different players can perform different harvest actions at the same time
- **Butchering skill** improves yield quantity and quality across all harvest types
---
 
### Kill Method & Harvest Quality
 
The method used to kill an animal affects the quality of outputs — primarily **hide/leather**:
 
| Kill Method | Hide Quality | Meat | Bones | Entrails/Sinew |
|---|---|---|---|---|
| **Clean kill** (single shot, neck / heart) | ✅ Full quality leather | Unaffected | Unaffected | Unaffected |
| **Multiple arrows** | ⚠️ Scrap leather only | Unaffected | Unaffected | Unaffected |
| **Small animal killed with large spear** | ⚠️ Damaged leather | Unaffected | Unaffected | Unaffected |
| **Sword / blade kill** | ❌ Heavily damaged leather | Marginally damaged | Marginally damaged | Unaffected |
 
**Design intent:** Players are incentivised to think about their weapon choice *before* a hunt. A player hunting for leather should use a bow with careful aim. A player hunting quickly for meat can use whatever is available with less consequence.
 
---
 
### Carcass Decay & Wildlife Attraction
 
An unharvested or partially harvested carcass in the world:
- **Attracts predators and scavengers** — the longer it sits, the greater the risk of wildlife arriving
- **Decays over time** in Chilly zones — eventually the meat becomes unusable
- Does **not** decay in Cold or Very Cold zones (natural preservation)
- Players should prioritise harvesting quickly, especially in warm areas or when predators are nearby
---
 
### Meat Spoilage & Preservation
 
| Condition | Spoilage Rate |
|---|---|
| Raw meat in **Chilly zone** | Spoils within a couple of days |
| Cooked meat in **Chilly zone** | Spoils slower, but still spoils |
| Meat in **Cold / Very Cold zone** | Does not spoil (natural refrigeration) |
 
**Wildlife theft risk (all temperatures):**
- Unsecured food can be stolen by **animals and rodents** even in cold zones
- Certain containers protect stored food from wildlife access
- **Iceboxes** (buildable structure) protect food from both spoilage and theft in cold zones
**Preservation methods** (tech tree unlocks — see also Open Question #8):
- Smoking (Smoking Rack station)
- Drying
- Curing / salting
- Cold storage (Icebox)
> *Full per-recipe nutritional values, costs, and effects are maintained in the Recipe Reference Document.*
 
---
 
## 17. Food System
 
### Overview
Food is a core survival resource and a long-term progression system. Every food item has a **nutritional profile**, a **spoilage rate**, and a **processing state** that affects its safety, value, and effects. Eating a varied, balanced diet builds a stronger **immune system** — the mechanical reward for good nutrition management.
 
---
 
### Food States & Processing
 
| State | Safety | Nutritional Value | Digest Energy Cost | Illness Risk |
|---|---|---|---|---|
| **Raw** | ❌ Unsafe | Low | High | High — food poisoning, parasites |
| **Cooked** | ✅ Safe | Medium–High | Normal | Very low |
| **Smoked** | ✅ Safe | Medium | Normal | Very low |
| **Preserved** (salted, dried, cured) | ✅ Safe | Low–Medium | Normal | Very low |
 
- Raw food is always an option in an emergency but carries significant risk and is harder to digest
- Processing food (cooking, smoking, preserving) eliminates biological hazards
- Some processing methods reduce nutritional value relative to fresh cooked food
---
 
### Nutritional Profile
Every food item provides two layers of value:
 
**1. Sustenance**
- Fills the **hunger** need meter
- Measured in a base sustenance value per gram
**2. Nutrients**
Each food also provides a nutritional profile across four categories:
 
| Nutrient | Sources | Effect |
|---|---|---|
| **Proteins** | Meat, fish, eggs | Muscle recovery, strength, energy capacity |
| **Carbohydrates** | Roots, berries, grains | Fast energy restoration |
| **Vitamins** | Berries, plants, organs | Immune system support, wound recovery |
| **Minerals** | Organs, bone broth, certain plants | Bone strength, endurance, various functions |
 
- Foods are not nutritionally equal — meat is protein-rich but low in carbs; berries are vitamin-rich but low in protein
- Eating a **variety of food types** is required to maintain a full nutritional profile
---
 
### Immune System
 
The **immune system** is a derived stat, built from the player's nutritional history:
 
- A **balanced diet** maintained consistently → strong immune system
- A **poor or monotonous diet** → weakened immune system over time
**Immune system effects:**
- Strong immune system → reduced chance of contracting illness from any source (contaminated water, raw food, cold exposure, wounds)
- Weak immune system → higher susceptibility to all illness types
- The immune system responds gradually — good nutrition improves it over days, not instantly
---
 
### Illness Risk from Food
 
| Food Type | Illness Risk | Example Afflictions |
|---|---|---|
| **Raw meat** | High | Food poisoning, intestinal worms, parasites |
| **Cooked / Smoked / Preserved** | Very low | — |
| **Spoiled food** | High | Food poisoning, vomiting, nausea |
 
- Each illness type has its **own independent probability roll** — multiple afflictions can occur simultaneously
- Risk percentages are starting values, adjusted through playtesting
- A strong immune system reduces the effective chance of each roll
---
 
### Food Spoilage
 
Every food item has a **spoilage rate** that determines how quickly it degrades to an unsafe state:
 
| Food State | Spoilage Rate (Chilly Zone) |
|---|---|
| Raw meat | Days |
| Cooked meat | Slower than raw |
| Smoked meat | Weeks |
| Dried / Cured | Very slow |
| Salted | Very slow |
| Stored in cold zone | Does not spoil (but wildlife theft risk remains) |
 
- Spoiled food can still be eaten — but significantly increases illness risk
- Spoilage is tracked per item; partially spoiled food has intermediate risk
---
 
### Preservation Methods
 
Each preservation method has a **benefit and a trade-off**:
 
| Method | Station Required | Spoilage Rate | Nutrient Impact | Other Effects |
|---|---|---|---|---|
| **Cooking** | Campfire / cooking station | Slower than raw | Moderate reduction | Eliminates illness risk |
| **Smoking** | Smoking Rack | Weeks | Some reduction | Adds smoky flavour — minor buff TBD |
| **Drying** | Drying Rack (future unlock) | Very slow | Moderate reduction | Lightweight — good for travel |
| **Salting** | By hand (requires salt) | Very slow | Some reduction | Causes slight dehydration debuff |
| **Curing** | Curing station (future unlock) | Very slow | Minor reduction | Effects TBD per recipe |
| **Cold storage (Icebox)** | Icebox structure | Preserved indefinitely | No reduction | Protects from wildlife; requires cold environment |
 
> *Full nutritional values, buff/debuff magnitudes, and time/energy costs are defined per recipe in the Recipe Reference Document.*
 
---
 
### Dietary Progression
Food options expand progressively through the tech tree and skill system:
 
**Phase 1 — Available at launch:**
- Raw meat (emergency only)
- Cooked meat (campfire)
- Smoked meat (Smoking Rack)
- Basic foraged foods (berries, roots — nutritional profiles TBD)
**Later unlocks (tech tree):**
- Advanced recipes combining multiple ingredients
- Stews and soups (higher nutrient density from combined ingredients)
- Preserved specialty foods
- Fermented foods
- Salt (enables salting preservation method)
- Distilled alcohol (future — social and medicinal uses)
---
 
## 18. Death, Rescue & The Dream Save System
 
### Overview
The two dreamers are time-displaced modern humans who can "dream the future." This same metaphysical nature lets them experience death as a shared nightmare rather than an ending — when a dreamer dies, they can revert to an earlier dream. This is the game's combined save and death system, and it belongs to the dreamers specifically, not to the rest of the tribe.
 
The game is built for couple play, and the system works identically whether the two dreamers are controlled by two people or one. In co-op, each player controls a dreamer: a partner going down is not a private bookkeeping event but a shared, high-tension moment — one fights to rescue the other, and failing that, both must agree on how far back to dream. In solo, one player controls both dreamers and switches between them at will, including taking control of the standing dreamer to attempt a rescue. There are always exactly two dreamers, and save protection is tied to them — not to the number of humans playing.
 
The rest of the tribe are ordinary NPCs without the dream ability — their deaths are permanent and shrink the tribe's capacity (see Section 7). Everything below applies to the two dreamers only, whether a dreamer is currently controlled by a human or running as AI.
 
---
 
### Incapacitation — Going Down
 
A lethal hit or affliction does not kill a dreamer outright. It puts them into an **Incapacitated** state — downed, but not yet gone — and starts a countdown.
 
This flow covers **intervention-required** incapacitations — the ones that won't heal on their own (Section 15). A minor, **self-resolving** incapacitation (a brief stun or knockdown) simply passes after a moment and does not trigger the rescue or Wake Up machinery below.
 
- The world is in **real-time** for the duration — Skip Time is unavailable while a dreamer is in danger (see Section 5) — making rescue a genuine real-time scramble.
- The countdown length scales with the affliction, not a fixed timer:
| Affliction | Approx. Window | Rescuable? |
|---|---|---|
| Arterial bleeding | ~90 seconds | Yes — stop the bleed |
| Hypothermia / exposure | Several minutes | Yes — restore warmth |
| Broken limb / blood loss | Several minutes | Yes — first aid |
| Poison / infection | Scales with severity | Yes — antidote / potion |
| Clean instant kill (one-shot) | None | No — straight to Wake Up / Game Over |
 
*Exact window durations are starting values, to be tuned in playtesting.*
 
While incapacitated, the downed dreamer can:
- Fire a **location signal** (horn or flare — see Section 20) to help their partner find them
- Wait for rescue
- **Opt to Wake Up immediately** rather than wait
If the window expires, rescue fails, or the downed player opts out → the flow proceeds to **Wake Up** (the shared reset).
 
---
 
### Rescue
 
The other dreamer can reach the downed one and perform first aid matched to the affliction — the surviving player in co-op, or the partner dreamer the solo player takes control of:
 
| Affliction | First Aid Required |
|---|---|
| Bleeding | Stop the bleed (bandage / dressing) |
| Cold / hypothermia | Get them to warmth — fire, shelter |
| Poison / infection | Administer antidote or potion |
| Trauma / blood loss | Stabilise with appropriate supplies |
 
- Rescue requires the **correct first-aid materials** in the rescuer's inventory.
- The rescuer's **Medicine skill** (Section 9) affects success chance and speed.
- **On success:** the downed player is revived in place, the world continues uninterrupted, and there is no reset and no Wake Up cost, and **no Nightmare debuff is applied at all**. This is the best possible outcome — and the reason the rescuer's risk and resource spend are worthwhile.
**Solo play:** The player switches control to the other dreamer and attempts the rescue themselves — same first-aid requirements, same real-time scramble to reach the downed dreamer inside the window. If the standing dreamer can't reach in time or lacks the right supplies, the rescue fails and the flow proceeds to Wake Up / Game Over — exactly as in co-op.
 
> **Design intent:** Rescue is the heroic path. It costs the rescuer materials, time, and exposure to whatever just downed their partner — but it preserves all progress for both players and skips the nightmare tax. Wake Up is the safety net; rescue is the save worth fighting for.
 
---
 
### Wake Up — The Shared Reset
 
When rescue fails, is impossible, or is declined, the players **Wake Up**: both players and the world revert to a chosen earlier checkpoint — the dream they return to.
 
- The reset is **total and global**: everything either player did after the chosen snapshot is undone.
- Consumed items return to inventory if their consumption happened after the chosen snapshot.
- A Wake Up applies the **Nightmares** debuff to the player who died (see below).
---
 
### The Save System — Protection Buffs
 
A save captures a snapshot of the world + both players and grants a **Protected** buff for a set duration. Active protection buffs are displayed in the buff panel (Section 5) with their remaining time, so players always know how long they are covered and by which save.
 
There are two save types. Mechanically the snapshots are identical — they differ only in who the buff covers:
 
| Save Type | Created By | Buff Applies To | Who Can Revert To It |
|---|---|---|---|
| **Shared** | Both dreamers sleeping simultaneously | Both dreamers | Either dreamer's death |
| **Personal** | A solo sleep, or a save consumable | One dreamer (tagged *Triggered by: [Dreamer]*) | Only that dreamer's death, while their buff is active |
 
Protection duration is set by difficulty. Difficulty changes only how long a save protects a dreamer — never whether protection exists. Higher difficulty means shorter coverage, so the dreamers must sleep or bank more often. These values are config-driven and meant to be adjusted freely throughout playtesting until they are dialled in.
 
**Baseline (full 8h sleep):**
 
| Difficulty | 8h Sleep → Protection |
|---|---|
| Hardcore | 16h |
| Hard | 24h |
| Normal | 32h |
| Easy | 48h |
 
Every other save action scales from the same difficulty setting and is tuned alongside it: a half (4h) sleep grants roughly half the full-sleep window, and save consumables come in shorter fixed tiers for banking a precise moment. All durations live in a single config so they can be retuned without touching the rules.
 
- **Sleeping together** refreshes the shared floor and covers both dreamers — making the nightly shared sleep a meaningful ritual.
- **Sleeping apart** grants each sleeper only a personal buff and does not refresh the shared floor — a deliberate risk when the dreamers desync their sleep.
- **Save consumables** let a dreamer bank their exact current progress at a moment of their choosing (e.g. before a hunt or migration). Durations skew generous so that consume → travel → risk → return fits inside the window.
- The game begins with an initial shared checkpoint (**"First Dream"**) at session start, so the dreamers are never instantly exposed.
This model is identical in single-player. Because protection belongs to the dreamers rather than to the humans playing, both dreamers carry their own protection regardless of who is controlling them. A save protects a dreamer only if that dreamer performed it (or it was a shared save), and in solo the player performs each save action as whichever dreamer they are currently controlling. Switching between dreamers does not transfer protection — each dreamer's buffs are their own.
 
*Consumable tiers and durations are tuning knobs; the master pacing knob for the whole system is how often players choose to sleep and bank.*
 
---
 
### Worked Examples
 
*Setup: Day 1 ends, both players sleep (shared save). They wake at 9:00 AM. At 12:00 PM Player 1 drinks a potion (8h personal buff). At 2:00 PM Player 2 drinks a potion (8h personal buff). (These examples use the Hard baseline — 8h sleep → 24h — for illustration.)*
 
| Event | Valid Revert Options | Why |
|---|---|---|
| P1 dies at 1:00 PM | 12:00 PM or 9:00 AM | P1 is covered by both their own 12 PM personal buff and the shared sleep |
| P2 dies at 1:00 PM | 9:00 AM only | The 12 PM buff is P1's; P2 hasn't drunk theirs yet — only the shared save covers P2 |
| P1 dies at 3:00 PM | 12:00 PM or 9:00 AM | P2's 2 PM buff doesn't cover P1. Reverting to 12 PM undoes the 2 PM potion, so it returns to P2's inventory |
| P1 dies at 10:00 PM | 9:00 AM only | P1's personal buff (12–8 PM) has expired; the shared buff (full sleep = 24h) is still active, so it remains as the fallback |
 
---
 
### Choosing a Checkpoint — The Revert Vote
 
On Wake Up, a checkpoint menu appears to both players simultaneously. The world is fully paused (as with the sleep / time-skip screen).
 
- The menu lists the **dead player's valid checkpoints** — their active personal buffs plus any active shared buff. The survivor's own personal buffs are not options here (they cannot cover someone else's death).
- Each slot displays its time, type, and the **cost to each player** — roughly how much progress each loses and which consumed items return — so neither player votes blind.
- Players may deliberately revert further back (e.g. to escape the situation that killed them); the menu is a real choice, not an automatic jump to the most recent save.
**The vote:**
- Selections are live and visible to both players — either can change their pick freely.
- The **Continue** button is greyed out until both players have selected the same slot.
- Once selections match, Continue enables and either player may press it.
**Solo play:** There is no consensus step — the one human picks the dead dreamer's checkpoint and confirms on behalf of both dreamers. The two-party agreement only matters when two people are playing.
 
> **Design intent:** Because the revert spends both players' progress, the choice must be mutual. The dead player often wants to dream further back to be safe; the survivor wants the most recent slot to lose less of their own work. The greyed-out button forces them to talk it out — a small, deliberate co-op negotiation that fits a game built around playing with a partner.
 
---
 
### Game Over
 
A dreamer who dies with **no active dream protection** reaches the Game Over screen. This is the single failure condition — it applies on every difficulty. If, at the moment of death (and after any rescue attempt fails), the dreamer has no valid checkpoint covering them — no live personal buff and no active shared buff — there is no dream to return to, and the run ends and returns to the main menu.
 
Difficulty does not change this rule; it only changes how long protection lasts (see the duration table above). On Easy a single sleep covers the dreamers for a long stretch, so reaching Game Over takes real neglect; on Hardcore the window is short, so staying protected demands frequent sleeping or banking. The forgiveness lives entirely in the duration dial, not in special-case fallbacks.
 
**Save lock.** Reaching Game Over locks the world's save — while locked, those saves cannot be loaded. During development and playtesting (phase 1), the lock is implemented as a simple lock file stored alongside the save: while the lock file is present the saves are unavailable for loading, and removing the lock file makes them loadable again. This keeps a Game Over recoverable for testers without softening the rule during normal play. The locking mechanism is expected to be hardened into a proper permadeath lock before full release.
 
---
 
### Nightmares — The Cost of Waking Up
 
A Wake Up (reset) afflicts **only the player who died** with Nightmares. The surviving player pays nothing extra — losing shared progress in the revert is punishment enough for both of them.
 
Nightmares are a tiered, escalating debuff:
- A death applies **Nightmare (Tier 1)** for a set duration (X hours).
- Dying again while a Nightmare debuff is still active replaces it with the next tier — Nightmare 2, then Nightmare 3, and so on — and **refreshes** the duration.
- Once the active debuff's timer runs out without another death, the player returns to baseline. A later death then starts again at Tier 1.
**Effect** *(starting values, to be tuned):*
 
| Tier | Energy Reduction | Duration |
|---|---|---|
| Nightmare 1 | −10% | X hours |
| Nightmare 2 | −20% | X hours |
| Nightmare 3+ | −10% × tier | X hours |
 
*The −10%-per-tier energy penalty and the X-hour duration are starting values for playtesting. Further effects per tier — beyond the energy reduction — may be layered in once the baseline is tuned.*
 
The escalating tiers are the system's anti-spam mechanism: repeatedly Waking Up instead of surviving compounds the penalty, so death stays meaningful even though it is not permanent.
 
A successful rescue applies **no Nightmare at all** — the rescued player never "died," so there is nothing to dream away. This is part of why rescue is worth the partner's risk.
 
---
 
### NPC Death
 
NPCs do not share the dream ability. Their deaths are permanent, permanently reducing tribe capacity. Players can recover up to 2 additional survivors from the world for a maximum tribe of 10. (See Section 7 for full NPC rules.)
 
---
 
> **System summary.** Death is a shared, relational moment, not a private failure. Rescue is the heroic save that preserves everything; Wake Up is the forgiving net that still costs both players their shared progress and leaves the one who died with escalating nightmares; the buff-based save model lets players set their own risk level by when they sleep and when they bank; and the consensus revert turns recovery into a moment of cooperation. The whole system is built to make two people feel that they are surviving — and dreaming — together.
 
---
 
## 19. Migration
 
### Overview
Migration is the macro-level action that moves the entire tribe from one map to the next. It is a **significant planned event** — not a casual transition. Players must decide what to take, what to leave behind, and how to travel.
 
### Individual Travel
- Players can travel between maps **individually at any time**
- Solo travel is fast but limited to what the player can personally carry
- Useful for scouting, retrieving resources, or visiting old camps
### Tribe Migration (Group Action)
A full tribe migration is triggered when the players decide to relocate camp. It requires:
 
1. **Packing** — players and NPCs load up their carry methods (backpacks, sleds, travois)
2. **Resource triage** — the tribe cannot carry everything; decisions must be made about what stays
3. **Travel** — costs time proportional to carry load and distance
**Travel Modes:**
 
| Mode | Speed | Capacity |
|---|---|---|
| **Light travel** (backpacks only) | Fast | Low — personal carry per member |
| **Sled travel** | Slow | High — sled can carry bulk resources |
 
- Sleds must be **crafted** (tech tree unlock)
- Players can mix modes — some travel light, others pull sleds
### What Happens at Migration
- **Tents and temporary structures** are dismantled and packed
- **Permanent structures** remain on the map
- **Non-perishable resources** left behind remain in place on the map
- **Perishables** left unsecured may decay or be scavenged by wildlife
- The tribe arrives at the new map and must **establish a new camp** from scratch or use any existing structures there
### Migration Pressure
The temperature cycle creates natural migration pressure:
- As a zone cools, wildlife becomes scarcer → hunting yields drop
- As a zone enters Very Cold, survival becomes dangerous and tasks become impossible
- Delaying too long risks a **soft failure state**: each day becomes a survival loop with no forward progress
---
 
## 20. Cross-Map Play & Communication
 
### Overview
Tribe members — both dreamers and NPCs — can be present on **different maps simultaneously**. Each map loads only when a player enters it, and travel between maps is a loading-screen transition. This opens up strategic decisions about splitting the tribe across locations — at a cost, since splitting means no one is watching what they leave behind.
 
---
 
### Simultaneous Multi-Map Presence
- Any dreamer or NPC can be on any map at any time.
- A map is **active** only while a human-controlled dreamer is on it — it loads, spawns wildlife, renders, and simulates in real time.
- A map with no human present — even one holding NPCs or an idle (AI-controlled) dreamer — is **inactive**: not simulated in real time, though its state is preserved.
- Each human renders only the map their dreamer is currently on. Because there are at most two dreamers, **at most two maps are ever active at once**.
---
 
### Inactive-Map Resolution
What happens on an inactive map isn't simulated live — it is computed by elapsed time the next moment the map is entered or its NPCs are checked, using the same approach Skip Time and the Scheduler use to resolve unattended progress (Section 5):
 
- NPC task progress and resource production/consumption are calculated for the time that passed.
- Need drain is applied across that interval.
- An under-provisioned NPC on a distant inactive map can come to harm or die in the meantime; that outcome is resolved retroactively on the next load. This is exactly why distant NPCs must be pre-supplied before leaving the camp radius (Section 7) — no one is there to react.
---
 
### Map Travel
- Moving between adjacent maps: a player travels to the map boundary and triggers the transition; a **loading screen** is shown; the destination loads its current state (structures, resources, any NPCs or dreamers already present).
- Individual travel is unrestricted — any dreamer can move between maps at any time.
- In solo, switching control to a dreamer on a different map loads that map; the one just left becomes inactive.
- **Full tribe migration** is a separate, planned group action (see Section 19).
---
 
### Camp Storage — Access Radius
- The tribe's shared camp storage is a **physical object** in the world.
- It is only accessible to tribe members **within a short radius** of the storage object (exact range to be tuned).
- A dreamer or NPC on a different map **cannot access the storage remotely**.
- NPCs assigned to distant tasks must be pre-supplied with provisions before leaving the storage radius.
---
 
### Cross-Map Communication
 
Tribe members on the **same map** can communicate using signal systems. There is currently **no cross-map communication** — members on different maps cannot signal each other directly.
 
#### Horn Signals
- A horn sends a signal audible to nearby tribe members on the same map.
- Used to: alert to danger, call for help (this is the location signal a downed dreamer fires — Section 18), and coordinate task timing.
- **Range:** to be tuned in playtesting.
#### Fire Signals
- A fire signal (smoke, flame pattern) sends a visual message to tribe members on the same map.
- Longer range than the horn; visible over greater distances.
- **Range:** to be tuned in playtesting.
Because both signalling and rescue are same-map only, a dreamer who is incapacitated while the other dreamer is on a different map **cannot be reached** — that incapacitation resolves to Wake Up (Section 18). Keeping the two dreamers on the same map is the price of being able to save each other.
 
> *Cross-map communication tools may arrive in future updates as technology advances (e.g. long-distance signalling via elevated fire beacons).*
 
---
 
### Cross-Map NPC Task Assignment
 
Players can only assign tasks to NPCs they can reach or signal:
 
| Situation | Can Assign Tasks? |
|---|---|
| NPC in player's party, **nearby** | ✅ Yes — direct interaction |
| NPC in player's party, **far away, same map** | ✅ Yes — via horn or fire signal |
| NPC at camp, player at camp | ✅ Yes — via Camp Management System |
| NPC on a **different map** | ❌ No — cannot assign tasks cross-map |
 
**Camp Management System:**
- A dedicated interface accessible at the camp for managing all NPCs **currently at camp** — assign tasks, update routines, supply provisions, check needs.
- Works only for NPCs physically present at camp; it cannot manage NPCs on other maps or away on distant tasks.
---
 
## 21. Multiplayer
 
### Mode
**Co-op only** — competitive play is not supported.
 
### Players & Roles
 
**Solo (1 player):** the player hosts a local session, controls both dreamers (switching between them), and manages all NPCs. (Control model: Section 7.)
 
**Co-op (2 players):** one player is the Host, the other the Client. Each controls one dreamer; the NPCs are shared. The Host invites the friend, runs the authoritative session, and controls the second dreamer (as AI) and the NPCs whenever the Client is absent.
 
### Player Count
1–2 players. There are always exactly two dreamers (Section 7), so a session supports at most two humans — one per dreamer.
The rest of the tribe — 6 members at start, up to 8 as the tribe grows to its maximum of 10 — is always filled by NPCs.
 
> **Future:** supporting larger groups would mean additional humans controlling mortal tribespeople (ordinary NPCs with no dream/save protection) — a separate design effort, out of scope for the two-dreamer launch.
 
### Connection Methods
Players join a session via:
- **Steam friend invite** — the host invites their friend directly; no public lobbies or matchmaking.
- **LAN** — local network connection.
- **Direct IP** — over the internet via IP address.
### Session Persistence & Joining
- When a player logs out or disconnects, their dreamer reverts to AI control under the host and retains its dreamer status and save protection (a dreamer is protected whether human- or AI-controlled — Section 18). Solo rules effectively resume for the host.
- When that player logs back in, they reclaim their dreamer in its current state.
- A second player joining an in-progress session does not claim an NPC. The host opens the game for multiplayer, keeps one dreamer, and hands the free dreamer to the joiner, who inherits its state and receives a one-time Re-spec Token. (Full flow: Section 7.)
### Host Disconnection
 
#### What Happens
- If the host disconnects (intentionally or through network failure), the session pauses for the client, who is notified and placed in a waiting state.
- The client can choose to disconnect from the waiting screen rather than wait indefinitely.
#### Reconnection
- The game automatically attempts to reconnect the client to the host at regular intervals.
- If the host returns, the session resumes from the paused state.
- If reconnection fails after a reasonable period, the client is returned to the main menu.
#### Disconnect Save
- On host disconnect, a disconnect save is automatically created as a separate slot that never overwrites existing saves, protecting in-progress data from corruption.
- This is a technical safety snapshot, distinct from the dream checkpoints used by the death/save system (Section 18).
- On next session start, the host can resume from the disconnect save or from the last regular save.
---
 
### Save System
 
#### Save Location
- All save data is stored locally on the host machine.
- The client does not store world or character state — it requests it from the host on connection.
#### Save File Structure
Each session is saved as a set of files:
 
| File | Contents |
|---|---|
| **World file** (1 per session) | Map states, structures, resources, camp, world objects, time/date, and the NPC tribe members' state |
| **Dreamer files** (2 per session) | Per-dreamer character state: attributes, inventory, needs, skills, time budget, active buffs/debuffs, and save-protection buffs |
| **Save and Exit** | Host-only manual exit snapshot — one per playthrough, overwritten by each new Save and Exit, loadable only from the main menu |
| **Disconnect save** | Integrity snapshot created on a graceful host disconnect — never overwrites the above |
 
Dream checkpoints (Section 18) are full snapshots of the world plus both dreamers, used by the death/save system. Detailed save-file architecture belongs in the TDD.
 
#### Save & Exit and Crash Recovery
Save and Exit lets the host cleanly leave a session at any time, writing the snapshot above. It loads only from the main menu (it is not part of the client rejoin flow), and a playthrough keeps only one — each Save and Exit overwrites the previous.
 
A Save and Exit is not consumed when loaded. It remains a valid fallback until the first in-game save (a sleep dream checkpoint) occurs after loading, at which point it is invalidated — by then the in-game save represents more recent, legitimate progress. This specifically protects the load-then-immediate-crash case:
 
- **Save and Exit → Load → crash before any in-game save** → the Save and Exit is still valid; you resume from it.
- **Save and Exit → Load → sleep save → crash** → you resume from the sleep checkpoint; the Save and Exit is now invalid.
Recovery in general follows the same principle. A hard crash or forced close (alt+F4 — the process is killed) writes no save, so the playthrough resumes from the most recent surviving save: the last in-game dream checkpoint, or the Save and Exit if no in-game save has occurred since loading. A graceful network disconnect is different — it writes the disconnect save before the process ends. Dream checkpoints (Section 18) therefore serve double duty: in-session Wake Up points and main-menu crash-recovery resume points.
 
Triggering a crash to dodge an outcome is a possible save-scum vector, but the time and friction it costs make it self-limiting — and in a co-op, non-competitive game it is not worth engineering against.
 
#### Session Identity & Rejoining
 
On first connection, the host generates a unique session ID, and the joining client is assigned a unique player ID tied to that session. Both are stored in the client's local storage.
 
The client's "Join Game" menu lists previously joined sessions, each showing the session / host name, online/offline status (the client pings the host to check availability), and last-played date.
 
The client selects a session and rejoins using its saved player ID; the host looks it up and restores that player's dreamer, returning control seamlessly.
 
#### Starting & Switching Sessions
- Creating a new session generates a fresh world file and both dreamer files — both dreamers are created at session start (Section 7).
- A second player who joins is assigned a player ID mapped to the free dreamer (they take over that dreamer rather than spawning a new character).
- Previous sessions and their save sets remain untouched on the host machine.
- A host runs one session at a time per game client; to switch, save and close the current one first. Multiple saved sessions coexist as separate file sets.
- A session that has reached Game Over is locked and cannot be loaded (Section 18).
---
 
## 22. Narrative & Lore
 
### Opening
The game begins with a **text narrative** describing the scene:
- A dreamer wakes up in a prehistoric landscape
- Surrounded by dead tribespeople
- Confused, with fragmented memories of a modern life that feel more like a fading dream
- A cutscene will replace the text intro in a future update
### The Dreamers' Nature
The two dreamers are **time-displaced modern humans**. They retain fragments of their former lives — vague feelings and dream-memories of technologies, comforts, and knowledge that feel alien in their current context. This is:
- The **narrative reason** they are special (they carry knowledge from another time)
- The **mechanical reason** they can dream the future (the tech discovery system)
- The source of their ability to "reset" from death — *they dreamed it, it wasn't real*
### The World's Secret — Artifact Discovery Arc
Scattered across all six maps are **artifacts** — objects that do not belong in the prehistoric world. Their meaning evolves as the tribe's technology advances:
 
| Tribe Tech Level | Artifact Appearance |
|---|---|
| Stone Age | Strange, unrecognizable object. Players have no context. |
| Early tools | Begins to look tool-shaped. Players sense intention in its design. |
| Advanced crafting | Recognizable as a manufactured item — a machine part, a sensor, a panel. |
| High technology | Clear purpose understood. The truth becomes undeniable. |
 
**The Reveal:**
The world is not prehistoric Earth. It is a **network of man-made islands**, engineered by an advanced future human civilization as **zoological preserves** for de-extincted prehistoric fauna. This particular island was designed to simulate Ice Age conditions.
 
The builders are gone — their fate is a mystery for future content. An **autonomous ecosystem management system** continues to operate, but has malfunctioned. To conserve energy, it cycles temperatures across the zones rather than maintaining stable conditions — creating the deadly temperature migration the tribe is forced to endure.
 
**The System can be repaired.** This is a major long-term goal, hinted at through artifacts and tech discovery. Repairing it would stabilize the world's temperature. (Full mechanics for this are scoped to a future update.)
 
### Tone
- The world feels ancient and harsh, but also mysterious and beautiful
- There is a sense that something *wrong* happened here, long before the players arrived
- Players are meant to feel wonder, dread, and gradually — understanding
- The modern memories add melancholy: the players half-remember a world they can never return to
---
 
## 23. Tribe Quests
 
### Overview
Tribe Quests are **collective goals** that give the group shared direction beyond individual survival. They are structured around the same needs pyramid that governs individual needs, ensuring the game's progression feels cohesive and meaningful.
 
### Quest Tiers
 
**Tier 1 — Survive (Physical Foundation)**
Goals focused on securing the basics for the whole tribe:
- Establish a reliable food source
- Secure clean water
- Build shelter for all tribe members
- Maintain warmth through the first cold snap
**Tier 2 — Stabilize (Safety & Community)**
Goals focused on building a resilient, secure tribe:
- Reduce wildlife threat around camp
- Build enough storage to weather a migration
- Ensure all tribe members are consistently fed and rested
- Foster tribe cohesion (socialization metrics)
**Tier 3 — Thrive (Self-Actualization)**
Goals focused on elevating life beyond survival:
- Create a communal bath or sauna
- Develop cuisine beyond basic sustenance (complex recipes, variety)
- Produce art — carvings, cave paintings, music instruments
- Achieve a personal milestone for every tribe member
- Create a space for leisure and rest (not just sleep)
### Quest Behavior
- Quests in a higher tier are **not available** until the tribe has consistently met the needs of the tier below
- Quests can be active simultaneously within a tier
- Completing quests earns **tribe-wide rewards**: permanent buffs, new crafting recipes, morale boosts, or story discoveries
---
 
## 24. Open Questions
 
All unresolved design questions are tracked in a dedicated document:
👉 **See [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md)**
 
Questions are prioritized by impact on core systems and updated as decisions are made. Once resolved, each question is marked complete and the relevant GDD section is updated.
 
---
 
*End of Document — Version 0.1*
*Next document: Technical Design Document (TDD)*