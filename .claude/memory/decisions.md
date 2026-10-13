# Decision Log

ADR rows, oldest first. **Status:** Draft (proposed, may change) · Active (committed) · Superseded (say by what). Open decisions are listed in `Documents/Operations/Roadmap.md` §4 until the operator decides them; each decision then gets a row here.

| Date | Decision | Rationale | Alternatives Considered | Status |
| :--- | :--- | :--- | :--- | :--- |
| 2026-10-13 | **Claude workflow:** agents, skills (scope / plan / phase / review / audit / handshake), commit-title hook, ledgers, Log Discipline, phase/plan templates. | The operator's preferred working loop. | Fresh minimal setup. | Active |
| 2026-10-13 | **Browser web app in TypeScript: runtime + editor sharing one versioned JSON format; runtime sim pure `step(state, input)` at a fixed 60 Hz.** | No install for students; a finished game is a URL; the pure sim is headless-testable and reused by the play-only page; Claude can verify autonomously. | Unity kit (the idea this project replaces). | Draft (confirm with the operator alongside the runtime spike) |
| 2026-10-13 | **Repo layout proposed as an npm-workspaces monorepo** (`packages/format`, `packages/runtime`, `apps/editor`, `apps/player`). | Enforces the dependency direction (editor → runtime → format) at the package boundary. | Single package with folders (boundary by convention only). | Draft (confirmed or changed by the scaffold plan) |
