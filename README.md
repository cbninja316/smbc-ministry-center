# SMBC Ministry Center

A church admin status board and receipt manager for South Moore Baptist Church. Staff can track church events, facility use requests, benevolence requests, and maintenance items on a Kanban board, and manage ministry receipts with image attachments.

**Stack:** ASP.NET Core 9 (C#) backend · SQLite · HTML/CSS/JS frontend · WordPress deployment

---

## Features

- Kanban status board with drag-and-drop (To Do → In Progress → Done)
- Auto-advances events/facility uses by date
- Five item types: Church Events, Facility Use, Benevolence, Maintenance, Receipts
- Full benevolence form (IRS-compliant with committee review section, print support)
- Full church event request form with promotion options and print support
- Receipt image upload and viewer (stored locally on the church machine)
- Role-based access: Super Admin and Admin roles
- Email-based user invitations
- JWT authentication

---

## Project Structure

```
smbc-status-board/
├── backend/
  └── SmbcStatusBoard.Api/        # ASP.NET Core 9 Web API
      ├── Controllers/
      ├── Models/
      ├── DTOs/
      ├── Services/
      ├── Migrations/
      ├── appsettings.Development.json
      └── appsettings.example.json  ← copy this to appsettings.json
```

---

## Local Development Setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) (for `npx serve` to serve the frontend)

### 1. Clone the repo

```bash
git clone https://github.com/cbninja316/smbc-ministry-center.git
cd smbc-ministry-center
```

### 2. Configure the backend

Copy the example settings file and fill in your values:

```bash
cp backend/SmbcStatusBoard.Api/appsettings.example.json \
   backend/SmbcStatusBoard.Api/appsettings.json
```

Edit `appsettings.json` and replace all `REPLACE_WITH_...` placeholders:

| Key                       | Description                                                                       |
| ------------------------- | --------------------------------------------------------------------------------- |
| `Jwt.Key`                 | Any random string, 32+ characters                                                 |
| `App.FrontendUrl`         | Set to `*` for local dev (allows any origin)                                      |
| `Email.*`                 | Your SMTP credentials (HostGator: `mail.yoursite.org`, port 587)                  |
| `Storage.ReceiptsPath`    | Local folder for receipt images (e.g. `C:\smbc\receipts` or `/tmp/smbc-receipts`) |
| `Seed.SuperAdminPassword` | Password for the auto-created admin account on first run                          |

> **Tip:** For local development, `appsettings.Development.json` is already configured with safe defaults (`FrontendUrl: "*"`, `/tmp/smbc-receipts` storage). You only need to set `appsettings.json` for production.

### 3. Create the receipts folder

**Mac/Linux:**

```bash
mkdir -p /tmp/smbc-receipts
```

**Windows:**

```
mkdir C:\smbc\receipts
```

### 4. Run the backend

```bash
cd backend/SmbcStatusBoard.Api
dotnet run
```

The API starts at `http://localhost:5000`. On first run it automatically:

- Creates the SQLite database and runs all migrations
- Seeds the super admin account using your `Seed` settings

### 5. Expose the local backend (ngrok)

Since the WordPress site is on HTTPS, it can't call a plain `http://localhost` backend directly (mixed content). Use ngrok to create a secure tunnel:

**Install:**

```bash
brew install ngrok/ngrok/ngrok          # macOS
# or download from https://ngrok.com
```

**Authenticate** (one-time, free account at ngrok.com):

```bash
ngrok config add-authtoken YOUR_TOKEN
```

**Run** (every time you start the church machine):

```bash
# Terminal 1 — backend
cd backend/SmbcStatusBoard.Api
dotnet run

# Terminal 2 — ngrok tunnel
ngrok http 5000
```

Copy the `https://xxxx.ngrok-free.app` URL from the ngrok output, set it as `API_BASE` in `config.js`, and re-upload `config.js` to HostGator.

> **Note:** Free ngrok URLs change every time you restart the tunnel. Re-upload `config.js` after each restart. For a permanent URL, upgrade to a paid ngrok plan or use [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/) (free).

---

## Environment Quick Reference

| Setting                     | Local dev                   | WordPress + ngrok                 |
| --------------------------- | --------------------------- | --------------------------------- |
| `API_BASE`                  | `http://localhost:5000/api` | `https://xxxx.ngrok-free.app/api` |
| `PAGES.login`               | `/login.html`               | `/smbc-login/`                    |
| `PAGES.dashboard`           | `/index.html`               | `/smbc-admin/`                    |
| `FrontendUrl` (appsettings) | `*`                         | `https://your-church-site.org`    |

---

## Default Credentials (Development)

After first run against `appsettings.Development.json`:

- **Username:** `admin`
- **Password:** `Admin123!`

Change these immediately in `appsettings.json` for any production deployment.

---

## Updating Files on Production

When you make code changes, only these files usually need to be re-uploaded to HostGator:

| Changed                              | Upload to                                  |
| ------------------------------------ | ------------------------------------------ |
| `frontend/css/styles.css`            | `themes/smbc-admin/smbc/css/`              |
| `frontend/js/*.js`                   | `themes/smbc-admin/smbc/js/`               |
| `frontend/wordpress-templates/*.php` | Active theme folder (e.g. `themes/astra/`) |
