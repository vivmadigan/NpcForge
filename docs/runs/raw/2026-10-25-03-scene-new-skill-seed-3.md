Describe the scene in one go: where the players are, who they meet, what they want from that person, and anything you already know about them. Press Enter when you are done.
> [skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\scene-interviewer\SKILL.md (3625 chars, 44b91186)
[loop] turn 1: 5 tool call(s), history 3, 1083 in / 505 out
[tool] record_answer {"field":"setting","value":"town\u0027s only inn","quote":"they have gone into the town\u0027s only inn"}
[tool]   Recorded setting = "town's only inn" (the GM's words).
[tool] record_answer {"field":"playersWant","value":"information about the town","quote":"They want to get information about the town"}
[tool]   Recorded playersWant = "information about the town" (the GM's words).
[tool] record_answer {"field":"difficulty","value":"Wall","quote":"he refuses to give them anything, unless threatened with death."}
[tool]   Recorded difficulty = "Wall" (inferred).
[tool] record_answer {"field":"occupation","value":"innkeeper","quote":"the innkeeper"}
[tool]   Recorded occupation = "innkeeper" (the GM's words).
[tool] record_answer {"field":"obstacle","value":"Wont","quote":"he refuses to give them anything"}
[tool]   Recorded obstacle = "Wont" (inferred).
[loop] turn 2: 0 tool call(s), history 9, 1699 in / 19 out
[interview] Done.

Here is the character so far. Nothing is rolled until you say yes.

  setting: town's only inn   [my words]
    from "they have gone into the town's only inn"
    in   "They have noticed something is off about the town and so they have gone into the town's only inn, which is empty apart from the innkeeper."

  playersWant: information about the town   [my words]
    from "They want to get information about the town"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  difficulty: Wall   [inferred]
    from "he refuses to give them anything, unless threatened with death"
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
[app] brief {"setting":"town\u0027s only inn","playersWant":"information about the town","difficulty":"Wall","occupation":"innkeeper","characterWants":"to keep a secret buried","obstacle":"Wont","attitude":"trying hard to sound professional","mannerism":"lowers their voice as though sharing a secret","weakness":"finds it hard to turn down a wager","willNotDiscuss":"their terrible sense of direction","wrongAbout":"thinks whispering means nobody else can hear them"}
[app] scene sent (306 chars)
[skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (3468 chars, c18284d9)
[loop] turn 1: 1 tool call(s), history 3, 1102 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[loop] turn 2: 0 tool call(s), history 5, 1151 in / 2560 out
## Who they are
**Tomas Venn** has kept the village’s only inn for twenty years and knows how to make a room sound welcoming even when it isn’t.

## Where they stand
Tomas knows the villagers are sheltering in their cellars because a sealed mine has begun venting deadly gas at dusk. The council is preparing an evacuation, but Tomas wants the danger kept secret until then, so nobody panics or goes looking for the source. He will not give the players information unless they threaten him with death.

## How they seem, and what is underneath
He tries hard to sound like a professional innkeeper: measured, courteous, and firm. When he answers, he lowers his voice as if sharing a confidence—even though the inn is empty. Beneath the polish, he is frightened that the players will expose the secret or get themselves killed. He is also sure that whispering means nobody else can hear him.

## Levers
- **A threat of death (scene):** This is the condition that makes him yield. A threat must be clear; ordinary pressure won’t do. “All right. The mine. The gas comes at dusk. The council’s hiding the villagers until they can get them out.”
- **A wager (weakness):** He finds it hard to turn one down. It can draw him into a bet, but it won’t get him to reveal the town’s secret without the threat above. “A wager, then. Name what you want to stake.”

## Anti-levers
- **Ordinary questions or demands (scene; Wall/Wont):** Without a death threat, he stays closed. “I’m sorry, but I can’t help you with that.”
- **Asking about directions (willNotDiscuss):** He shuts down that subject rather than admit how bad he is at finding his way. “Directions? No. Ask someone else.”
- **Calling attention to his whispering (wrongAbout):** He thinks the whisper is private and becomes flustered when told otherwise. “Keep your voice down. No one else needs to hear this.”

## Afterwards
If threatened with death, **got it**: Tomas tells them about the mine and the gas, then tries to keep the news from spreading. If they leave without making that threat, **didn’t**: he gives them nothing about the town.
[app] saved as 6 in C:/Users/camer/AppData/Local/Temp/claude/C--Users-camer-source-repos-NpcForge/cd839712-1138-421c-9f7d-a0b33dc1635b/scratchpad/chars.json
