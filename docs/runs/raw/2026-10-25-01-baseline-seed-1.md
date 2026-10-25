Describe the scene in one go: where the players are, who they meet, what they want from that person, and anything you already know about them. Press Enter when you are done.
> [skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\scene-interviewer\SKILL.md (3625 chars, 44b91186)
[loop] turn 1: 5 tool call(s), history 3, 1083 in / 498 out
[tool] record_answer {"field":"setting","value":"town\u0027s only inn","quote":"gone into the town\u0027s only inn"}
[tool]   Recorded setting = "town's only inn" (the GM's words).
[tool] record_answer {"field":"playersWant","value":"information about the town","quote":"get information about the town"}
[tool]   Recorded playersWant = "information about the town" (the GM's words).
[tool] record_answer {"field":"difficulty","value":"Wall","quote":"unless threatened with death"}
[tool]   Recorded difficulty = "Wall" (inferred).
[tool] record_answer {"field":"occupation","value":"innkeeper","quote":"the innkeeper"}
[tool]   Recorded occupation = "innkeeper" (the GM's words).
[tool] record_answer {"field":"obstacle","value":"Wont","quote":"he refuses to give them anything"}
[tool]   Recorded obstacle = "Wont" (inferred).
[loop] turn 2: 0 tool call(s), history 9, 1693 in / 45 out
[interview] Done.

Here is the character so far. Nothing is rolled until you say yes.

  setting: town's only inn   [my words]
    from "gone into the town's only inn"
    in   "They have noticed something is off about the town and so they have gone into the town's only inn, which is empty apart from the innkeeper."

  playersWant: information about the town   [my words]
    from "get information about the town"
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
[app] brief {"setting":"town\u0027s only inn","playersWant":"information about the town","difficulty":"Wall","occupation":"innkeeper","characterWants":"to keep a secret buried","obstacle":"Wont","attitude":"distrustful","mannerism":"answers questions with questions","weakness":"is easily distracted by gossip","willNotDiscuss":"someone they have a crush on","wrongAbout":"believes expensive things are always better"}
[skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (2239 chars, 6b58d7c2)
[loop] turn 1: 1 tool call(s), history 3, 707 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[loop] turn 2: 0 tool call(s), history 5, 756 in / 1262 out
## Who they are
**Mara Venn**, who has kept the town’s only inn running long enough to learn that guests ask more than they mean to.

## Where they stand
Mara knows the town’s business, but won’t give the players a useful account just because they ask. They’re watching for questions that might disturb a secret they’re determined to keep buried.

## How they seem, and what is underneath
They’re distrustful and answer questions with questions: “What makes you think I’d know?” Beneath the wary manner, gossip can catch their attention—and expensive offerings impress them more than they should.

## Levers
- **Weakness — easily distracted by gossip:** Bring Mara a juicy, credible bit of town gossip and let them get absorbed in it. They may trade a little ordinary town information before remembering to guard their words. “You heard that from whom? And what else did they say?”
- **wrongAbout — expensive things are always better:** Offer something plainly valuable as a show of good faith. It may persuade Mara to share a carefully limited piece of information, though it won’t buy the buried secret. “At least you understand what a proper gift looks like. What exactly did you want to know?”

## Anti-levers
- **attitude — distrustful:** Press them to answer, or treat their caution as proof they’re hiding something. They’ll shut down. “And why should I tell you?”
- **willNotDiscuss — someone they have a crush on:** Ask about or tease them over that person. Mara will end that line of questioning at once. “We’re not discussing them. Ask about the town, or don’t.”
- **wrongAbout — expensive things are always better:** Offer something cheap while insisting it ought to impress them. They’ll take it as an insult, not a reason to cooperate. “You think that’s worth my time?”

## Afterwards
If the players win a disclosure, Mara shares only the limited town information they managed to draw out, then keeps the secret buried. If they leave empty-handed, Mara watches for them at the inn and is less willing to hear them out next time.
[app] saved as 1 in C:/Users/camer/AppData/Local/Temp/claude/C--Users-camer-source-repos-NpcForge/cd839712-1138-421c-9f7d-a0b33dc1635b/scratchpad/chars.json
