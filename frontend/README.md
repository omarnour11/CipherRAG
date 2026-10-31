# CipherRAG Frontend Console

This is the standalone web frontend for CipherRAG.

## Deploying to Vercel

1. Push this repository to GitHub.
2. In [Vercel](https://vercel.com), click **Add New > Project** and import your repository.
3. In **Project Settings**:
   - **Root Directory**: Select `frontend` (or leave as root `./`).
   - **Framework Preset**: `Other`.
4. To connect to your Render backend:
   - **Option A (Zero-Code Rewrite)**: In `vercel.json`, replace `https://YOUR-RENDER-APP.onrender.com` with your actual Render service URL.
   - **Option B (Direct Connect)**: In `config.js`, set `window.CIPHERRAG_API_URL = "https://your-app.onrender.com"`.
   - **Option C (In-Browser UI)**: On the login screen, click **Configure API** and paste your Render URL.
5. Click **Deploy**.

