# SurakshaAR admin dashboard

A static site with no build step. Admins use it to create worker logins and to see each worker's synced training progress.

## One-time Supabase setup

1. Create a project at supabase.com.
2. Open **SQL Editor**, paste in `supabase/migrations/0001_init.sql`, and run it.
3. Go to **Authentication → Users → Add user**, create an admin with an email and password, and copy that user's UUID.
4. In the SQL Editor, run:
   ```sql
   insert into public.admins (user_id) values ('<admin-user-uuid>');
   ```
5. Go to **Authentication → Providers → Email** and turn off "Allow new users to sign up", so that only admins you add can sign in.
6. Copy **Project URL** and the **anon public key** from **Project Settings → API** into:
   - `dashboard/config.js`
   - `Assets/Resources/SupabaseConfig.asset` in Unity (select the asset and fill in the Inspector)

## Running

- Local: from this folder, run `npx serve .` or `python -m http.server 8080`, then open http://localhost:8080.
- GitHub Pages: `.github/workflows/dashboard-pages.yml` publishes this folder whenever `dashboard/` changes on `main`.
  1. On GitHub, open the repo's **Settings → Pages** and set **Source** to **GitHub Actions**.
  2. Push to `main`, or run the workflow by hand from the **Actions** tab.
  3. The site is published at `https://<user>.github.io/<repo>/`.

  A private repo needs GitHub Pro, Team or Enterprise for Pages. Either way the site itself is public, but only admins can sign in and see data.

## Use

- **New worker** creates a worker ID (e.g. `W-04821`) and an 8-character password. The password is shown **only once**; you can copy or print it for the worker.
- **Reset password** issues a new password. The old password stops working. Devices that already cached it have to log in online once, and their unsynced progress uploads after that.
- **Disable login** blocks the worker online, and blocks further syncing from their devices. Progress that was already synced is kept.
- Click any worker to see their attempts, pass rate, and step-by-step details. **Export CSV** downloads their history.

## How offline works (app side)

- The first login on a device needs internet. After that, the device keeps a salted hash of the password and logs in offline.
- Every completed attempt is saved on the device first, then uploaded whenever the device is online. The app retries every 30 s, backing off to every 5 min. Uploads are idempotent, so the same attempt is never counted twice.
