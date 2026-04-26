# Deploying the Backstage Server to Railway

This guide walks you through getting the cloud relay running so your viewers can reach it. Don't worry if you've never deployed a server before — Railway makes it very simple, and each step here explains what's happening and why.

**Why do we need a server at all?**
Your viewers need somewhere to click a button on the internet. Your home computer can't serve web pages to the world (firewalls, dynamic IPs, etc.). Railway is a free cloud hosting service — you deploy the server there once, and it runs 24/7 without you doing anything. When a viewer clicks the button, it reaches Railway, Railway tells your local app, and your app fires the shock. Your PiShock credentials never touch Railway.

---

## Before you start

- Make sure you have a [Railway](https://railway.app) account (free tier is enough — sign up with GitHub)
- Make sure you have forked or copied this repo to your own GitHub account
- Have a terminal open in the folder where you downloaded this code

---

## Step 1 — Generate two secret values

You need two random secret strings. Think of them like passwords that only you know:

- **ARM_SECRET** — proves to Railway that you (and only you) are allowed to arm the shock button
- **CHANNEL_TOKEN** — makes your viewer URL stable so it doesn't change every time Railway restarts

Run these two commands in your terminal and save the output somewhere (a text file is fine):

```powershell
# Generate ARM_SECRET
python -c "import secrets; print(secrets.token_urlsafe(32))"

# Generate CHANNEL_TOKEN (run it again to get a different value)
python -c "import secrets; print(secrets.token_urlsafe(32))"
```

You should get two different strings that look like random letters and numbers. Keep them — you'll need them in the next steps.

---

## Step 2 — Push to GitHub

Railway deploys from a GitHub repository. Your forked repo is already on GitHub, so this is probably already done.

If you downloaded a zip instead of forking, you'll need to create a new GitHub repository and push the files there. If you're not sure how to do that, ask GitHub Copilot: *"How do I push this folder to a new GitHub repository?"*

---

## Step 3 — Create a Railway project

### 3a. Create a new project

1. Go to [railway.app](https://railway.app) and log in
2. Click **New Project**
3. Choose **Deploy from GitHub repo**
4. If prompted, click **Configure GitHub App** to give Railway access to your repositories
5. Find and select your repository from the list

Railway will start trying to deploy. It might fail at first — that's fine, we need to configure it first.

### 3b. Tell Railway to look in the `server/` folder

The code that runs on Railway lives inside the `server/` subfolder, not the root of the repo. We need to tell Railway where to look:

1. Click on the service that was created (it'll have a random name)
2. Click the **Settings** tab at the top
3. Find **Source** and look for **Root Directory**
4. Type `server` and press Enter
5. Railway will automatically redeploy

**Why does this matter?** The root of this repo contains your local CLI code and config templates. Railway only needs the server files. If we leave it pointing at the root, Railway would try to run the wrong thing.

### 3c. Add your secret values as environment variables

Environment variables are how you pass secrets to a deployed app without putting them in the code (which would be publicly visible on GitHub). Think of them as settings that only exist on Railway's servers:

1. Click the **Variables** tab
2. Click **New Variable** and add:
   - Name: `ARM_SECRET` → Value: the first secret you generated in step 1
   - Name: `CHANNEL_TOKEN` → Value: the second secret you generated in step 1
3. Railway will redeploy automatically after you add variables

### 3d. Get your public URL

1. Click the **Settings** tab
2. Find **Networking** → **Public Networking**
3. Click **Generate Domain**
4. Copy the URL — it'll look something like `backstage-production-abc123.up.railway.app`

### 3e. Check it's working

Paste this in your browser (replace with your actual URL):

```
https://your-app.up.railway.app/health
```

You should see: `{"status":"ok"}`

If you see an error or a blank page, check the **Deployments** tab in Railway — it'll show you what went wrong.

---

## Step 4 — Wire up your local CLI

Open your `.env` file (copy from `.env.example` if you haven't already) and fill in:

```
RAILWAY_URL=https://your-app.up.railway.app
ARM_SECRET=<the same ARM_SECRET you put in Railway>
```

The `ARM_SECRET` must be identical in both places. This is how your local app proves to Railway that it's really you.

---

## Step 5 — Get your viewer URL

Run this once to get the permanent link your viewers will use:

```powershell
python cli.py get-url
```

It prints a URL like `https://your-app.up.railway.app/shock/abc123...`

**Paste this in your stream chat and pin it. You never need to change it** — it stays the same across Railway restarts because we set `CHANNEL_TOKEN`.

---

## Step 6 — Test it end to end

Run the shock button with a low intensity to test:

```powershell
python cli.py shock-button --max-intensity 10 --duration 500 --label "Test"
```

Open the viewer URL in a browser. You should see your label and a slider. Move the slider and click the button. The shock should fire within a couple of seconds.

If it works — you're done! Go stream.

---

## Resetting the viewer URL

If you want to invalidate the old viewer URL (e.g. after a stream, or if someone shared it somewhere they shouldn't have):

```powershell
python cli.py rotate-token
```

This gives you a new URL. Update it in Railway's `CHANNEL_TOKEN` variable to make it permanent.

---

## If you redeploy

Railway redeploys automatically whenever you push to your GitHub repo. The slot state is in-memory, so a redeploy will expire any currently armed buttons. Don't push while a shock-button session is live.


## Prerequisites

- A [Railway](https://railway.app) account (free tier is sufficient)
- This `backstage-template/` folder pushed to a GitHub repo of your own
- Python 3.11+ and a local venv set up (`python -m venv venv`, then `pip install -r requirements.txt`)

---

## 1. Generate secrets

You need two secrets before you start:

```powershell
# ARM_SECRET — authenticates your CLI to the Railway server
python -c "import secrets; print(secrets.token_urlsafe(32))"

# CHANNEL_TOKEN — makes the viewer URL stable across Railway restarts
python -c "import secrets; print(secrets.token_urlsafe(32))"
```

Save both — you will need them in Railway (step 3) and your local `.env` (step 4).

---

## 2. Push to GitHub

Railway deploys from a GitHub repo. Push this template (or your fork of it) to a new GitHub repo.

You can keep it private — Railway just needs read access.

---

## 3. Deploy to Railway

### 3a. Create a new project

1. Go to [railway.app](https://railway.app) and log in
2. Click **New Project → Deploy from GitHub repo**
3. Authorise Railway to access your GitHub account if prompted
4. Select your repository

### 3b. Set the root directory

Railway needs to run from `server/`, not the repo root:

1. Click the service that was created
2. Go to **Settings → Source**
3. Set **Root Directory** to `server`
4. Railway will redeploy automatically

### 3c. Set environment variables

1. In the service, go to **Variables**
2. Add both:
   - `ARM_SECRET` = the value you generated above
   - `CHANNEL_TOKEN` = the second value you generated above
3. Railway will redeploy

### 3d. Generate a public domain

1. In the service, go to **Settings → Networking**
2. Click **Generate Domain**
3. Copy the domain — it will look like `my-backstage.up.railway.app`

### 3e. Verify the deployment

```powershell
curl https://my-backstage.up.railway.app/health
# Expected: {"status":"ok"}
```

---

## 4. Configure the local CLI

Edit your local `.env` file:

```env
RAILWAY_URL=https://my-backstage.up.railway.app
ARM_SECRET=<same value you put in Railway>
```

---

## 5. Get the viewer URL

```powershell
python cli.py get-url
```

This prints the permanent viewer URL — paste it once in your stream chat. It stays the same across Railway restarts as long as `CHANNEL_TOKEN` is set.

To invalidate it (e.g. between streams):

```powershell
python cli.py rotate-token
# Prints the new URL — update your pinned link
# Update CHANNEL_TOKEN in Railway to the new value to persist it
```

---

## 6. Test end-to-end

```powershell
python cli.py shock-button --max-intensity 20 --duration 1000 --label "Test"
```

Open the URL in a browser — you should see the intensity slider and ZAP button. Click it, and the shock should fire locally.

---

## Redeployment

Any push to the connected GitHub branch triggers an automatic redeploy. The slot store is in-memory, so any redeploy expires all currently armed slots. Avoid deploying while a shock-button is live.
