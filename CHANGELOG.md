BioRand v3.3.2 (2026-01-18)

* [Change] Update link now goes to official website (which hosts full packs) instead of GitHub.
* [Fix] #614: RE 2, Claire, lockpick still works for sewer door when she has no lockpick.
* [Fix] #826: RE 2, locked drawers sometimes already open.
* [Fix] #949: RE 2, Leon B, 602 enemy attack in Annette cutscene.
* [Fix] #1081: RE 2, Leon is Claire when player character not randomized.
* [Fix] RE 3, restaurant lockpick cabinet sometimes already unlocked.
* [Fix] RE 3, boutique door sometimes already unlocked.

BioRand v3.3.1 (2025-12-29)

* [Feature] Add copy and paste buttons for seed.
* [Feature] Allow BGM volume to be modified in settings file.
* [Enhancement] Speed up randomization after restart by caching voice sample lengths.
* [Change] Add extra logging to enemy rando to help diagnose seed inconsistency.
* [Change] RE 3, increase default volume of BGM.
* [Fix] #841: UI not compatible with screen resolution 1280x800.
* [Fix] #1068: Voice randomization broken for actor skins when all voice samples used at least once.
* [Fix] Automatically unblock game .exe files to prevent `operation was cancelled by the user` message.

BioRand v3.3.0 (2025-08-22)

