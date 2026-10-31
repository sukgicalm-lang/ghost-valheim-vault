# Ghost launcher 1.0.11: scroll and resize investigation
Date: 2026-10-31. Investigation only; user explicitly deferred patches while playing.
No application source, installed files, release assets, public website, update feed or game DLL is changed by this branch.

## Verified shipped input
Runtime: GhostR2-Windows-1.0.11.zip, 219349839 bytes.
SHA-256: 5fe20bb2b386c8e5d8969c0cde4fcd3b2285eaefd78a71a1316ff9b54e396488.
Downloaded the public runtime, verified SHA-256 and read its actual resources/app.asar (190556304 bytes).
Its emitted Manager CSS and LocalModList CSS match the inspected source.

## Confirmed scroll regression
Pinned original 1.0.5 Manager.vue uses a row flex container for manager-main-view.
The automatic-setup notice introduced flex-direction:column. The router-view beneath it still has the default automatic minimum height.
Its content-based minimum height grows with the entire mod list, while ancestors clip overflow.
The inner mod-list-content has overflow-y:auto, but expands to its entire content height; there is no inner overflow to scroll.

Reduced browser reproduction used the shipped layout declarations and 29 fixed-height (42px) dummy rows in a 240px shell. This was an isolated MDN Playground preview; no source was published there.
Read actual DOM clientHeight, scrollHeight, bounding rectangles and computed styles:

| Layout | List clientHeight | List scrollHeight | Router height | Scrollable |
| --- | ---: | ---: | ---: | --- |
| Original row layout, no setup notice | 198 | 1218 | 238 | Yes |
| Current column layout with setup notice | 1218 | 1218 | 1258 | No |
| Current layout with explicit minimum size override | 158 | 1218 | 198 | Yes |

The experiment demonstrates the CSS mechanism in Chromium, not a full Windows Electron gameplay test.

Proposed later correction:
- Keep the automatic-setup notice and column layout.
- Set min-height:0 on manager-main-view and router-view; explicitly constrain the LocalModList pane as well.
- Set min-width:0 on the corresponding flexible containers so long package names cannot stretch the layout.
- Keep the list itself flex:1 with overflow-y:auto and min-height:0.
- Do not replace ordinary native wheel scrolling with scripted smooth scrolling.
- Test local and online lists, expanded cards, search, settings/config editor, minimum and maximized window sizes, and both ordinary wheel and middle-button behavior separately.

The existing global auxclick preventDefault is not sufficient evidence that it causes Windows middle-button autoscroll failure. Middle-button down defaults and ordinary wheel scrolling are separate mechanisms.

## Resize: facts and limits
Confirmed in shipped code:
- window-state-persist.ts is unchanged from pinned source 1.0.5.
- resize events debounce state persistence for 100ms, then call ensureDirSync and writeFileSync in the main process.
- This timer saves geometry; it does not animate the window or delay setSize/setBounds.
- The main process also calls execFileSync(tasklist.exe) for exclusivity/game-running guards. These are conditional checks, not continuous resize handlers. A fully ready setup returns before these installation guards.
- No explicit disableHardwareAcceleration, disable-gpu, setFrameRate, setSize or setBounds customization is present in the actual backend.
- No deliberate whole-window size animation was found.
- File traversal for setup uses asynchronous IPC-backed filesystem operations; do not incorrectly label every setup read as synchronous.

These are performance risks/candidates, not proof of the dominant cause on the user's PC.
Do not claim that a 100ms persistence delay causes 100ms of visual resize lag.
The layout regression can also increase sizing work, but its contribution to frame time has not been measured.

Prepared remediation and verification sequence:
1. Correct the proven flexible-height regression.
2. Move window-state persistence to nonblocking serialized writes after interaction settles; preserve final normal/maximized geometry and avoid stale writes.
3. Replace blocking process enumeration with asynchronous enumeration while retaining fail-closed safety and checks immediately before installation. Do not cache away required fresh pre-install checks.
4. Measure Windows resize frames and main/renderer long tasks before/after, both with Valheim closed and running.
5. Inspect actual GPU feature status if stutter persists; do not disable hardware acceleration globally on an untested assumption.
6. Only then build and publish a new immutable launcher version, preserving all 29 mod versions, game DLLs, personal configs and profiles.

## References
- https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/flex
- https://www.electronjs.org/docs/latest/tutorial/performance
- https://developer.mozilla.org/en-US/docs/Web/API/Element/auxclick_event
