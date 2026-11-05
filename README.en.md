# ⚙️ Sephiria Expanded MOD
[한국어](README.md) | **English** | [日本語](README.ja.md) | [简体中文](README.zh-CN.md)

A HorayModAPI mod that adds quality-of-life features to Sephiria.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3832216646) | [Update history](https://github.com/psyche-kr/Sephiria-Expanded-MOD/releases) | [Roadmap](https://github.com/psyche-kr/Sephiria-Expanded-MOD/blob/main/ROADMAP.md) | [Suggestions & bug reports](https://github.com/psyche-kr/Sephiria-Expanded-MOD/issues)

## 💡 Installation
1. Subscribe to Sephiria Expanded on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3832216646).
2. Launch the game and the mod is applied. Steam downloads new versions automatically.
3. Every feature can be turned on and off with the `Sephiria Expanded` button in the ESC menu. The settings support Korean, English, Simplified Chinese and Japanese.

## 🧩 Source code
The mod source is in [`src/`](src). An entry point that inherits the game's internal API (`HorayModBase`) patches game methods with [Harmony](https://github.com/pardeike/Harmony).

| Location | Role |
| --- | --- |
| `src/SephiriaExpanded/Main.cs` | Mod entry point. Loads settings, applies Harmony patches, creates and removes runtime objects |
| `src/SephiriaExpanded/ModSettings.cs` | Saves and loads option values |
| `src/SephiriaExpanded/ExpandedSettingsPanel.cs` | Mod options UI attached to the game's settings screen |
| `src/SephiriaExpanded/Features/` | One file per feature (damage meter, boss challenges, hidden room tracker, shared wallet, etc.) |

**Build**: Open `src/SephiriaExpanded.sln` in Visual Studio 2022. Game DLLs (`Assembly-CSharp`, `Mirror`, `UnityEngine.*`, etc.) are not included in the repository, so change the `HintPath` entries in `SephiriaExpanded.csproj` to your own game install path (`Sephiria_Data/Managed`).

## 🛠️ Tools
- Visual Studio 2022
- dnSpy v6.1.8
- Unity Explorer BIE 5 Mono
- Asset Studio Net 8.0
- Codex
- Aseprite

## ✨ Features
**Convenience**
- Chat Convenience: adds timestamps, chat scrolling, /clear (clear chat), item grade display and more.
- Show Favorites in Shop: favorite item marks are also shown in shops.
- Show Enchant Count: shows how many times an item has been enchanted.
- Preset Slot Expansion: raises preset slots to up to 100.
- Auto Run: always keeps running whenever running is possible.
 - Remove Run Inertia: while running you move in the input direction and stop instantly instead of slowly speeding up or sliding.
- Hidden Room Tracker: shows where hidden rooms are on the screen.
- Show Remaining Rewards: shows how many reward chances are left when choosing.
- Reveal Minimap: reveals the minimap.
 - Minimap Travel: click the minimap to travel even to far places.
- Open Inventory in Combat: lets you open the inventory and stats window during combat.
- Boss HP Text: shows the exact boss HP as text above the boss HP bar. (Percentages show one decimal place.)
- Show Boss Aggro Target: outlines the player targeted by the boss in white.
- Auto Use Special Attack, Grimoire, Active Artifact: keeps using them while you hold the key.
- Mod Item Tooltip: shows in the item tooltip which mod added the item.
- Show FPS: shows the current frame rate in the top-left corner.
- Damage Meter: measures personal and party damage and DPS and shows them on screen. (Personal DPS, personal total damage, party DPS, party total damage) Hold Alt to move the window.
 - Meter Hotkey: sets the key that shows or hides the meter window.
- Kill Log: a kill log option is added under Friendly Fire. When a player dies, the name of whoever killed them (monster or player) is shown in chat. Guests without the mod don't see the log.
- Nickname Color: changes your nickname to one of 12 colors. (Visible to guests without the mod.) With gradient colors 1 and 2 set, the nickname goes nickname color → gradient color 1 → gradient color 2.
- Return to Town on Leaving: when you leave a multiplayer session as a guest, go straight to your starting town instead of the title screen.
- Cursor Visibility: gives the mouse cursor a thicker outline with a selectable color.

