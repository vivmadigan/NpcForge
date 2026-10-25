Describe the scene in one go: where the players are, who they meet, what they want from that person, and anything you already know about them. Press Enter when you are done.
> [skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\scene-interviewer\SKILL.md (3625 chars, 44b91186)
[loop] turn 1: 3 tool call(s), history 3, 1083 in / 297 out
[tool] record_answer {"field":"setting","value":"town\u0027s only inn","quote":"they have gone into the town\u0027s only inn"}
[tool]   Recorded setting = "town's only inn" (the GM's words).
[tool] record_answer {"field":"playersWant","value":"information about the town","quote":"They want to get information about the town"}
[tool]   Recorded playersWant = "information about the town" (the GM's words).
[tool] record_answer {"field":"occupation","value":"innkeeper","quote":"the innkeeper"}
[tool]   Recorded occupation = "innkeeper" (the GM's words).
[loop] turn 2: 1 tool call(s), history 7, 1453 in / 111 out
[tool] record_answer {"field":"obstacle","value":"Wont","quote":"he refuses to give them anything"}
[tool]   Recorded obstacle = "Wont" (inferred).
[loop] turn 3: 0 tool call(s), history 9, 1586 in / 17 out
[interview] Done.

Here is the character so far. Nothing is rolled until you say yes.

  setting: town's only inn   [my words]
    from "they have gone into the town's only inn"
    in   "They have noticed something is off about the town and so they have gone into the town's only inn, which is empty apart from the innkeeper."

  playersWant: information about the town   [my words]
    from "They want to get information about the town"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  difficulty: Wall   [default]
    nothing recorded, so it takes its usual default

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
[skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (2239 chars, 6b58d7c2)
[loop] turn 1: 1 tool call(s), history 3, 712 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[loop] turn 2: 0 tool call(s), history 5, 761 in / 2049 out
### 1. Who they are
**Mara Venn** took over the town’s only inn after years of working its common room.

### 2. Where they stand
Mara knows plenty about the town, but won’t volunteer it to the players: she’s intent on avoiding someone she owes a favour to, and doesn’t want to risk being drawn into anything that might bring them together.

### 3. How they seem, and what is underneath
She takes small slights personally, and gets close enough to make people lean back when she talks. Beneath the prickliness, she’s anxious about crossing paths with the person she owes.

### 4. Levers
- **[weakness]** Do her a small kindness—a quick, unasked-for help—and she’ll feel she owes the players a return. “You didn’t have to do that. Ask me one thing about town, then.”
- **[characterWants]** Promise to keep the conversation discreet and not draw attention to her. She’ll risk giving them useful information, quietly. “Fine. But you didn’t get it from me, and don’t make a scene about it.”
- **[wrongAbout]** Let her believe they can pass a message through their fellow adventurers; she’ll trade a little information for the favour. “You all know each other, don’t you? Tell your lot I won’t be available.”

### 5. Anti-levers
- **[attitude]** Mock her or snap at a minor rebuke, and she’ll take offence. “If you’re going to be insulting, you can find your answers somewhere else.”
- **[mannerism]** Make a pointed show of pushing her away when she crowds you, and she’ll treat it as a slight. “Oh, so that’s how you want to talk to me?”
- **[characterWants]** Press her to face the person she’s avoiding, or bring that person up insistently, and she shuts the conversation down. “I said I’m not getting involved.”

### 6. Afterwards
- **Got it:** She keeps an eye out for the person she’s avoiding and makes sure the players don’t linger.
- **Didn’t:** She returns to her work, watchful for any sign that the players might bring trouble to her door.
- **Made an enemy:** She remembers the slight and won’t share anything useful with them again.
[app] saved as 2 in C:/Users/camer/AppData/Local/Temp/claude/C--Users-camer-source-repos-NpcForge/cd839712-1138-421c-9f7d-a0b33dc1635b/scratchpad/chars.json
