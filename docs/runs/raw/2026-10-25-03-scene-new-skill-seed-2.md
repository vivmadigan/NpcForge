Describe the scene in one go: where the players are, who they meet, what they want from that person, and anything you already know about them. Press Enter when you are done.
> [skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\scene-interviewer\SKILL.md (3625 chars, 44b91186)
[loop] turn 1: 5 tool call(s), history 3, 1083 in / 699 out
[tool] record_answer {"field":"setting","value":"town\u0027s only inn","quote":"the town\u0027s only inn"}
[tool]   Recorded setting = "town's only inn" (the GM's words).
[tool] record_answer {"field":"playersWant","value":"information about the town","quote":"get information about the town from the innkeeper"}
[tool]   Recorded playersWant = "information about the town" (the GM's words).
[tool] record_answer {"field":"difficulty","value":"Wall","quote":"unless threatened with death"}
[tool]   Recorded difficulty = "Wall" (inferred).
[tool] record_answer {"field":"occupation","value":"innkeeper","quote":"the innkeeper"}
[tool]   Recorded occupation = "innkeeper" (the GM's words).
[tool] record_answer {"field":"obstacle","value":"Wont","quote":"he refuses to give them anything"}
[tool]   Recorded obstacle = "Wont" (inferred).
[loop] turn 2: 0 tool call(s), history 9, 1894 in / 28 out
[interview] Done.

Here is the character so far. Nothing is rolled until you say yes.

  setting: town's only inn   [my words]
    from "the town's only inn"
    in   "They have noticed something is off about the town and so they have gone into the town's only inn, which is empty apart from the innkeeper."

  playersWant: information about the town   [my words]
    from "get information about the town from the innkeeper"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  difficulty: Wall   [inferred]
    from "unless threatened with death"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  occupation: innkeeper   [my words]
    from "the innkeeper"
    in   "They have noticed something is off about the town and so they have gone into the town's only inn, which is empty apart from the innkeeper."

  characterWants: [rolled]
    nothing recorded, so the dice pick it after your yes

  obstacle: Wont   [inferred]
    from "he refuses to give them anything"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  attitude: [rolled]
    nothing recorded, so the dice pick it after your yes

  mannerism: [rolled]
    nothing recorded, so the dice pick it after your yes

  weakness: [rolled]
    nothing recorded, so the dice pick it after your yes

  willNotDiscuss: [rolled]
    nothing recorded, so the dice pick it after your yes

  wrongAbout: [rolled]
    nothing recorded, so the dice pick it after your yes

Roll this character? Type yes to roll it. Anything else stops without rolling. [server] info: ModelContextProtocol.Server.McpServer[570385771]
[app] openai gpt-6-luna
[app] brief {"setting":"town\u0027s only inn","playersWant":"information about the town","difficulty":"Wall","occupation":"innkeeper","characterWants":"to avoid someone they owe a favour to","obstacle":"Wont","attitude":"easily offended","mannerism":"invades personal space","weakness":"feels obliged to repay small kindnesses","willNotDiscuss":"how much money they have","wrongAbout":"thinks all adventurers know one another"}
[app] scene sent (306 chars)
[skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (3468 chars, c18284d9)
[loop] turn 1: 1 tool call(s), history 3, 1095 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[loop] turn 2: 0 tool call(s), history 5, 1144 in / 3099 out
## Who they are
**Hobb Tern**, the village innkeeper; the healer once saved his life, and he owes her a favour.

## Where they stand
Hobb knows why the village feels wrong: a fever is spreading, and the old well is contaminated. The healer has asked him to keep quiet while she treats the sick. He refuses to tell the players because he doesn’t want the healer to learn he broke his word.

## How they seem, and what is underneath
He puts on a brisk, overfamiliar welcome, leaning far too close as he asks if they know “any of the other adventurers passing through.” It’s a strained act: he’s worried about the sick villagers and afraid of betraying the healer. A slight or accusation quickly makes him snap.

## Levers
- **The scene’s condition — threaten him with death:** This is what makes him give way. He goes pale, backs off, and tells them about the fever, the contaminated well, and the healer’s efforts. “Fine! The old well. That’s where it started. Now stay away from it—and don’t tell her I said a word.”
- **Weakness — feels obliged to repay small kindnesses:** A small kindness earns a practical favour, but not the information on its own. “You didn’t have to do that. Here—take this clean water, at least. It’s not from the well.”

## Anti-levers
- **Attitude — easily offended:** Call him a liar or accuse him of caring more about his promise than the villagers. He bristles and shuts down. “You come into my inn and insult me? Then you can leave knowing nothing.”
- **Mannerism — invades personal space:** Shoving him back or making a show of disgust at how close he stands makes him take offence. “Don’t you put your hands on me. Get out.”
- **Wrong about — thinks all adventurers know one another:** Claiming some other adventurer sent you makes him wary, as if the healer might hear of the conversation through them. “So you all talk, do you? Then you can ask your friend what’s going on.”

## Afterwards
If threatened with death, **they got the information**—and Hobb has betrayed the healer’s confidence. He’ll be frightened and resentful, and may tell her who forced his hand. Otherwise, **they didn’t**; he keeps quiet and wants them gone.
[app] saved as 5 in C:/Users/camer/AppData/Local/Temp/claude/C--Users-camer-source-repos-NpcForge/cd839712-1138-421c-9f7d-a0b33dc1635b/scratchpad/chars.json