**Growth / Items**
- Inventory per Level: grants inventory slots every time you gain the set number of levels.
- Player Level Cap Expansion: raises the level cap from 30 to 60. Applies on the host; guests also need the mod to use it.
- Instant Gold & EXP: removes the pickup objects for gold and EXP and collects them instantly.
- Sell Starting Items: lets you sell the costume's starting items in shops.
- First Artifact Legendary: until you get your first item, artifact choices are always legendary or higher. Rerolls keep this, and it returns to normal once you get an item.
- First Tablet Legendary: until you get your first Tablet, Tablet choices are always legendary or higher. Rerolls keep this, and it returns to normal once you get a Tablet. (If there aren't enough legendary choices, the rest are vanilla.)
- Enchant Altar Fixed to 3: every enchant altar allows 3 enchants.
- First Room Anvil: guarantees an anvil in the first room of floors 1 and 2 and shows every weapon that can be upgraded.
- Scarecrow HP Boost: training scarecrows get 100x HP.

**Costume / Journal effects**
- Root's Retreat Weapon Border: marks weapons cleared on hard mode 15/30/50/60 in the journal. (Type A, Type B or Type B (S))
- Root's Retreat Weapon Shine: weapons cleared on hard mode 60 sparkle while held. Also shown on the weapons of teammates who use the mod.
- Root's Retreat Costume Animation: decorates the banner of costumes cleared on hard mode 60.

**Wishing Fountain**
- Unlock All Items in Wishing Fountain: Bond Artifacts, Tablets and artifacts sold by the suspicious merchant can be stored in the Wishing Fountain.
- Wishing Fountain Expansion: increases the maximum capacity of the Wishing Fountain.
- Others Can Pick Up Fountain Artifacts: when you drop an artifact taken from the Wishing Fountain, other players can pick it up.

**Map / Events**
- Map Expansion: expandable floors get 4 to 6 extra rooms at random. (Guests also need the mod.)
- Random Event Weight: adjusts the chance multiplier of random events such as blood donation, gravestone, suspicious merchant and pocket dimension shop. (Even at max they are not guaranteed.)
- Guaranteed Mystic Pot: the Mystic Pot always appears where it can appear.
- Guaranteed Merchant Inventory: the small merchant always sells an inventory.
- Skip Qliphoth Cutscenes: skips the floor 5 Qliphoth and floor 6 Qliphoth phase 1 cutscenes.

**Multiplayer**
- Max Players Expansion: raises the multiplayer player limit to 16.
- Share Lucky Medal: shares the Lucky Medal effect with the party.
- Share Potions: when you pick up a potion, every party member gets one.
- Friendly Fire: you can damage allies and be damaged by allies.
- Ping Sound: mutes ping sounds or stops them from repeating. (Once per 3s, once per 5s, mute)
- Ping Spam Prevention: limits the pings and notices from interacting with or trying to use the next map portal to once per player per stage.
- Show Teammate Currency: in multiplayer, shows the dice, sapphires and gold each teammate has under their HP bar.
- Share Wallet with Team: combines every teammate's gold or dice into one shared wallet. (Share Gold / Share Dice) (Also works for guests without the mod.)

**Pattern Enhancement**
- Pattern Enhancement: raises overall monster multipliers and enhances and adds boss patterns, making the game much harder. (Guests without the mod can also play.) (Guests without the mod don't see boss lines in chat.)
 - Global Difficulty: the difficulty level applied to every floor and boss at once. With it on, you get +300% (Normal) / +500% (Hard) extra sapphires.
 - Use Boss Break Gauge: a break gauge is shown under the boss HP bar. Deplete it to knock the boss down for a moment.
 - Apply Recommended Multipliers: raises monster multipliers together with the pattern enhancement level. (Normal / Hard) Normal monster and mini boss HP x2 / x4, normal monster spawn count x2 / x2, normal monster and mini boss attack speed x1.2 / x1.2, boss HP x2 / x4, boss damage +15% / +25%. Not multiplied with values set in the Scaling tab; the higher one is used.

**Monster multipliers**
- Adjusts monster spawn, HP, damage and pattern speed multipliers.
- Apply Spawn Multiplier to Mini Bosses: chooses whether mini bosses are also multiplied by the spawn multiplier.
- Reduce EXP & Gold by Multiplier: when on, extra monsters from the spawn multiplier don't give extra gold and EXP.
- Festival of Blood Mitigation: when on, extra monster HP from the HP multiplier doesn't increase Festival of Blood healing.

**Misc**
- Language: changes the display language of the mod settings. (Korean / English / Chinese / Japanese) It starts from your PC language. Mod notices and boss lines also follow this language.
- Update History: check it to open the mod's update history (releases) page in your web browser.
- Reset All Settings: resets every mod setting to its default.

## 💡 What Pattern Enhancement changes (Normal/Hard)
**Floor 1 Mogdi**
Combo attacks: the basic attack, double attack, dynamite and hand slam chain into another attack (up to 2 times). The chain chance is 50/100% and the cooldown is 5/2.5 seconds.
When throwing dynamite, he throws 5 more on each side.
The hand slam becomes a 12-way radial shockwave.
On jump landings and when coming down from the air, he scatters dynamite in all directions (6/12).
While in the air, 2 falling rocks drop every second and the drill dynamite fires twice as often.
Falling rocks drop near players every 2 seconds. Every 20 seconds, falling rocks rain over the whole stage.
The drum stage attack drops 2/3 more falling rocks.
Hard: every 14 seconds, falling rocks sweep in two lines. In phase 2 they go to the end and come back.
When phase 2 starts, his HP is fully restored.
Lines: encounter, dynamite pattern, below 50% HP in each phase, phase 2 landing, death.

**Floor 1 Odner**
After using the bomb punch, he uses it once more.
During the missile pattern he throws dynamite every 0.5/0.25 seconds. After the missiles, a 7-ring explosion spreads out.
The bomb scatter throws 1/2 more bombs. Remote dynamite now explodes in order along two lines, top and bottom.
Overheat (20% HP or less): shorter waits between patterns, a bomb every 5 seconds, and the HP bar changes color.
Hard: throws dynamite to both sides while charging. While overheated, he uses the bomb scatter and remote dynamite one more time.
Lines: encounter, 50% HP, overheat, death.

**Floor 1 Haz**
Vanilla hard patterns are not forced on, and there is no break gauge.
Line attack bullets: unchanged on Normal, 1 more on Hard.
Point attacks also raise tentacles at random spots in the map.
Lasers become 2 lines. On Hard, after a full turn the laser turns once more in the opposite direction.
Hard: tentacles rise along the map edge every 1.5 seconds. 50% of line attacks become vertical line attacks (0.75s warning, slow bullets).
Lines: encounter, death.

**Floor 2 Rataka**
The bullet hell pattern is remade as one of circle, fan, spin or cross. While using it, she fires 12 aimed bullets, and on Hard these follow the player.
Falling rocks become denser straight lines and drop after the bullet hell ends.
The vanilla summon pattern is removed; instead she summons minions periodically (every 16/10 seconds, up to 8).
Lines: encounter, summon, 50% HP, death.

**Floor 2 Lerid**
Charges 4 times in total. On Hard, every 2nd and 4th charge also fires shuriken in 8 directions.
The 2nd hit of the basic attack becomes 12-way bullets 30° apart, with matching warnings. On Hard he also throws shuriken 45° apart.
Slow shuriken count: first 7/8, second 5/6, spread angle 80°.
The aimed dagger pattern gets 1/2 more clones. In the sequential daggers, the second clone throws 5 slow shuriken.
Lines: encounter, charge, clones, 50% HP, death.

**Floor 3 Erma (Golem)**
No break gauge.
The golem's hands take no damage.
The golem gets 1/2 more phases. Phase 4 uses a missile pattern (straight, converging or grid) every second. Hard adds phase 5 with 20% move speed, 30% laser speed, and only support fire and lasers.
The hand slam is fixed to 5 times and the shockwave to 5 times; on Hard every slam makes a shockwave.
Spread shots are 5 sets of 18/20 bullets, 0.66/0.56 seconds apart.
Lines: golem encounter, entering phase 4/5, death.

**Floor 3 Pentaxis**
Uses the ordered explosion together with the zigzag bullets.
Uses chakrams together with the spinning charge. Chakrams and fireballs no longer appear as standalone patterns.
Jump attacks and lasers spawn 1 fireball, 2 below 50% HP, and 3 below 10% HP on Hard.
Hard:
Zigzag bullet interval changes from 1.2s to 1.0s and the number of volleys from 7 to 8.
Ordered explosion warning changes from 2s to 1.6s.
Jump attacks are fixed at 5.
6 fragments burst from where a fireball disappears.
Below 10% HP he enters annihilation mode and the HP bar changes color.
Lines: encounter, spinning charge, jump, 50/10% HP, death.

**Floor 4 Askard**
The pattern order is vanilla.
Tentacle patterns:
Fill: the wait between rows changes from 1s to 0.88/0.75s.
Wave: the wait between sweeps changes from 0.75s to 0.66/0.5s.
Point and line: one more tentacle row each; line attacks add rows at ±45° toward the player.
Slam: the warning changes from 2s to 1.88s. Landing bullets increase by 2/4, and on Hard he slams once more 1.5 seconds later.
Laser: after the full fan, he fires once more in the opposite direction without warning. On Hard the laser warning changes from 2s to 1.75s.
Other: blood spit comes from both sides. Laser intervals in the darkness pattern change from 1.25~0.45s to 1.20~0.40s.
Lines: entrance, slam, laser, 75/50/25% HP, death.

**Floor 5 Kovess**
Double slash: Normal chains hits 1→2→1, Hard 1→2→1→2.
Band explosion: when it ends, it explodes once more in the unused direction (horizontal/vertical).
Flying tornado: warnings appear one by one, then explode in warning order. Normal: warnings 1.33s apart, 1.66s wait, explosions 1.33s apart. Hard: 1s, 1.5s, 1s.
Outer storm bullets: 6%/12% faster.
Circular bullets: only appear in the 4th, 8th, 12th… pattern bag.
Hard: from 12 seconds into the fight, 5 falling bullets drop every 12 seconds.
Lines: encounter, flying tornado, 90/50/25% HP, death.

**Floor 5 Kraz**
Hard:
The head slam becomes a 3-hit combo.
Slam bullets spread 360° beyond the vanilla fan (4 fewer than filling at vanilla spacing).
Charges are 15% faster.
Lines: encounter, 90/50/25% HP, death.

**Floor 5 Qliphoth** (same on Normal/Hard)
Awakening (once per fight): when HP falls below 5%, all patterns are cancelled. She teleports to the center and, invincible for 5 seconds, fully restores her HP and break gauge. The HP bar changes color from then on.
After awakening:
Damage taken from players is tripled.
Tree lasers become 6 lines.
Root explosion and geometric projectile patterns are no longer used.
An extra crack appears every 3 seconds, and she approaches players 20% faster.
Lines: encounter, awakening, 66/33% HP after awakening, death.
