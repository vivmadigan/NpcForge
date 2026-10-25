Describe the scene in one go: where the players are, who they meet, what they want from that person, and anything you already know about them. Press Enter when you are done.
> [skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\scene-interviewer\SKILL.md (3625 chars, 44b91186)
[loop] turn 1: 5 tool call(s), history 3, 1092 in / 570 out
[tool] record_answer {"field":"setting","value":"small village","quote":"The players are in a small village."}
[tool]   Recorded setting = "small village" (the GM's words).
[tool] record_answer {"field":"playersWant","value":"information about the town","quote":"They want to get information about the town from the innkeeper"}
[tool]   Recorded playersWant = "information about the town" (the GM's words).
[tool] record_answer {"field":"difficulty","value":"Wall","quote":"he refuses to give them anything, unless threatened with death."}
[tool]   Recorded difficulty = "Wall" (inferred).
[tool] record_answer {"field":"occupation","value":"innkeeper","quote":"from the innkeeper"}
[tool]   Recorded occupation = "innkeeper" (the GM's words).
[tool] record_answer {"field":"obstacle","value":"Wont","quote":"he refuses to give them anything"}
[tool]   Recorded obstacle = "Wont" (inferred).
[loop] turn 2: 0 tool call(s), history 9, 1772 in / 52 out
[interview] Done.

Here is the character so far. Nothing is rolled until you say yes.

  setting: small village   [my words]
    from "The players are in a small village"
    in   "The players are in a small village."

  playersWant: information about the town   [my words]
    from "They want to get information about the town from the innkeeper"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  difficulty: Wall   [inferred]
    from "he refuses to give them anything, unless threatened with death"
    in   "They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death."

  occupation: innkeeper   [my words]
    from "from the innkeeper"
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
[app] brief {"setting":"small village","playersWant":"information about the town","difficulty":"Wall","occupation":"innkeeper","characterWants":"to keep a secret buried","obstacle":"Wont","attitude":"distrustful","mannerism":"answers questions with questions","weakness":"is easily distracted by gossip","willNotDiscuss":"someone they have a crush on","wrongAbout":"believes expensive things are always better"}
[app] scene sent (343 chars)
[skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (3628 chars, 65d91edc)
[loop] turn 1: 1 tool call(s), history 3, 1130 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[loop] turn 2: 0 tool call(s), history 5, 1179 in / 2570 out
### 1. Who they are
**Marek Venn** has kept the village inn for twenty years, long enough to know which doors should stay shut after dusk.

### 2. Where they stand
Marek knows why the village feels wrong: after dark, a thing on the road calls in the voices of missing villagers. The others hide in their cellars; Marek stays at the inn as a decoy. He once let the thing inside, and he wants that secret buried. He will not tell the players anything unless they credibly threaten to kill him.

### 3. How they seem, and what is underneath
He watches the players with wary suspicion, answering each question with another: “What makes you ask?” Beneath the guarded manner is a man afraid that one careless answer will lead the thing to the people in hiding.

### 4. Levers
- **A credible threat of death** *(scene)* is the one thing that makes him give way. He tells them what comes down the road, where the villagers are hiding, and that he let it into the inn. “Fine. You want the truth? Keep your voice down, and don’t repeat it outside.”
- **Gossip about the village** *(weakness: easily distracted by gossip)* can briefly pull him off his guard, but won’t make him reveal the secret. “Who told you that? No, wait—what else have they been saying?”

### 5. Anti-levers
- **Treating him as untrustworthy or pressing him without the threat that would make him yield** *(attitude: distrustful; scene)* makes him retreat further behind questions. “And why should I tell you anything?”
- **Asking about the person he has a crush on** *(will not discuss)* shuts the subject down. “That’s not your concern.”
- **Calling something cheap or settling for the lesser option** *(wrong about expensive things always being better)* makes him dismissive. “If that’s all you’re willing to spend, I doubt you’re serious.”

### 6. Afterwards
If they threaten him and get the truth, Marek watches them leave, knowing he has exposed the villagers—and remembering who forced his hand. If they don’t, he gives them nothing and keeps watch from the inn window.
[app] saved as 10 in C:/Users/camer/AppData/Local/Temp/claude/C--Users-camer-source-repos-NpcForge/cd839712-1138-421c-9f7d-a0b33dc1635b/scratchpad/chars.json