* [Feature] New content added, see [biorand/classic-data](https://github.com/biorand/classic-data/releases) for release notes.
* [Enhancement] Add support for datapacks.
* [Fix] #920: RE 1, cannot enter door 302:1.
* [Fix] #934: RE 2, female characters unable to hit with custom magnum.

BioRand v3.2.7 (2024-08-31)

* [Feature] RE 1, add Donna, Helena, and Yoko as playable characters.
* [Feature] RE 2, add Angie, Bela, Cassandra, Daniela, Donna, Excella, Helena, and Yoko as playable characters.
* [Feature] RE 3, add Bela, Cassandra, Daniela, Excella, Helena, and Yoko as playable characters.
* [Enhancement] Increase playable character dropdown limit to 128.
* [Fix] #889: RE 2, valve handle can be placed in helicopter fire.

BioRand v3.2.6 (2024-02-21)

* [Fix] Randy Rando when Japanese text is enabled.

BioRand v3.2.5 (2024-02-20)

* [Feature] Add BGM for RE 0 (N64) and RE 7.
* [Feature] Enable or disable hidden key items.
* [Feature] RE 1, add Andy, Eveline, Mark, Niikolai (RE3R), and Sherry (RE2R) as playable characters.
* [Feature] RE 1, add Jessica (Revelations), Jill (BSAA) (RE 5), Jim (Outbreak), and Misty (RE 2) as playable characters.
* [Feature] RE 2, add Cindy (Outbreak), Jim (Outbreak) as playable characters.
* [Feature] RE 2, add Claire (CV), Irving (RE 5), Jill (BSAA) (RE 5), and Jack Baker (RE 7) as playable characters.
* [Feature] RE 2, add Randy Rando.
* [Feature] RE 2, add Steve G1 skin.
* [Feature] Replace weapon slider with list selection.
* [Enhancement] Include soft segment enders in single segment door randos.
* [Enhancement] RE 1, randomize main hall doors.
* [Enhancement] RE 2, orca skin, updated sounds.
* [Enhancement] RE 2, prevent playing wrong scenario.
* [Enhancement] Update and add more voice lines.
* [Change] RE 1, 108, randomize dogs to any enemy.
* [Fix] #823: RE 1, generation gets stuck rather than showing message.
* [Fix] Missing hurt sounds for Nikolai and Tyrell.
* [Fix] Random events sometimes enabled for RE 1 and RE 3.
* [Fix] RE 2, room 403, lighter requires lighter.
* [Fix] Same seed produces different enemies across devices.

BioRand v3.2.4 (2024-01-27)

* [Feature] RE 2, add orca dog enemy skin.
* [Change] RE 2, room 613, larvae removed from room due to #797.
* [Fix] #791: RE 3, room 217, text disappears.
* [Fix] #794: RE 3, room 408, shooting explosive box with hunter gammas crashes the game.
* [Fix] #795: RE 2, room 409, potential softlock with fan vent.
* [Fix] #797: RE 2, room 613, crash due to larvae and random events.
* [Fix] #800: RE 2, room 614, enemies in cutscene for Claire A door rando.
* [Fix] #805: RE 3, Room 113, crash due to Hunters Gamma.
* [Fix] #806: RE 3, room 417, hospital explodes in door rando causing softlock.
* [Fix] #810: RE 3, room 103, arms crash.
* [Fix] RE 2, room 403, lighter placed in dark.

BioRand v3.2.3 (2024-01-14)

* [Fix] #786: RE 1, room 301, crash due to bees.
* [Fix] Last room appearing in any segment.

BioRand v3.2.2 (2024-01-13)

* [Change] RE 2, improve trade NPC message.
* [Change] Tweak health item ratio.
* [Fix] #697: RE 3, room 309, statue items unavailable after puzzle.
* [Fix] #706: RE 3, text 50A, text disappears.
* [Fix] #773: RE 2, room 113, 614, enemies attack in cutscene.
* [Fix] #783: RE 3, room 200, crash when enemy is spider.
* [Fix] RE 1, potential soft lock in door randomizer.
* [Fix] RE 1, room 10F, potential softlock in door rando.
* [Fix] RE 1, room 205, dogs crash game during cutscene.
* [Fix] RE 3, room 20F, crash when NPC is Irons.
* [Fix] generation error when randomize NPCs is disabled.

BioRand v3.2.1 (2024-01-04)

* [Fix] #756: RE 1, room 10F, Jill softlock when using music notes.
* [Fix] #757: RE 2, room 309, random event crash.
* [Fix] #759: RE 2, room 304, Claire crash when partner uses ledge.
* [Fix] #760: RE 2, room 10C, arms can prevent statue push.
* [Fix] #769: RE 2, room 601, Leon B crash.
* [Fix] launch when no settings file.
* [Fix] pasting of seed.

BioRand v3.2.0 (2023-12-31)

* [Feature] Add custom content folder (`%APPDATA%\biorand\data`).
* [Feature] Add support for alternative PLD skins.
* [Feature] RE 2, add Ethan, Eveline, and William (G1) as playable characters.
* [Feature] RE 2, add random events.
* [Enhancement] RE 2, Update Kendo with zombie texture.
* [Enhancement] RE 2, Update Mark, and Regina PLD.
* [Change] Move RE 4 regenerator BGM to danger category.
* [Change] RE 2, room 20B, make item behind desk low priority.
* [Fix] #502: RE 2, Leon's partner character health is danger in some rooms.
* [Fix] #687: RE 2, room 219, crash due to G-Adult.
* [Fix] #694: RE 3, room 110, locked RPD causes softlock.
* [Fix] #703: RE 2, room 10C, moths crash game.
* [Fix] #705: RE 1, room 100, crash during Jill poison cutscene.
* [Fix] #711: RE 2, room 309, crash due to enemies.
* [Fix] #747: RE 1, mini spiders crash game during first Hunter cutscene.
* [Fix] RE 2, moths can crash the game.
* [Fix] RE 2, room 106, fix door crash.
* [Fix] RE 2, room 114, Claire zombie cutscene and random events.
* [Fix] RE 2, room 206, 60D, exclude moths.
* [Fix] RE 2, room 60B for Claire.
* [Fix] RE 2, room 614, enemies during cutscene.

BioRand v3.1.8 (2023-12-07)

* [Feature] Add and improve BGM for RE:CV, RE 0, RE 4, RE 5, RE1R, RE2R.
* [Feature] Add BGM for RE 1.5, RE Survivor.
* [Feature] Add more radio voice lines.
* [Feature] RE 1, randomize lab announcer.
* [Feature] RE 2, randomize lab announcer, narrator, and monster/screams.
* [Feature] RE 1, allow pass code door to be unlocked from other side.
* [Feature] RE 2, add Andy (Survivor), Jessica (Revelations), Lily (Survivor), Mark (Outbreak), Rodrigo (CV), Vincent (Survivor) as playable characters.
* [Feature] RE 2, add Lady D (RE8), Nemesis (RE3R) Neptune (RE1), zombies (RE1), zombie (RE1R) enemy skins.
* [Feature] RE 2, enemies can now spawn in cutscene rooms.
* [Enhancement] Add tooltips to item distribution chart.
* [Enhancement] Improve audio quality for Ark, Ashley, Krauser, Lott, William.
* [Enhancement] Increase NPC and enemy skin limit.
* [Enhancement] RE 2, room 614, enable enemies.
* [Enhancement] RE 2, update Lott model.
* [Enhancement] RE 3, allow sliding worm in all rooms.
* [Enhancement] RE 3, room 109, room 123, randomize enemy placements.
* [Change] RE 2, room 200, no longer classified as safe room.
* [Change] RE 3, alter enemy limits.
* [Change] RE 3, update enemy constraints.
* [Fix] #93: RE 1, two music books placed for Jill.
* [Fix] #507: RE 2, room 613, silent enemies.
* [Fix] #595: RE 2, Irons voice line labeled as Ben.
* [Fix] #652: RE 3, room 508, crash due to Jill NPC.
* [Fix] #663: RE 3, room 111, crash when zombie uses spit attack.
* [Fix] #664: RE 2, room 613, crash due to G-Adult.
* [Fix] #665: RE 2, Spider spawned when slider was set to none.
* [Fix] #666: RE 1, room 513, incorrect voice.
* [Fix] #668: RE 2, room 612, crash due to moths.
* [Fix] #670: RE 3, room 205, crash due to incompatible NPC.
* [Fix] #671: RE 2, room 106, crash when opening door with no enemies.
* [Fix] #673: RE 1, room 40F, crash if hurt when using V-JOLT.
* [Fix] #676: RE 1, pass code door does not work in Mansion 1.
* [Fix] #678: RE 1, room 405, crash due to snakes during door cutscene.
* [Fix] #682: RE 3, crash at basement door due to arms.
* [Fix] enemy randomization logging in some rooms.
* [Fix] generate fail for RE 1 and 3.
* [Fix] muted enemies when NPC is in room.
* [Fix] NPC gifts always being FAS.
* [Fix] player voice and local variables.
* [Fix] random events when npc or voice rando is off.
* [Fix] RE 1, crash in crank rooms due to wasps.
* [Fix] RE 1, emblems not randomized.
* [Fix] RE 1, music score not randomized for Jill.
* [Fix] RE 1, prevent battery from being placed in caves.
* [Fix] RE 2, inventory menu is in JPN.
* [Fix] RE 2, room 300, Ben heard instead of Irons replacement.

BioRand v3.1.7 (2023-11-03)

* [Feature] Increase maximum average number of enemies to 16.
* [Feature] RE 1, helmet key now available in mansion 1.
* [Feature] RE 1, keep escape lab key in Tyrant room.
* [Feature] RE 1, mansion 2 only rooms now accessible in mansion 1.
* [Feature] RE 2, add Carlos, Mikhail, Nikolai, Jill from RE3R as playable characters.
* [Feature] RE 2, add Misty as playable character.
* [Feature] RE 2, add Zombies (RE 1.5), Gorilla (RE 1.5), Hunter (RE 3).
* [Feature] RE 3, enable all enemy types in all rooms.
* [Enhancement] Marvin, add lines from RE 2 trial.
* [Enhancement] RE 2, Nemesis enemy skin now has faster RE 3-like animations.
* [Fix] #629: RE 2, room 103, G-Adult out of bounds.
* [Fix] #630: RE 3, room 30E, crash.
* [Fix] #630: RE 3, room 500, crash.
* [Fix] #632: RE 2, Room 119, crash when random placements is disabled.
* [Fix] #637: RE 3, room 414, crash due to NPC.
* [Fix] #638: RE 1, room 112, player is invisible.
* [Fix] #639: RE 3, room 50A, crash due to Carlos NPC change.
* [Fix] #640, BioRand has no minimize button.
* [Fix] RE 1, crash when lots of enemies in a room.
* [Fix] RE 1, improve enemy placement in a few small rooms.
* [Fix] RE 1, player sometimes invisible in rooms containing a mirror.
* [Fix] RE 1, web spinner had no room limit.
* [Fix] RE 2, room 20E, crash due to G-Adult.

BioRand v3.1.6 (2023-10-18)

* [Fix] #541: RE 3, Sheva model is glitched in HD.
* [Fix] #563: RE 3, Murphy model is glitched in HD.
* [Fix] #610: RE 3, room 10D, crash when playing as Regina.
* [Fix] #615: RE 3, swap player character can be enabled via pasting a seed.
* [Fix] #617: RE 3, room 30F, crash due to random enemies.
* [Fix] #619: RE 3, room 504, crash in middle of cutscene.
* [Fix] #620: RE 1, room 30F, crash due to wasp attack during cutscene.
* [Fix] #621: RE 2, room 409, unable to use valve handle.
* [Fix] #622: RE 3, Claire model is glitched in HD.
* [Fix] #623: RE 3, room 508, crash due to EM67.
* [Fix] #626: RE 2, room 60C, door not randomized.
* [Fix] RE 1, room 308, enemies in Jill cutscene.
* [Fix] RE 1, room 609, crash due to hunter ambush.
* [Fix] RE 2, room 60A, vines replaced with zombies.

BioRand v3.1.5 (2023-10-13)

* [Fix] RE 2, some rooms crash when G-Adult is present.

BioRand v3.1.4 (2023-10-10)

* [Feature] Add new slider for % of rooms with enemies.
* [Feature] RE 1, add many more playable character.
* [Feature] RE 1, show warning if bio1dc is found.
* [Feature] RE 1, Add Orca (Neptune skin).
* [Feature] RE 1, Add piano melodies from RE 0, and RE:CV.
* [Feature] RE 2, add G-adult, and G-embryo.
* [Feature] RE 2, add room 408, 409 pair to door randomizer.
* [Feature] RE 2, enable spider and ivy poison in every room.
* [Feature] RE 2, guest registration now required to open door.
* [Feature] RE 2, room 208, enable special key locker.
* [Change] RE 1, improve enemy balancing.
* [Change] Improve random enemy proportions.
* [Change] Disable title screen demos that auto-play if idle.
* [Fix] #566: RE 1, Yawn poison can not be healed with blue herb.
* [Fix] #574: RE 2, room 21B, Irons might not have a gun.
* [Fix] #575: RE 1, room 406, crash due to enemy in cutscene.
* [Fix] #577: RE 1, room 40A, small key not placed for desk.
* [Fix] #579: RE 1, room 20E, crash when enemy is Neptune.
* [Fix] #580: RE 3, Wesker (RE1R) + shotgun causes crash in some rooms.
* [Fix] #583: RE 1, room 306, crash during cutscene due to baby spiders.
* [Fix] #584, #588: scripted enemy IDs not unique.
* [Fix] #585: RE 1, room 61A, enemies can attack you during radio cutscene.
* [Fix] #586: RE 1, room 60F bar, enemies during cutscene.
* [Fix] #587: RE 3, room 20C, Nikolai might not have a gun.
* [Fix] #593: RE 1, room 205, crash due to snake bite in cutscene.
* [Fix] #598: RE 3, room 315, crash due to enemy change.
* [Fix] #599: RE 1, room 50C, Rebecca NPC has incorrect voice.
* [Fix] #603: RE 3, room 218, killing dogs can crash the game.
* [Fix] #611: RE 3, room 205, NPC EM67 crashes the game.
* [Fix] RE 1, battery is not placed in lab segment.
* [Fix] RE 1, issues with Chris 8-slot stacking.
* [Fix] RE 1, room 306, typo in exclude name.
* [Fix] RE 1, room 307, crash due to snake bit during cutscene.
* [Fix] RE 1, room 30F, crash due to snake bit during cutscene.
* [Fix] RE 2, incorrect enemy ratios.
* [Fix] RE 3, incorrect enemy ratios.
* [Fix] RE 3, room 205, crash due to Marvin 2 NPC.
* [Fix] RE 3, room 300, enemy spawns during cutscene.

BioRand v3.1.3 (2023-09-07)

* [Feature] RE 1, add Carlos, Irons, Marvin, Nikolai, and Wesker (RE 5) as playable characters.
* [Fix] #565: RE 1, Chris, random items in inventory slot 7,8.
* [Fix] #568: RE 1, room 405, enemies present during cutscene.
* [Fix] #569: RE 1, Enrico does not have a gun.
* [Fix] #570: RE 3, room 310, enemies present during cutscene.
* [Fix] #572: RE 1, room 10E, crash when enemy is adder.
* [Fix] RE 1, 10F, crash during cutscene due to bees.
* [Fix] RE 1, 20A, crash during cutscene due to mini spiders.
* [Fix] RE 1, room 116, pickup always has amount 7.
* [Fix] RE 1, room 212, 408, 502, 508, Richard is too slow.
* [Fix] RE 1, room 302, stuck when ladder is locked.

BioRand v3.1.2 (2023-09-05)

* [Change] Add BSAA radio voice clips.
* [Change] RE 1, change enemy names.
* [Change] RE 1, room 212, force cutscene for Jill door rando.
* [Change] RE 3, add RDT integrity check.
* [Feature] RE 1, add Kendo, Lott, and Sherry as a playable character.
* [Feature] RE 1, add random enemy placements.
* [Feature] RE 1, allow Chris to use 8 inventory slots.
* [Feature] RE 1, allow using herbs when at full health.
* [Feature] RE 1, enable Neptune as a kill-able enemy.
* [Feature] RE 1, Tyrant 2 fight is now guaranteed in door randos.
* [Feature] RE 2, add Leon (RE 6) as a playable character.
* [Feature] RE 2, add wasp enemy skin.
* [Fix] #229: RE 1, combining large amount of ammo with Flamethrower would overflow.
* [Fix] #325: RE 3, an error occured during generation with randomize NPCs.
* [Fix] #475: RE 1, room 305, player sometimes changes.
* [Fix] #540: RE 3, room 20B, graphical issues during cutscene.
* [Fix] #546: RE 3, room 211, crash due to NPC EM67.
* [Fix] #549: RE 3, room 107, crash due to reused global enemy ID.
* [Fix] #550: RE 3, room 402, Hunter may not spawn in cutscene.
* [Fix] #552: RE 3, room 20C, crash due to Marvin NPC.
* [Fix] #552: RE 3, room 211, crash due to Marvin NPC.
* [Fix] #553: RE 3, 205, crash due to NPC EM67.
* [Fix] #556: RE 2, room 608 NPC out of place during cutscene.
* [Fix] #556: RE 2, room 612 NPC out of place during cutscene.
* [Fix] #561: RE 3, room 502, 508, 50C, incorrect voices during cutscene.
* [Fix] Incorrect voices when only one character is available.
* [Fix] RE 1, 30F, locked lift causes softlock.
* [Fix] RE 1, improve partner handling for 8 slot inventory.
* [Fix] RE 1, incorrect enemy sounds for Jill.
* [Fix] RE 1, incorrect hit scan when swap is on but change player isn't.
* [Fix] RE 1, player animation height.
* [Fix] RE 1, randomize Rebecca / partner character.
* [Fix] RE 1, room 112, enable closet door in door rando.
* [Fix] RE 1, room 207, enable broken door in door rando.
* [Fix] RE 1, room 502, improve logic for Jill.
* [Fix] RE 1, room 513, 70C, NPC was always Barry.
* [Fix] RE 1, zombie death and vine grab player sounds were incorrect.
* [Fix] RE 2, room 408, NPC might not have a gun.
* [Fix] RE 3, Jake has corrupted face texture.
* [Fix] RE 3, room 20D, incorrect Carlos voice.
* [Fix] RE 3, room 20E, door rando, camera locked after cutscene.
* [Fix] RE 3, room 408, 40A, only use NPCs with weapon.

BioRand v3.1.1 (2023-08-06)

* [Feature] Add option for title voice randomization.
* [Feature] RE 2, add US/EUR item placements.
* [Feature] RE 3, add Nemesis, and Sheva as playable characters.
* [Change] Move Barry and Billy radio voice samples to misc category.
* [Fix] #513: RE 3, Annette model has texture issues.
* [Fix] #514: RE 1, room 118, key item placed in 618.
* [Fix] #515: RE 2, 401, Claire A, crash due to partner being Ben.
* [Fix] #520: RE 1, Jill (RE3) voices are sometimes silent.
* [Fix] #525: RE 3, 107, crash due to Marvin 2 as NPC.
* [Fix] #526: RE 2, room 404, Claire A, crash due to Ben NPC.
* [Fix] #527: RE 2, room 608, moths may crash the game.
* [Fix] #529: RE 1, room 500, softlock when escape sequence active.
* [Fix] #530: RE 3, Zombies during Carlos segment have incorrect meshes and UV.
* [Fix] #531: RE 3, room 11B, NPC has glitched textures.
* [Fix] #535: RE 1, room 106, incorrect voice for acid round cutscene.
* [Fix] RE 1, 203, NPC has incorrect voice.
* [Fix] RE 1, 20D, NPC has incorrect voice.
* [Fix] RE 1, various sound conversion issues.
* [Fix] RE 2, missing PLWs for Claire and Rebecca.
* [Fix] RE 3, 105, crash caused by Irons NPC.
* [Fix] RE 3, 50A, crash caused by Marvin NPC.
* [Fix] RE 3, Ashley caused crashes.
* [Fix] RE 3, Krauser has graphical issues.
* [Fix] RE 3, starting handgun ammo is sometimes another handgun.

BioRand v3.1.0 (2023-07-27)

* [Feature] Add fully randomized voice lines option.
* [Feature] Add reduce awkward silences option.
* [Feature] All NPC EMD files now auto generated from PLD files.
* [Feature] Allow zombies to be a random character.
* [Feature] Applicable randomized NPCs now have a random gun.
* [Feature] Random enemy skins.
* [Feature] RE 1, Kenneth now randomized.
* [Feature] RE 1, Random NPCs now keep their correct animations.
* [Feature] RE 1, Add Ark, Barry, Jill (RE3), Steve as playable characters.
* [Feature] RE 2, Ben, Irons, and Marvin now fully replaced with any character.
* [Feature] RE 2, Mayor's daughter now randomized.
* [Feature] RE 2, Add Alexia, Alfred, Dario, David, Lott, Nemesis, Nikolai, Rachael, Tyrell as playable characters.
* [Feature] RE 3, Add Alexia, Alfred, Annette (RE2R), Ashley, Ben, Chris, Chris (CV), Dario, David, Enrico, Kendo, Irons, Piers, Rebecca, Tyrell, Wesker, Wesker (RE5) as playable characters.
* [Enhancement] Increase size of enemy bar chart.
* [Enhancement] RE 1, Chris / Jill voice during lab computer now randomized.
* [Change] Update Regina's voice files.
* [Fix] #482: RE 3, room 50A, crash due to NPC change
* [Fix] #491: RE 3, room 112, Brad replacing Marvin EMD crashes game
* [Fix] #501: RE 2, room 301, Ben -> Hunk has wrong body texture
* [Fix] #509: RE 2, scenario B, power room key always in 603
* [Fix] Ada character replacement has odd mesh morphing.
* [Fix] RE 1, player hurt sounds not correct in cave / lab
* [Fix] RE 1, room 71A, softlock when ladder is locked
* [Fix] RE 2, room 113, Claire voice lines can overlap

BioRand v3.0.9 (2023-07-04)

* [Fix] #463: RE 2, Leon A/B, Sherry height is wrong when grabbed by enemies.
* [Fix] #466: RE 1, swap characters crashes apart from Chris / Jill.
* [Fix] #470: RE 2, room 409, Claire A/B, crash when random enemy placement is on.
* [Fix] #472: RE 3, room 315, crash when enemies are changed.
* [Fix] #480: RE 3, room 104, softlock if the player ignores lighter oil item.
* [Fix] error: resampling not yet supported for stereo.
* [Fix] RE 1, EMW files are not replaced.
* [Fix] RE 1, weapon hit scan is incorrect when swapping characters.
* [Fix] RE 2, Sherry (RE2R) has incorrect height during cutscenes.
* [Fix] RE 2, some female EMDs don't have hurt texture pages.

BioRand v3.0.8 (2023-06-27)

* [Feature] Add music tracks from RE 1 remake.
* [Feature] RE 1, RE 2, allow Leon/Claire characters to be swapped.
* [Feature] RE 2, Allow partner to be a male character.
* [Fix] #270: RE 2, Claire B, Magnum/Shotgun cannot kill enemies.
* [Fix] #446: Enemies sliders not working properly.
* [Fix] #447: RE 1, room 603, crash when hunter jumps you.
* [Fix] #449: RE 3, room 105, NPC change to 96 crashes the game.
* [Fix] #451: RE 3, room 206, enemies not disabled for post-basement cutscene.
* [Fix] #452: RE 3, doors to rooms originally connected to room 105 are locked.
* [Fix] #454: RE 2, room 405, Leon A, 6+ spiders crashes the game.
* [Fix] #457: RE 3, room 122, enemy event crashes the game if not zombie.
* [Fix] #458: RE 3, room 10F, killing dogs crashes the game.
* [Fix] #459: RE 1, room 306, crash when picking up item which is replaced with a document.
* [Fix] #460: RE 2, room 219, possible crash when Ben is replaced with EM5A.
* [Fix] #461: RE 3, room 105, NPC change to 95 crashes the game.
* [Fix] #462: RE 3, room 507, enemies unable to move.
* [Fix] RE 2, Sherry weapon hit scan.

BioRand v3.0.7 (2023-06-17)

* [Fix] #443: RE 3, new characters animations glitch or crash.

BioRand v3.0.6 (2023-06-16)

* [Feature] RE 1, add Leon, Leon (RE2R), Claire, and Regina as playable characters.
* [Feature] RE 1, add Leon, and Claire as NPCs.
* [Feature] RE 2, add Leon (RE4), and Wesker (RE1R) as playable characters and NPCs.
* [Feature] RE 3, add Barry, Billy, Brad, Chris (RE 5), Hunk, Jake, Kevin, Krauser, Leon, Leon (RE 4), Rebecca, Richard, Wesker (CV), Wesker (RE1R), William as playable characters.
* [Feature] RE 3, add Kevin, Leon, and Wesker (RE1R) as NPCs.
* [Fix] #433: RE 3, room 120, crank not placed.
* [Fix] #434: RE 3, killing dogs in some rooms can crash the game.
* [Fix] #437: RE 3, room 120, crash when random enemy placement is enabled.
* [Fix] RE 3, Rebecca NPC is distorted.
* [Fix] RE 3, Jill NPC is distorted.

BioRand v3.0.5 (2023-06-10)

* [Feature] RE 2, Leon can now be given the lockpick.
* [Feature] RE 3, enable random enemy placements.
* [Fix] #269: RE 2, Wesker (RE 1) does not have hurt texture.
* [Fix] #278: RE 3, some cutscenes freeze.
* [Fix] #398: RE 3, room 405, Kendo graphical glitches or crash.
* [Fix] #406: RE 3, room 213, glitched enemies after electrocution.
* [Fix] #408: RE 3, room 10B, camera angle locked.
* [Fix] #409: RE 3, room 40D, camera stuck due to incorrect entrance.
* [Fix] #413: RE 3, room 503, softlock due to locked elevator door.
* [Fix] #415: RE 2, ligher / lockpick is not given to player at start of game.
* [Fix] #427: RE 3, room 20C, NPC softlock after ready to go cutscene.
* [Fix] #428: RE 3, room 317, NPC change crashes game.
* [Fix] #429: RE 3, room 303, softlock when random enemy placement enabled.
* [Fix] #430: RE 2, Claire B, non-Sherry NPCs get stuck entering room for cutscene.
* [Fix] #431: RE 3, Kevin's model crashes the game.
* [Fix] RE 2, Murphy, Piers, and Tyrell do not have hurt textures.
* [Fix] RE 3, room 111, fix leave day / night cycle.
* [Fix] RE 3, water sample was never placed.

BioRand v3.0.4 (2023-05-27)

* [Fix] RE 1, NPC randomization.
* [Fix] #374: RE 3, room 108, door sometimes unlocked without using key.
* [Fix] #400: RE 3, room 11B, glitches if entered after room 11C cutscene.
* [Fix] #403: RE 3, crash due to no weapon in inventory slot 0.
* [Fix] #404: RE 3, room 506, softlock due to no facility key.

BioRand v3.0.3 (2023-05-26)

* [Enhancement] Use targeted voice lines for NPCs that about to die.
* [Enhancement] Add some extra calm CV:X music.
* [Enhancement] RE 3, room 309, randomize crystal ball items.
* [Change] Improve BGM volume for RE 1 and RE 3.
* [Fix] RE 3, restore day/night cycle around hydrant room and first Nemesis encounter.
* [Fix] #363: RE 3, room 405, locked elevator door causes softlock.
* [Fix] #364: RE 3, room 203, cutscene does not work with different enemies.
* [Fix] #367: RE 3, room 103, crash caused by car door event.
* [Fix] #368: RE 2, unable to find key item to swap.
* [Fix] #369: RE 3, room 104, Zombie has bugged animation.
* [Fix] #372: RE 3, room 40A, crash when re-entering room.
* [Fix] #376: RE 3, room 30B, camera stuck after Nemesis encounter.
* [Fix] #378: RE 3, Jill model not replaced.
* [Fix] #379: RE 3, room 209, crash due to car door enemy.
* [Fix] #380: RE 3, room 105, crash due to NPC.
* [Fix] #381: RE 3, room 11D, crash.
* [Fix] #383: RE 3, room 500, enemies out of bounds.
* [Fix] #384: Segment created without a box room when prioritise cutscenes enabled.
* [Fix] #385: RE 2, Leon B, softlock because lighter not placed.
* [Fix] #391: RE 3, room 205, crash due to NPC.
* [Fix] #396: RE 3, room 207, camera lock when re-entering after panic event.
* [Fix] #397: RE 3, room 21A, crash if the dogs are damaged by explosive barrel.
* [Fix] #399: RE 3, room 40B, camera angle stuck after draining the fish tanks.
* [Fix] #400: RE 3, room 11B, glitches if entered after room 11C cutscene.

BioRand v3.0.2 (2023-05-16)

* [Feature] Add button to UI for starting the game.
* [Feature] RE 2, disable 'must wait for Sherry' prompt and allow more other NPCs in place of Sherry.
* [Feature] RE 2, randomize lighter/lockpick inventory item.
* [Feature] RE 2, randomize partner (Ada/Sherry) character.
* [Feature] RE 3, randomize partner (Carlos) character.
* [Enhancement] RE 3, randomize between rocket and no rocket Nemesis.
* [Enhancement] RE 2, room 100, 104, 105, 10B, 119, 204, 205, 212, 216, enable random enemy placement.
* [Change] Improve UI for shuffle default items.
* [Change] Remove a couple of short riffs from RE 1 BGM pool.
* [Change] Remove a few short riffs from RE 3 BGM pool.
* [Fix] #141: RE 1, room 302 battery lift can corrupt inventory.
* [Fix] #257: RE 2, room 60C, crash possibly due to moths.
* [Fix] #284: RE 3, room 109, fire comes back.
* [Fix] #300: RE 3, room 109, firehose cannot be used.
* [Fix] #303: RE 3, room 103, fix crash if car door bursts open.
* [Fix] #304: RE 3, room 101, crash if NPC is EM2F.
* [Fix] #305: RE 3, room 107, crash if NPC is EM67.
* [Fix] #306: RE 2, Leon A, player changes to Ada in door rando.
* [Fix] #307: RE 2, room 107, crash possibly due to moths.
* [Fix] #311: RE 1, crash when playing Rebecca due to invalid inventory.
* [Fix] #312: RE 3, room 205, restaurant door not linked.
* [Fix] #313: RE 3, room 402, bomb can detonate in door rando.
* [Fix] #314: RE 3, room 301, enemy change crashes game.
* [Fix] #315: RE 3, room 110, Nemesis crash in certain situations.
* [Fix] #320: RE 3, room 106, crash when Nemesis performs attack.
* [Fix] #328: RE 3, room 21A, crash when enemies are dogs.
* [Fix] #331: RE 2, room 501, crash possibly caused by moths.
* [Fix] #336: RE 3, room 20F, crash after Nemesis encounter.
* [Fix] #337: RE 3, room 20B, crash during Mikhail cutscene.
* [Fix] #343: RE 2, failed to generate seed when no ammo types are available.
* [Fix] #346: RE 3, room 102, crash when girl is killed.
* [Fix] #349: RE 2, room 106, out of bounds enemies active in door rando.
* [Fix] #350: RE 3, room 202, crash when car door bursts open.
* [Fix] #350: RE 3, room 204, crash on Nemesis cutscene.
* [Fix] #351: RE 3, room 40A, crash when shooting explosive.
* [Fix] #354: RE 2, room 10B, Claire A with random enemy placement crash.
* [Fix] #355: RE 3, room 105, crash when Brad is changed to Jill.
* [Fix] #357: RE 2, room 113, Leon B, Sherry to Ada can cause softlock.
* [Fix] #358: RE 2, crash when first inventory slot is not a weapon.
* [Fix] #359: RE 3, room 20E, cutscene plays with enemies in room.
* [Fix] #360: RE 2, room 21A, crash potentially caused by moths.
* [Fix] #361: RE 2, room 202, crash due to shared enemy ID for Marvin.
* [Fix] RE 2, room 202, 408, 409, 605, moths potentially crash game.
* [Fix] RE 2, room 307, last Claire line in first Sherry cutscene.
* [Fix] RE 2, room 407, moths potentially crash game.
* [Fix] RE 2, room 603, unable to push box past arm enemy.
* [Fix] RE 2, room 607, Sherry voice clips not correct actor.
* [Fix] RE 3, room 112, enemy sounds are incorrect.
* [Fix] RE 3, room 20C, Mikhail 2nd cutscene crashes.
* [Fix] Incorrect seed in log when using random player.
* [Fix] Randomization of various RE 2 voice clips.

BioRand v3.0.1 (2023-03-17)

* [Feature] RE 1, add playable characters: Enrico and Richard
* [Feature] RE 3, add NPCs: Carlos (RE3R) and Jill (RE3R)
* [Feature] RE 3, add Mikhail (RE3R) and Nikolai (RE3R) NPCs
* [Fix] #275: RE 3, room 107, crash due to Jill 2 NPC
* [Fix] #277: RE 3, room 105, crash due to bad NPC
* [Fix] #279: RE 3, room 40C, crash on entering room
* [Fix] #280: RE 3, room 11B, Carlos cutscene crash
* [Fix] #285: RE 3, room 104, crash when killing brain sucker
* [Fix] #286: RE 3, room 118, crash when killing brain sucker
* [Fix] #294: RE 3, room 307, crash due to Carlos NPC change
* [Fix] #295: RE 3, room 209, crash when car door bursts open
* [Fix] inventory colour of rifle and ink ribbons
* [Fix] RE 3, title card sound clip for JPN
* [Fix] RE 3, crash when opening first door
* [Fix] RE 3, room 208, zombie window climb cutscene crashes game
* [Fix] RE 3, room 209, gunpowder not randomized
* [Fix] RE 3, room 20A, grave digger triggered crash
* [Fix] RE 3, room 504, cutscene crash
* [Fix] room 300, 310, Jill / Carlos voices not correct
* [Fix] voice randomizer was not deterministic
* [Fix] Carlos RE3R face

BioRand v3.0.0 (2023-03-12)

* [Feature] Resident Evil 3 randomizer now implemented.
* [Enhancement] Add support for RE 2 HQ sound pack.
* [Fix] #265: RE 2, room 10D, Claire crash when entering room.
* [Fix] #273: RE 2, room 404, Claire, NPC EM5A crashes the game.
* [Fix] RE 2, adjust enemy limit for certain types based on difficulty.
* [Fix] RE 2, crash when using Claire's handgun as Carlos, Mikhail, or Murphy.
* [Fix] RE 2, room 409, cutscene crash due to Sherry NPC.
* [Fix] RE 2, room 501, using lighter crashes the game.

BioRand v2.3.0 (2023-03-05)

* [Feature] Add BGM context menu.
* [Feature] Add prioritise cutscenes option.
* [Feature] Add random player selection.
* [Enhancement] Add more title voices
* [Enhancement] Add music tracks from RE 1: Director's Cut.
* [Enhancement] Enable music tracks from RE 3.
* [Enhancement] RE 2, add Carlos, Jill, Mikhail, Murphy from RE 3 as playable characters.
* [Enhancement] RE 2, add Carlos, Jill, Mikhail, Murphy, Tyrell from RE 3 as NPCs.
* [Enhancement] RE 2, room 112 (Library) now supports random enemy placement.
* [Change] RE 1 and RE 3 save theme now retrieved from installed game.
* [Change] Rename 'Cerberus' to 'Zombie Dog' for RE 2.
* [Fix] #225: RE 1 data not found when data folder name is USA
* [Fix] #251: RE 2, room 501, Claire, crash when using lighter in flare cannon
* [Fix] #253: RE 1, room 508, MO disk placed after door that requires it
* [Fix] #258: RE 2, room 404, crash due to EM05A
* [Fix] #261: RE 2, random BGM requires RE1 installation (#262)

BioRand v2.2.2 (2023-02-10)

* [Feature] RE 2, custom player inventory faces now use .png files.
* [Feature] Add context menus to enemies and NPCs UI.
* [Enhancement] Add more links to UI.
* [Enhancement] Add progress / status indicator when generating.
* [Enhancement] RE 2, room 602, allow return to room in door rando.
* [Fix] #227: RE 2, room 203, zombie arms prevents step ladder from being pushed.
* [Fix] #228: RE 2, room 40D, crash due to partner and random enemy placement.
* [Fix] #230: Weapons in inventory also found as pick ups.
* [Fix] #231: RE 2, room 501, crash due to partner and random enemy placement.
* [Fix] #234: RE 1, room 20E, crash when enemy is Yawn.
* [Fix] #235: RE 1, no Wesker face image in inventory.
* [Fix] #237: RE 2, room 202, crash when enemies are arms.
* [Fix] #238: RE 2, random inventory double items glitch if not at top.
* [Fix] #239: RE 2, room 117, crash caused by Claire B Tyrant cutscene.
* [Fix] #241: RE 2, room 30B, freeze when NPC 0x59 is used.
* [Fix] #244: RE 2, freeze when NPC 0x59 is used for Claire scenario.
* [Fix] #245: RE 2, room 409, crash due to Sherry (0x45) as NPC.
* [Fix] #249: RE 2, room 60A, Lighter not placed before vent door.
* [Fix] RE 1, room 107, Yawn causes player to disappear.
* [Fix] RE 2, room 107, enemies out of bounds.
* [Fix] RE 2, room 20D, enemies out of bounds.

BioRand v2.2.1 (2023-02-05)

* [Feature] Allow customisation of starting weapons.
* [Feature] Allow quantity of weapons in seed to be customised.
* [Feature] Allow full enemy selection.
* [Feature] Allow full NPC selection.
* [Feature] Allow full background music selection.
* [Feature] RE 1, Rebecca, and Wesker are now playable characters.
* [Feature] RE 2, add Billy from RE 0.
* [Feature] RE 2, add Ada, Annette, Claire, Kendo, Leon, Marvin, and Sherry from RE 2 remake.
* [Feature] RE 2, add Chris, Steve, and Wesker from RE Code Veronica X.
* [Feature] RE 2, add Ashley, and Krauser from RE 4.
* [Feature] RE 2, add Chris, Sheva, and Wesker from RE 5.
* [Enhancement] RE 1, all characters can now have Enrico animations.
* [Enhancement] RE 2, Add Claire voice lines from RE:CV.
* [Enhancement] RE 2, Add walking NPCs for Enrico and Richard.
* [Enhancement] Speed up NPC generation by caching voice sample lengths and using all processors.
* [Enhancement] Support multiple conditions in custom voice filenames.
* [Enhancement] Report issue now uses a more user friendly GitHub form.
* [Change] Use QR code for seed when in game.
* [Change] Limit maximum number of certain enemy types within a single room.
* [Change] Last judgement RE2R BGM updated to include intro.
* [Change] RE 2, room 111, exclude moths from randomization pool.
* [Change] RE 2, room 603, exclude moths from randomization pool.
* [Fix] #97: RE 2, room 401, going down ladder breaks if lift is unconnected.
* [Fix] #170: UI is too tall for small screen resolutions.
* [Fix] #190: RE 2, room 402, crash when random enemy placement is enabled.
* [Fix] #191: Number of ink ribbons in seed does not reflect probability.
* [Fix] #192: Permission denied when generating seed.
* [Fix] #193: RE 2, room 501, crash when random enemy placement is enabled.
* [Fix] #197: RE 2, room 106, crash when opening door with random enemy placement enabled.
* [Fix] #198: RE 2, room 110, 10F, 111, enemies out bounds.
* [Fix] #200: RE 2, room 208, key item behind Film A document.
* [Fix] #203: RE 2, room 405, 407, 504, crash with random enemy placements.
* [Fix] #204: RE 1, room 406, item 8 can not be picked up.
* [Fix] #205: RE 2, room 113, first Claire line is not randomized correctly.
* [Fix] #206: RE 2, room 608, EM90 NPC crashes.
* [Fix] #212: RE 2, room 602, Leon B Tyrant item can be key item.
* [Fix] #214: RE 2, enemies are silent when partner is present.
* [Fix] #216: RE 1, room 10B, key item not present.
* [Fix] #219: RE 1, room 308, flamethrower randomized causing softlock.
* [Fix] #220: RE 1, room 118, items from Barry / Wesker not randomized.
* [Fix] #221: RE 1, Rebecca's invetory could be full not allowing her to pickup serum or V-Jolt.
* [Fix] RE 1, room 105, zombie cutscene not randomized.
* [Fix] RE 1, room 106, Chris voice not randomized for cutscene 2.
* [Fix] RE 1, room 405, Enrico / Richard are unsuitable for Wesker cutscene.
* [Fix] RE 2, inventory may not be randomized for scenario B.
* [Fix] Install path does not get saved.

BioRand v2.1.7 (2023-01-20)

* [Change] Remove RE 1 BGM 40 out of music rotation, too short.
* [Change] RE 2, update Regina model.
* [Fix] #137: RE 1, room 10F, Rebecca disappears after leaving potentially causing softlock.
* [Fix] #164: RE 2, room 208, enemies in safe room even when option unchecked.
* [Fix] #165: RE 1, room 400, moving statue over hole in guard house hall can crash game.
* [Fix] #168: RE 1, room 309, Barry vs. hunter crash.
* [Fix] #171: RE 2, room 10D, enemy out of bounds when random enemy position enabled.
* [Fix] #173: RE 2, room 203, softlock when Tyrant hits you off ladder.
* [Fix] #174: RE 2, room 300, confusing if cutscene plays from wrong door.
* [Fix] #175: RE 2, room 400, cutscene adds invisible wall / camera glitch.
* [Fix] #177: RE 2, room 405, crash caused by lots of tyrants.
* [Fix] #178: RE 2, room 101, crash when NPC is em90.
* [Fix] #179: RE 2, room 402, crash due to random NPC.
* [Fix] #180: RE 2, room 40E, crash when random enemy placement enabled.
* [Fix] #183: RE 1, obtaining 003 key requires helmet key.
* [Fix] #184: RE 1, room 116, broken shotgun cannot be placed.
* [Fix] #186: RE 2, room 202, Claire B crash with random enemy placements.
* [Fix] #187: RE 2, room 21B, wall unexpectedly unblocked for Leon in door rando.
* [Fix] #189: RE 2, Irons backwards animation glitches.

BioRand v2.1.6 (2023-01-15)

* [Enhancement] Add settings to control enemy quanity and whether they appear in save rooms.
* [Enhancement] Report issue with seed now inserts version info into issue body.
* [Fix] #147: RE 2, Regina inventory has yellow line artefacts.
* [Fix] #153: RE 2, room 201, crash at licker window with random enemies enabled on Claire.
* [Fix] #154: RE 2, room 200, glitch when enemy attacks during cutscene.
* [Fix] Increase volume of some BGM tracks.
* [Fix] RE 2, Alyssa inventory shows Claire.
* [Fix] RE 2, Irons, Kendo and Marvin have wonky walk animations.
* [Fix] RE 2, room 403, Leon switches to Ada when going down the lift.
* [Fix] RE 2, room 405, prevent enemy repositioning for random placement.
* [Fix] RE 2, some enemies are out of bounds when random enemy placement is enabled.

BioRand v2.1.5 (2023-01-12)

* [Feature] RE 2, Add Ada as a playable character.
* [Feature] RE 2, Add Alyssa (RE Outbreak) as a playable character and NPC.
* [Feature] RE 2, Add Jake (RE 6) as a playable character and NPC.
* [Feature] RE 2, Add Piers (RE 6) as a playable character and NPC.
* [Feature] RE 2, Add random enemy positions and quantities.
* [Feature] RE 2, Add Regina (Dino Crisis) as a playable character.
* [Feature] RE 2, Birkin / G1 can now appear with random enemy placements enabled.
* [Enhancement] Title backgrounds now have BioRand logo.
* [Fix] #124: RE 2, room 601, softlock due to partner flag in door rando.
* [Fix] #125: Random enemy placements places enemies in cutscene rooms.
* [Fix] #127: Firing the grenade launcher (explosive) crashes the game.
* [Fix] #128: Cabin key spawned in A scenario non-door rando.
* [Fix] #131: RE 2, room 404, Claire softlock when partner is not Sherry.
* [Fix] #132: RE 2, room 409, Irons invisible during Ada / Annette cutscene.
* [Fix] #142: RE 2, room 601, soft lock when door is locked.
* [Fix] #143: RE 1, room 10E, crash when dog is in closet.
* [Fix] RE 2, Hunk model..
* [Fix] RE 2, Kevin voice lines do not play.
* [Fix] RE 2, Richard PLD injured texture was incorrect.
* [Fix] RE 2, room 21B, lift is not randomized for Leon.
* [Fix] RE 2, room 40A, voices not randomized for Claire.
* [Fix] RE 2, room 611, crash when em90 is used.
* [Fix] RE 2, William, two voice lines do not play.
* [Fix] Reduce chance of duplicates in voice randomization.
* [Fix] Volume of some BGM tracks were too loud.

BioRand v2.1.4 (2023-01-07)

* [Feature] Claire / female playable characters can now use all of Leon's weapons.
* [Feature] Inventory can now be randomized for RE 2.
* [Enhancement] Add description to rebirth mod selection.
* [Enhancement] Add Josh and Doug radio voice lines from RE 5.
* [Enhancement] Add Mike radio voice lines from RE 4.
* [Enhancement] Add more Ada voice lines from RE: Umbrella Chronicles and RE: The Darkside Chronicles.
* [Enhancement] Add more Hunk voice lines from RE: Umbrella Chronicles.
* [Enhancement] Add more music tracks from RE: Code Veronica, RE 4, RE 5, RE:UC, and RE:DC.
* [Enhancement] Add more RE: Code Veronica music tracks.
* [Enhancement] Allow Leon to use Claire's handgun.
* [Enhancement] Custom voice files can now use conditions and kind. E.g. `joe-chris_radio.ogg` or `joe-nojill.ogg`.
* [Enhancement] Inventory randomization can now be toggled on or off.
* [Enhancement] Speed up music randomization by utilizing all processor cores.
* [Change] Hunk model updated to be more similar to RE 2 Remake.
* [Change] Normalize volume of William's voice lines.
* [Change] Random inventories can now contain mixed herbs and ammo for the random weapon.
* [Change] RE 2, enemy ratios for easy difficulty adjusted.
* [Change] RE 2, room 106, enable back door in door randos.
* [Fix] #95: RE 2, room 614, Claire A cutscene softlocks.
* [Fix] #106: RE 1, room 502, possible softlock if cutscene plays.
* [Fix] #114: RE 2, room 409, crash when Annettee is swapped with Sherry (jacket).
* [Fix] #119: RE 1, room 203, Barry cutscene can softlock.
* [Fix] #120: RE 1, room 307, item 3 is only available if Barry is triggered.
* [Fix] #121: RE 1, room 308 Barry cutscene can crash game.
* [Fix] #122: Config button's function doesn't reflect its name.
* [Fix] #123: RE 1, room 30C, item not present.
* [Fix] RE 2, Hurt Ark and William models / textures not correct.
* [Fix] RE 2, main fuse is never placed for Leon door randos.
* [Fix] RE 2, non-door rando, scenario B key items in scenario A start area.
* [Fix] RE 2, room 20E, softlock due to item on other side of mirror.
* [Fix] RE 2, room 214, item may not be in scenario B.
* [Fix] RE 2, room 216, Leon B, enemies may be present during cutscene.
* [Fix] RE 2, room 505, Claire B cutscenes.

BioRand v2.1.3 (2023-01-02)

* [Fix] RE 2, softlock due to door change after cutscene in 60C.

BioRand 2.1.2 (2023-01-02)

* [Fix] RE 2, every seed fails in 2.1.1.

 BioRand 2.1.1 (2023-01-01)

* [Feature] RE 1, implement document/item randomization for RE 1.
* [Feature] RE 2, Add more music tracks from RE 0.
* [Feature] RE 2, Add results music track from RE 5.
* [Feature] RE 2, Add Ark Thompson (RE Survivor) as a playable character and NPC.
* [Feature] RE 2, Add Kevin Ryman (RE Outbreak) as a playable character and NPC.
* [Feature] RE 2, Add Sherry Birkin as a playable character.
* [Feature] RE 2, Add William Birkin as a playable character and NPC.
* [Enhancement] Countdown/alarm music tracks now swapped with another countdown track (non-door rando only).
* [Enhancement] Reduce chance of unused rooms in door rando.
* [Enhancement] RE 1, randomize ladder in room 302 and 406.
* [Enhancement] RE 2, allow randomization of radio to any character (inc. Brad).
* [Enhancement] RE 2, enable room 101 front door in door rando.
* [Enhancement] RE 2, enable room 103 gate in door rando.
* [Enhancement] RE 2, enable room 10C middle door in door rando.
* [Enhancement] RE 2, enable room 11B in door rando.
* [Enhancement] RE 2, enable room 20E in door rando.
* [Enhancement] RE 2, enable room 400 in door rando.
* [Enhancement] RE 2, enable room 404 in door rando.
* [Enhancement] RE 2, enable room 701 in door rando (scenario A).
* [Enhancement] RE 2, force most cutscenes (regardless of scenario) to play in door rando.
* [Change] RE 1, room 20D, make clip low priority.
* [Change] RE 2, room 204, prevent confusion due to change of player position.
* [Change] RE 2, room 703, prevent confusion due to cutscene.
* [Fix] #94: RE 1, using Helmet Key before killing Plant 42 crashes the game.
* [Fix] #102: RE 1, room 405, cutscene crashes when enemies are swapped.
* [Fix] #103: RE 1, room 11A, crests are already placed in door rando.
* [Fix] #104: RE 1, room 208, dog spawn crashes the game.
* [Fix] #105: RE 1, enemy swap with tyrant 2 crashes game.
* [Fix] #107: RE 2, room 20C, 109, tyrant encouters crash the game.
* [Fix] #108: RE 2, room 307, softlock caused by swap with Irons.
* [Fix] #110: RE 2, room 110, moths crash the game.
* [Fix] RE 1, prevent Yawn 2 from spawning anywhere other than lesson room.
* [Fix] RE 2, NPC randomization in room 505.
* [Fix] RE 2, player death sound not randomized.
* [Fix] RE 2, room 216, 301, Ada NPC swap softlocks when partner flag is enabled.
* [Fix] RE 2, room 400, Sherry scream not randomized.
* [Fix] RE 2, room 404, Leon's voice not randomized.
* [Fix] RE 2, room 604, Ada's grunts not randomized.
* [Fix] RE 2, Sherry/swap height positioning in cutscenes.

BioRand v2.0.3 (2022-12-20)

* [Change] RE 1, make x-ray lab room wooden box items low priority.
* [Change] RE 2, room 205, do not randomize enemies.
* [Change] RE 2, room 309, now bi-directional for Leon and remove ladder lock.
* [Change] RE 2, room 301, allow Ben to be swapped with Irons in death cutscene.
* [Fix] #82: Room 104 items are not low priority in RE 1.
* [Fix] #86: RE 1 double doors to plant 42 room is not randomized.
* [Fix] #87: RE 1 Barry Forrest cutscene NPC rando always Barry.
* [Fix] #88: RE 1 softlock with Tyrant 1 elevator cutscene.
* [Fix] #89: RE 2 Vaccine placed in RPD in Claire B.
* [Fix] #90: Improve tooltips for door rando sliders.
* [Fix] #91: RE 1 "file is already in use by another process" error occurs.
* [Fix] RE 1, room 408, snakes are floating.
* [Fix] RE 2, room 608, sherry voice not randomized in room 608.
* [Fix] RE 2, room 115, Leon NPC not randomized for Claire B.
* [Fix] RE 2, potential softlock if manhole cover is open for Claire.
* [Fix] RE 2, room 301, softlock when swapping Ada NPC swap.

BioRand v2.0.2 (2022-12-18)

* [Change] Avoid duplicate actors in include type pool.
* [Change] Allow any NPC for Ada swap in room 300.
* [Fix] Make Barry's gift in room 203 low priority for RE 1.
* [Fix] Improve RE2 directory validation.
* [Fix] Potential issues with room 303.
* [Fix] Update model textures for damage.
* [Fix] #60: Room 409 two music tracks play.
* [Fix] #70: Killing Chimeras crashes RE 1.
* [Fix] #71: BGM tracks larger than 30 MiB crash RE 1.
* [Fix] #74: Room 60C cutscene has no randomized voice for Annette NPC.
* [Fix] #75: Chess piece not locked to clock tower to ensure G boss spawns.
* [Fix] #76: Music is replaced with ambient sounds from RE 1.
* [Fix] #77: Small key spawns with 0 quanity.
* [Fix] #81: Duplicate items when playing with "Shuffle default items".

BioRand v2.0.1 (2022-12-17)

* [Fix] opcode NOP logic not working, caused many crashes, and player swaps.
* [Fix] Annette player model.
* [Fix] RE 2 safe theme replaced with calm music instead of safe music.

BioRand v2.0.0 (2022-12-17)

* [Feature] Annette, Ben, Brad, Hunk, Irons, Kendo, Marvin are now playable characters.
* [Feature] Barry, Chris, Enrico, Jill, Rebecca, Richard, and Wesker are now playable characters.
* [Feature] Barry, Chris, Enrico, Jill, Rebecca, Richard, and Wesker  can now be NPCs (requires RE1 game data).
* [Feature] Beretta now available for all male and female playable charactes.
* [Feature] Bowgun, Colt, Grenade Launcher, and Sparkshot are now available for all male playable characters.
* [Feature] Resident Evil 1 door randomizer now implemented.
* [Feature] Boss music tracks now shuffled separately from danger music tracks.
* [Feature] Additional music tracks from RE 2 remake added.
* [Feature] Music tracks can now be shuffled between Resident Evil 1 and 2.
* [Fix] #61: "You Unlocked It" Freeze at ventilation shaft.
* [Fix] #64: Room 200 rando assumed ladder was down.
* [Fix] #66: Room 216 Softlock possible when NPC is Claire or Ben.
* [Fix] #68: Room 700 for B scenario crashes in door rando.

BioRand v1.3.3 (2022-12-11)

* [Fix] Final door locked in scenario A
* [Fix] Clipped audio samples are silent when using HQ sound pack

BioRand v1.3.2 (2022-12-10)

* [Change] Some underlying logic has been changed to cater for upcoming RE1 randomizer.
* [Enhancement] Characters now avoid saying a voice line that directly mentions a character not in the room.
* [Enhancement] Improved non-key item distribution.
* [Fix] #42: Room 300 voice incorrect for Ben
* [Fix] #54: Allow alternative routes can cause softlock with Leon
* [Fix] #56: Room 504 Two BGM tracks are playing at the same time
* [Fix] #57 Lift in pump room is often in incorrect state
* [Fix] #58 Room 60E lickers can be knocked off platform

BioRand v1.3.0 (2022-12-05)

* [Feature] Add buttons to view the generation log
* [Feature] Add some extra BGM from RE4
* [Feature] Add some extra save room BGM from various RE games
* [Feature] Add visualization for enemy types
* [Enhancement] Add random chance of Brad zombies
* [Enhancement] Include Claire A vaccine related items in door randos
* [Enhancement] Do not spawn keys for unconnected doors
* [Fix] #33: Room 216 Annette swap causes softlock
* [Fix] #43: Room 21B revisiting the room in a door rando can cause incorrect camera angle
* [Fix] #44: Room 10E, 216, 500 Irons can cause cutscene softlock
* [Fix] #45: Room 20A, 20B moths crash the game
* [Fix] #46: Room 60A chute doesn't always require the lighter
* [Fix] #47: Room 40E should not be left unconnected
* [Fix] #48: Room 608, 617 play two music tracks
* [Fix] #49: Room 401 sometimes camera angle is wrong when going down ladder
* [Fix] #50: Room 40E Claire B softlock if Sherry partner flag is already set
* [Fix] #51: Room 30B Ada model glitch due to NPC swap
* [Fix] #52: Room 606 softlock due to "can't go back now" message
* [Fix] #53: Room 40A softlock caused by partner flag during aligator fight
* [Fix] Browse dialog crash if initial directory does not exist
* [Fix] Inconsistent text size on generated background
* [Change] Make documents in stars office low priorty
* [Change] Prevent plugs from spawning in room 701
* [Change] Increase chance of rooms with cutscenes being included in door randos
* [Change] Reduce chance of same NPC as player and force NPCs to be swapped to a different character
* [Change] Reduce 'S.T.A.R.S. room' desk inspection from 50 down to 5
* [Change] Rename 'Prevent soft lock' to 'Safe key placement'

BioRand v1.2.2 (2022-11-25)

* [Enhancement] Allow previously disabled locker items to be randomized
* [Enhancement] Guarantee weapons for harder enemy difficulties
* [Enhancement] Make the seed font clearer on the background
* [Enhancement] Randomize NPCs for room 702
* [Fix] #31: Room 219 unable to go through door after triggering Ben cutscene
* [Fix] #32: First licker in corridor is silent
* [Fix] #34: Item 501:2 should require the lighter
* [Fix] #35: Setting all item ratios to 0 crashes biorand and will not open again
* [Fix] #36: Room 603 radio cutscene causes incorrect camera angle in door rando
* [Fix] #38: No grenade rounds are generated
* [Fix] #39: Room 40E Claire B Sherry cutscene sometimes crashes the game

BioRand v1.2.1 (2022-11-22)

* [Feature] Add a pie chart to user interface to show item pickup distribution
* [Feature] Add version and seed info to in-game backgrounds
* [Enhancement] Allow more enemy types for Leon in room 21A
* [Fix] #22: Sparkshot pickup always has only 1% ammo
* [Fix] #23: Room 21B elevator glitches when unlocked
* [Fix] #24: Room 60C for door rando has a camera glitch
* [Fix] #25: Room 402 soft lock caused by sherry NPC switch
* [Fix] #26: Room 614 soft lock possible if document is replaced in door rando with key item
* [Fix] #27: Room 213 door rando causes Japanese translation to crash when inspecting puzzle
* [Fix] #28: Room 208 document is not randomized
* [Fix] #30: In some rooms, replaced enemies are silent

BioRand v1.2.0 (2022-11-21)

* [Feature] Add documentation link to user interface
* [Feature] Add a file integrity warning when generating a rando
* [Feature] Add an ETA to the user interface for door rando
* [Enhancement] Avoid identical seeds for both scenarios
* [Enhancement] Prevent lickers, dogs, tyrants in crowded rooms for easy enemy difficulty
* [Enhancement] Room 60A is now a bridge node.
* [Fix] #6: Hall ladder is an unlock
* [Fix] #8: Room 60A to 609 link breaks door rando
* [Fix] #9: Room 404 crash due to NPC swap
* [Fix] #10: Room 10A licker replacement enemy is not on ground
* [Fix] #11: Room 30D exiting as Claire switches player to Sherry
* [Fix] #12: Room 502 incorrect camera angle for Claire
* [Fix] #14: Room 109, 20C tyrant encounters cause camera glitches and soft lock
* [Fix] #15: Some door lock IDs were duplicated causing some doors to not require a key or randomly unlock
* [Fix] #16: Soft lock may occur when 'alternative routes' is enabled
* [Fix] #18: Room 601 soft lock due to early Sherry spawn
* [Fix] #19: Room 608 cutscene locks doors and can cause soft lock
* [Fix] #20: Room 216 Claire door rando connects blocked door causing soft lock
* [Fix] Room 409 crash due to NPC swap
* [Fix] Room 612 crash due to NPC swap
* [Fix] NPCs that did not get swapped would keep same voice lines
* [Fix] NPCs that could get swapped, always got swapped

BioRand v1.1.0 (2022-11-18)

* [Feature] Door randomization can now be enabled
* [Feature] Show more helpful error message if access is denied
* [Fix] #1: G-Virus is swapped when enabling 'Shuffle default items'
* [Fix] #2: Error is given when setting all item ratios to 0
* [Fix] #3: Room 410, NPC change can crash
* [Fix] #4: Room 301, NPC change can soft lock
* [Fix] #5: Randomize optional reverse-player sections
* [Fix] 3 small keys are now placed for Leon B
* [Fix] Randomize Fuse Case

BioRand v1.0.1 (2022-11-06)

* [Fix] Generation for game structures that do not contain a data directory.

BioRand 1.0.0 (2022-11-03)

* [Feature] First release of BioRand, with initial support for Resident Evil 2.
