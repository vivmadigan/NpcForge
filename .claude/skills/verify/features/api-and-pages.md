# The API and the pages

The website is `NpcForge.Api`, a minimal API over the same core, and `NpcForge.Web`'s pages, which call it for everything. The API lists and loads saves and runs the forge's interview, roll and save. Here it runs on the stand-in model, so nothing costs money, and on a saves file of its own, so the owner's `characters.json` is never read or written.

## Sub-features

- `api-alone` starts the API on a port of its own and lists the saves of the run directory.
- `api-paid-refused` refuses a paid provider by name and starts nothing.
- `apphost-up` starts the API at 5180, the dashboard and the pages through the AppHost, and the pages reach the API.
- `apphost-held` refuses to start while a fixed port is held, and leaves the holder alone.
- `servers-stop` ends the recorded tree and leaves no process of it and no listener.

## How to get to it (user POV)

- Run `dotnet run --project NpcForge.AppHost`. It starts the API and the pages, and prints the dashboard's address. This is the owner's path, and it is paid once the forge page sends a scene unless the stand-in is set.
- Run `$V api start`, or `$V apphost start`, which are the free paths behind the fences in `../SKILL.md`. Stop them with `$V api stop` and `$V apphost stop`.

## Driving it with npcforge-verify

Preconditions:

- `dotnet build` succeeded just now and `$V doctor` prints no `FAIL`, which includes the Api's and the AppHost's builds. Both servers start with `--no-build`.
- For `apphost start`, nothing listens on 5180, 15180, 19180 or 20180. For the pages, `NpcForge.Web/node_modules` exists, or the first start installs it and may run past its wait.
- `$V start` ran, so `$D` exists. A server is stopped in the run directory that started it.

- **Paid refused.** Run `$V api start --provider openai`. Exit `64`, and the line `verify: refused: --provider openai could reach a paid model. The api runs on the stand-in only`. `ls "$D/api.pid"` finds nothing.
- **Start the API.** Run `$V api start --seed 14`. Exit `0` within a few seconds, with `[verify] api ready after N tries: http://localhost:54xx`. `$D` holds `api.cmd` (the command and its environment), `api.log`, `api.pid`, `api.ports`, `api.tree`, `api.url` and `api.saves.json`.
- **List.** Run `U=$(cat "$D/api.url"); curl --max-time 2 -s "$U/api/saves"`. A JSON array. `curl ... | grep -o '"number"' | wc -l` equals `grep -c '^  "[0-9]*": {' "$D/api.saves.json"`, 16 on the made-up saves.
- **It reads the run's file.** Run `sed -i '0,/"occupation": "innkeeper"/s//"occupation": "tampered"/' "$D/api.saves.json"`. The next listing shows `"occupation":"tampered"` in row 1. The tracked `saves.json` and `characters.json` are unchanged, which `git status --short` shows.
- **One save, and one never saved.** `curl -s -o /dev/null -w '%{http_code}' "$U/api/saves/1"` prints `200`, and for `99` prints `404`, with the body `No save numbered 99.`
- **The stand-in answers.** `curl --max-time 20 -s -X POST -H 'Content-Type: application/json' -d '{"scene":"a tired innkeeper"}' "$U/api/interviews"` returns `"kind":"question"` and costs nothing.
- **Stop the API.** Run `$V api stop`. Exit `0`, a `running before` line that names the API and its `dotnet`, and `ok:   api stopped. Running after: none`. `netstat -ano | grep LISTENING | grep ":$(cat "$D/api.ports") "` prints nothing, and `api.pid` is gone.
- **Start the AppHost.** Run `$V apphost start --seed 14`. Exit `0` in about ten seconds, with the API ready at `http://localhost:5180` and `pages ready: http://localhost:N`, N being the port Aspire chose, also in `$D/apphost.pages`. `curl -s "$(cat "$D/apphost.url")/api/saves"` and the same on `$(cat "$D/apphost.pages")` print the same body, so the pages' proxy reaches the API.
- **Held port.** With the AppHost up, run `$V start`, which warns that the AppHost is still recorded in the old directory, then `$V apphost start`. Exit `64`, with `refused: port 5180 is held by pid N (dcp.exe), which this run did not start`. The first AppHost still answers on 5180. Put `NPCFORGE_VERIFY_DIR` back to the old directory to stop it.
- **Stop the AppHost.** Run `$V apphost stop`. The `running before` line lists the AppHost, DCP, the dashboard, the API and node, then `Running after: none`. `netstat -ano | grep LISTENING | grep -E ':(5180|15180|19180|20180) '` prints nothing.
- **Proof.** `$D` holds each server's `.cmd`, `.log`, `.pid` while it runs, `.tree` (every process the tree held, with its start time), `.ports` and `.url`, and the `.saves.json` the API read.

## Gotchas

- Stop a server with `$V ... stop`, never with `taskkill` or `timeout` on the root alone. The AppHost starts DCP detached, so DCP, the dashboard, the API and Vite are not its children, and an AppHost ended by `timeout` once left them running until a `git worktree remove` failed.
- List listeners with `netstat -ano`, not `netstat -ano -p tcp`. The pages' Vite binds `[::1]` alone, and `-p tcp` shows IPv4 only, so it hides a held port.
- Under the AppHost the API runs in Development and can read the owner's user secrets. The stand-in and `NPCFORGE_OFFLINE=1` are the two stops that keep it free. The API alone runs with `--no-launch-profile`, so it is Production.
- A stop with the wrong run directory finds nothing. `NPCFORGE_VERIFY_DIR` names the directory that started the server.
- Not mapped. The browser tests (`npm --prefix NpcForge.Web test`) start their own API and dev server on 5381 and 5382, which this skill never uses. Whether a page reads well is *needs my eyes*.
