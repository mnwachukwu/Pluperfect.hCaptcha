# Pluperfect.hCaptcha

[![Build and test](https://github.com/mnwachukwu/Pluperfect.hCaptcha/actions/workflows/ci.yml/badge.svg)](https://github.com/mnwachukwu/Pluperfect.hCaptcha/actions/workflows/ci.yml)

How every Pluperfect Development service asks whether a submission came from a person. Two halves
of one question, in one repository, so a project adopts both from a single checkout.

| | |
|---|---|
| `src/Pluperfect.HCaptcha` | The .NET library. `net10.0`, consumed by project reference. |
| `client/` | The React hook. An npm package, consumed by a `file:` dependency. |
| `tests/Pluperfect.HCaptcha.Tests` | NUnit. The registration rule is the safety story, so it is what gets asserted. |

## The shape

Server:

```csharp
var result = await verifier.VerifyAsync(model.CaptchaToken, cancellationToken);

if (!result.Success)
{
    // result.ErrorCodes carries hCaptcha's own vocabulary.
}
```

Client:

```tsx
const executeCaptcha = useHCaptcha(import.meta.env.VITE_HCAPTCHA_SITE_KEY);

const token = await executeCaptcha();
if (!token) { /* blocked script, offline, or a closed challenge */ }
```

`ICaptchaVerifier` has one method, and which verifier answers is a deployment decision expressed in
configuration rather than a choice the call site makes. That is what lets the same code path run
offline, where every token passes, and in production against hCaptcha.

Nothing throws for a failed challenge, because a failed challenge is an ordinary outcome. A
verifier that cannot reach hCaptcha reports failure too: an unverifiable submission is not a
verified one.

**There is no score.** hCaptcha returns a plain success flag; confidence scoring is an Enterprise
feature. Anything ported from reCAPTCHA v3 should lose its threshold rather than invent one.

## Configuration

Bound to the `HCaptcha` section.

| Key | |
|---|---|
| `HCaptcha:SecretKey` | The secret half of the key pair. Required unless the bypass is in force |
| `HCaptcha:Bypass` | Accept every token without asking. The offline path |
| `HCaptcha:TimeoutSeconds` | How long to wait on hCaptcha. Defaults to 10 |

```csharp
builder.Services.AddPluperfectHCaptcha(builder.Configuration, builder.Environment.IsDevelopment());
```

⚠ **The second argument is not optional in spirit.** The bypass needs configuration to ask for it
*and* the application to say its environment allows it. A setting can be copied onto a production
box by accident; an environment cannot be as easily. The parameter defaults to `false`, so
forgetting it fails closed rather than open.

The library takes that as a parameter instead of reading `IHostEnvironment` itself, because a
library that inspects the host is a library that behaves differently depending on who hosts it.

### Where the secret lives

In the deployment's own configuration on its own box, mode `0600`, or in an environment variable
(`HCaptcha__SecretKey` — a double underscore; a single one fails silently). Not in this repository
and not in a CI secret: CI has no reason to verify a captcha.

## Developing against it

Set `HCaptcha:Bypass` and every token is accepted, so a contact form is fully exercisable with no
network and no credentials. The counterpart of `Pluperfect.Mail`'s pickup directory.

On the client, hCaptcha publishes test keys that always issue a passing token. The site key is
public by design, so this one is safe to commit:

```
VITE_HCAPTCHA_SITE_KEY=10000000-ffff-ffff-ffff-000000000001
```

A fresh clone then has a working contact form with nothing to set up.

## Consuming the .NET library

By project reference to a **sibling** checkout. One relative path has to resolve both on a
development machine and on a runner, which it does as long as both places have the same shape:

```
<parent>/
├── Pluperfect.hCaptcha/      ← this repository
└── <the consuming repository>/
```

⚠ **The number of `..` differs per project**, because consuming projects sit at different depths.
Getting it wrong resolves to a path inside the consuming repository, where nothing exists.

| Consuming project | Depth | `ProjectReference Include` |
|---|---|---|
| `Courtney.Care/Server/Courtney.Care.Api` | 3 | `..\..\..\Pluperfect.hCaptcha\src\Pluperfect.HCaptcha\Pluperfect.HCaptcha.csproj` |
| `Studio TM14 Site/Server/Pluperfect.Api` | 3 | `..\..\..\Pluperfect.hCaptcha\src\Pluperfect.HCaptcha\Pluperfect.HCaptcha.csproj` |

Count from the directory holding the consuming `.csproj` up to the directory holding both
repositories, then append `Pluperfect.hCaptcha\src\Pluperfect.HCaptcha\Pluperfect.HCaptcha.csproj`.

⚠ The sibling directory must be named `Pluperfect.hCaptcha`, because that name is inside the
relative path. The consuming repository's own directory name is free — only its depth matters.

## Consuming the client package

The same idea, expressed the way npm expresses it. In the consuming front end's `package.json`:

```json
"dependencies": {
  "@pluperfect/hcaptcha": "file:../../Pluperfect.hCaptcha/client"
}
```

| Consuming front end | Depth | `file:` path |
|---|---|---|
| `Courtney.Care/Client` | 2 | `file:../../Pluperfect.hCaptcha/client` |
| `Studio TM14 Site/Client` | 2 | `file:../../Pluperfect.hCaptcha/client` |

npm symlinks the directory into `node_modules`, and `exports` resolves to `client/dist`.

⚠ **`dist/` is git-ignored, so the sibling has to be built before a consumer installs.** One
command, and it belongs in the consumer's CI before its own `npm ci`:

```bash
npm ci --prefix ../Pluperfect.hCaptcha/client && npm run build --prefix ../Pluperfect.hCaptcha/client
```

### Three things that bite, and what to do about them

**1. A second copy of React.** A `file:` dependency is a symlink, so anything React-shaped inside
this package's own `node_modules` resolves as a *different* React than the application's. Two
copies in one page and every hook throws `Invalid hook call`, with a stack trace that points
nowhere useful.

Mitigated on both sides, and both are load bearing:

- Here, `react` is a `peerDependency` and never a dependency. CI fails if that changes.
- In the consumer, Vite is told to resolve one copy:

```ts
resolve: { dedupe: ['react', 'react-dom'] }
```

**2. Stale pre-bundled code.** Vite pre-bundles dependencies and caches the result. A linked
package that changes on disk keeps serving the cached copy, so an edit here appears to do nothing
in a consumer's dev server. Exclude it:

```ts
optimizeDeps: { exclude: ['@pluperfect/hcaptcha'] }
```

**3. The relative path is recorded in the lockfile.** `npm ci` reproduces exactly what
`package-lock.json` says, including `file:../../Pluperfect.hCaptcha/client`. If a runner checks out
into a different shape, install fails outright — loudly, which is the good case. The consuming
workflow therefore checks **itself** out into a subdirectory so this repository lands beside it
rather than inside it:

```yaml
- uses: actions/checkout@v5
  with:
    path: Courtney.Care

- uses: actions/checkout@v5
  with:
    repository: mnwachukwu/Pluperfect.hCaptcha
    path: Pluperfect.hCaptcha
```

Unpinned, so every consumer builds against the current default branch. That is what you want while
several repositories are adopting it at once; add `ref:` to a workflow when a consumer needs to
stop moving.

## Building

```bash
dotnet build
```

```bash
dotnet test
```

```bash
npm ci --prefix client && npm run build --prefix client
```

Warnings are errors, code style is enforced in the build, and restores are locked. A
`packages.lock.json` that changes is a dependency that moved, and it is committed so that shows up
in review.

## Deliberate omissions

No visible checkbox component, no theming, no `onVerify` callback prop. The hook is invisible-mode
only, because every form in this fleet wants the same thing: a token at submit time and nothing in
the layout. Each of those is easy to add later and impossible to remove once a caller depends on
it.
