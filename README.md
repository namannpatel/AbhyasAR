# AbhyasAR

**Practise safety, don't just read it.** AbhyasAR is an augmented-reality safety-training app for workers, with an admin dashboard for supervisors. Workers practise real procedures on their own Android phone, take a short quiz, and earn a certificate with a QR code that anyone can check online. *Abhyas* means "practice" in Hindi.

## Trainings

| Training | What the worker does |
|---|---|
| **Fire Safety** | Places a fire-safety station in their room with one floor tap, raises the alarm, picks the right extinguisher for the fire, and uses the **P-A-S-S** technique (Pull, Aim, Squeeze, Sweep) on four scenarios: a bin, a power outlet, an electrical cabinet and a gas furnace. Points are only earned for steps actually done. |
| **Machine Training** | Practises the controls of an AR conveyor belt: start, stop, emergency stop, reset, manual jog and speed. |

Each training ends with a 10-question quiz. The final mark is **practice 40% + quiz 60%**, and **80 out of 100 passes**. A pass issues a certificate automatically.

## Features

- **Worker login** with an ID and password created by an admin. Works offline after the first sign-in.
- **Offline first.** Attempts and certificates are saved on the phone and upload by themselves when the phone is back online.
- **Certificates** with the worker's name, score, date and a verification QR. They can be viewed stacked in the app, zoomed, and saved to the phone's gallery.
- **Public verification page.** Scanning a certificate's QR opens a page that confirms it against the server.
- **Three languages:** English, Hindi and Santali, with spoken guidance in English and Hindi (Santali uses the Hindi voice).
- **Admin dashboard:** create logins in bulk, follow progress and pass rates, see certificates, export CSV.

## How it is built

| Part | Technology |
|---|---|
| App | Unity **6000.5.11f1**, C#, AR Foundation 6.6 + ARCore, Universal Render Pipeline, Input System, TextMesh Pro |
| Backend | [Supabase](https://supabase.com) (Postgres with row-level security, database functions) |
| Dashboard | Static site (`dashboard/`), published with GitHub Pages |
| Target | Android 10+ (API 29), 64-bit ARM, IL2CPP. The phone must support ARCore. |

## Repository layout

| Path | Contents |
|---|---|
| `Assets/Scripts*` | App code, one folder per area: `ScriptsFireExtinguisher`, `ScriptsConveyor`, `ScriptsQuiz`, `ScriptsCertificate`, `ScriptsSync`, `ScriptsAuth`, `ScriptsLocalization`, `ScriptsShared` |
| `Assets/Scenes` | `Login`, `MainMenu`, `FireTraining` (Fire Safety), `ConveyorTest` (Machine Training) |
| `Assets/Resources` | Language files (`Localization/*.json`), certificate images, Supabase settings |
| `CertificateTemplates/` | The SVG sources the certificate images are rendered from |
| `dashboard/` | Admin dashboard and the public certificate-verification page (see its own README) |
| `supabase/migrations/` | Database setup, applied in number order |
| `Tools/Localization/` | Translation and narration generation scripts (development only) |
| `docs/` | Project documents, including the showcase-video script |

## Getting started

1. Install Unity **6000.5.11f1** with the Android Build Support module (SDK, NDK and OpenJDK).
2. Open the project folder in Unity.
3. Set up Supabase and the dashboard by following [`dashboard/README.md`](dashboard/README.md), then put the project URL and public key into `Assets/Resources/SupabaseConfig.asset`.
4. AR needs a real phone. In the Editor you can use AR Foundation's XR Simulation; assign a simulation environment to test placement.

### Build the APK

Switch the platform to Android and build (File → Build Profiles), or use *Build* from the Unity command line. The package ID is `com.oasisuniandes.abhyasar`. Before handing builds to workers, sign them with **one** shared release keystore (not Unity's debug key) and raise the version code on every build, so updates install over older versions.

## Dashboard and certificate verification

The dashboard is published from `dashboard/` by `.github/workflows/dashboard-pages.yml`. In the repository's **Settings → Pages**, set **Source** to **GitHub Actions**. Certificate QR codes point to `https://namannpatel.github.io/AbhyasAR/#/verify/<certificate-id>`, so the repository must keep the name `AbhyasAR`.

## Localization

All on-screen text lives in `Assets/Resources/Localization/{en,hi,sat}.json`. Missing Hindi or Santali entries fall back to English. The Santali lines are machine-translated and still need review by a native speaker (see `Tools/Localization/README.md`).

## Known limitations

- Hindi and Santali names show broken conjunct letters (TextMesh Pro does not shape them). English names are fine.
- Certificates and the recorded narration are not translated: the certificate design is English.
- Performance on low-end phones depends on ARCore support and hardware.

## License

[MIT](LICENSE) for the project's own code. Third-party models, textures, effects, fonts and audio in `Assets/` have their own licences, so check them before any commercial use.
