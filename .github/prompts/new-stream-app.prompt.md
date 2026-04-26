---
description: "Scaffold a new stream session project from scratch. Creates the sessions/ folder, entry point, config, and gets it running in one shot."
argument-hint: "App name and language, e.g. 'chat-overlay in C#' or 'hype-bot in Python'"
agent: "agent"
tools: [read, edit, execute, search, todo]
---

Scaffold a new NialoLand stream session project based on the user's request: ${input}

## Steps

1. **Parse the request** — extract the app name (convert to kebab-case) and language (C#, Python, or TypeScript)

2. **Get today's date**:
   ```powershell
   $date = Get-Date -Format "yyyy-MM-dd"
   ```

3. **Create a `live/` branch**:
   ```powershell
   git checkout -b live/$date-{app-name}
   ```

4. **Create the session folder** under `sessions/`:
   ```
   NialoLand/
     sessions/
       {date}-{app-name}/
   ```

5. **Scaffold the project** based on language:

   **C#**:
   ```powershell
   cd sessions/$date-{app-name}
   dotnet new console -n {AppName} -f net8.0
   dotnet add package Spectre.Console
   ```
   Update `Program.cs` with a startup banner using `AnsiConsole.Write(new FigletText("{AppName}"))` and a status line.

   **Python**:
   - Create `main.py` with a `rich` Console banner and `if __name__ == "__main__":` guard
   - Create `requirements.txt` with `rich` and any other needed packages
   - Set up venv: `python -m venv .venv ; .venv\Scripts\activate ; pip install -r requirements.txt`

   **TypeScript**:
   ```powershell
   cd sessions/$date-{app-name}
   npm init -y
   npm install -D typescript ts-node @types/node
   npm install chalk
   npx tsc --init
   ```
   Create `src/index.ts` with a chalk startup banner.
   Add `"start": "ts-node src/index.ts"` to `package.json` scripts.

6. **Create a stub `README.md`** with the app name and a one-liner about what it does

7. **Verify it runs** — execute the entry point and confirm output

8. **Make first commit**:
   ```powershell
   git add .
   git commit -m "feat: scaffold {app-name}"
   ```

9. **Report back** — show the session folder path and the command to run it

## Output Goal

At the end, the user should be able to run ONE command and see a colored, branded startup output. That's the stream-ready starting point.
